using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using BattleShip.Core.Domain;
using BattleShip.Core.Services;
using BattleShip.LanServer.Contracts;

namespace BattleShip.LanServer;

/// <summary>
/// Self-contained LAN host built on <see cref="HttpListener"/>. No ASP.NET Core dependency,
/// so it drops straight into a MAUI project. The listener runs on a background loop and
/// dispatches requests to the authoritative <see cref="LanGameSession"/>.
/// </summary>
public sealed class LanHost : ILanHost
{
    private readonly IGameEngine _engine;
    private readonly object _sync = new();

    private HttpListener? _listener;
    private CancellationTokenSource? _listenerCts;
    private Task? _acceptLoop;
    private LanGameSession? _session;
    private string? _baseAddress;
    private string? _lanAddress;

    public event Action? StateChanged;

    public LanHost(IGameEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    public bool IsRunning => _listener?.IsListening == true;
    public string? BaseAddress => _baseAddress;
    public string? LanAddress => _lanAddress;
    public LanGameSession? Session => _session;
    public ConnectResponse? HostIdentity => _session?.HostIdentity;

    public Task StartAsync(string hostName, Board hostBoard, int port, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(hostBoard);

        lock (_sync)
        {
            if (IsRunning) throw new InvalidOperationException("Host is already running.");

            var session = new LanGameSession(_engine);
            session.RegisterHost(hostName, hostBoard);
            session.StateChanged += RaiseStateChanged;

            var listener = new HttpListener();
            string bound = BindListener(listener, port);

            _session = session;
            _listener = listener;
            _baseAddress = bound;
            _lanAddress = GetLanAddress(port);
            _listenerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _acceptLoop = Task.Run(() => AcceptLoopAsync(listener, session, _listenerCts.Token));
        }

        RaiseStateChanged();
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        HttpListener? listener;
        CancellationTokenSource? cts;
        Task? loop;
        LanGameSession? session;

        lock (_sync)
        {
            listener = _listener;
            cts = _listenerCts;
            loop = _acceptLoop;
            session = _session;

            _listener = null;
            _listenerCts = null;
            _acceptLoop = null;
            _session = null;
            _baseAddress = null;
            _lanAddress = null;
        }

        try { cts?.Cancel(); } catch { }
        try { listener?.Stop(); } catch { }
        try { listener?.Close(); } catch { }

        if (loop is not null)
        {
            try { await loop.ConfigureAwait(false); }
            catch { /* ignore shutdown exceptions */ }
        }

        if (session is not null)
        {
            session.StateChanged -= RaiseStateChanged;
        }

        cts?.Dispose();
        RaiseStateChanged();
    }

    public ValueTask DisposeAsync() => new(StopAsync());

    private async Task AcceptLoopAsync(HttpListener listener, LanGameSession session, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (HttpListenerException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (InvalidOperationException) { break; }

            _ = Task.Run(() => HandleRequestAsync(context, session), ct);
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, LanGameSession session)
    {
        try
        {
            var req = context.Request;
            var res = context.Response;

            // Permissive CORS for local dev tools.
            res.Headers["Access-Control-Allow-Origin"] = "*";
            res.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization";
            res.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";

            if (req.HttpMethod == "OPTIONS")
            {
                res.StatusCode = 204;
                res.Close();
                return;
            }

            var path = req.Url?.AbsolutePath.TrimEnd('/') ?? string.Empty;
            if (path.Length == 0) path = "/";

            var route = (req.HttpMethod, path);
            switch (route)
            {
                case ("GET", "/health"):
                    await WriteJsonAsync(res, 200, new { status = "ok" });
                    break;
                case ("POST", "/connect"):
                    await HandleConnectAsync(req, res, session);
                    break;
                case ("GET", "/state"):
                    await HandleStateAsync(req, res, session);
                    break;
                case ("POST", "/action/deploy"):
                    await HandleDeployAsync(req, res, session);
                    break;
                case ("POST", "/action/fire"):
                    await HandleFireAsync(req, res, session);
                    break;
                default:
                    await WriteErrorAsync(res, 404, "not_found", $"No handler for {req.HttpMethod} {path}.");
                    break;
            }
        }
        catch (Exception ex)
        {
            try
            {
                await WriteErrorAsync(context.Response, 500, "internal_error", ex.Message);
            }
            catch
            {
            }
        }
    }

    private static async Task HandleConnectAsync(HttpListenerRequest req, HttpListenerResponse res, LanGameSession session)
    {
        var body = await ReadJsonAsync<ConnectRequest>(req);
        var result = session.RegisterGuest(body?.Name);
        await WriteActionAsync(res, result, successStatus: 200);
    }

    private static async Task HandleStateAsync(HttpListenerRequest req, HttpListenerResponse res, LanGameSession session)
    {
        var token = GetBearerToken(req);
        var state = session.GetState(token);
        await WriteJsonAsync(res, 200, state);
    }

    private static async Task HandleDeployAsync(HttpListenerRequest req, HttpListenerResponse res, LanGameSession session)
    {
        var token = GetBearerToken(req);
        if (token is null)
        {
            await WriteErrorAsync(res, 401, ActionErrorCodes.Unauthorized, "Missing bearer token.");
            return;
        }

        var body = await ReadJsonAsync<DeployRequest>(req);
        if (body is null || body.Placements is null)
        {
            await WriteErrorAsync(res, 400, ActionErrorCodes.BadRequest, "Request body must include a 'placements' array.");
            return;
        }

        var result = session.Deploy(token, body.Placements);
        await WriteActionAsync(res, result, successStatus: 200);
    }

    private static async Task HandleFireAsync(HttpListenerRequest req, HttpListenerResponse res, LanGameSession session)
    {
        var token = GetBearerToken(req);
        if (token is null)
        {
            await WriteErrorAsync(res, 401, ActionErrorCodes.Unauthorized, "Missing bearer token.");
            return;
        }

        var body = await ReadJsonAsync<FireRequest>(req);
        if (body is null)
        {
            await WriteErrorAsync(res, 400, ActionErrorCodes.BadRequest, "Request body must include a 'target'.");
            return;
        }

        var result = session.Fire(token, body.Target);
        await WriteActionAsync(res, result, successStatus: 200);
    }

    // ---------- helpers ----------

    private static async Task<T?> ReadJsonAsync<T>(HttpListenerRequest req)
    {
        if (!req.HasEntityBody) return default;
        try
        {
            using var stream = req.InputStream;
            return await JsonSerializer.DeserializeAsync<T>(stream, LanJson.Options);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static async Task WriteJsonAsync<T>(HttpListenerResponse res, int statusCode, T payload)
    {
        res.StatusCode = statusCode;
        res.ContentType = "application/json; charset=utf-8";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, LanJson.Options);
        res.ContentLength64 = bytes.LongLength;
        await res.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        res.Close();
    }

    private static Task WriteErrorAsync(HttpListenerResponse res, int statusCode, string code, string message)
        => WriteJsonAsync(res, statusCode, new ErrorResponse(code, message));

    private static async Task WriteActionAsync<T>(HttpListenerResponse res, ActionResult<T> result, int successStatus)
    {
        if (result.Success && result.Value is not null)
        {
            await WriteJsonAsync(res, successStatus, result.Value);
            return;
        }

        var status = result.ErrorCode switch
        {
            ActionErrorCodes.Unauthorized => 401,
            ActionErrorCodes.SessionFull => 409,
            ActionErrorCodes.InvalidPhase or ActionErrorCodes.InvalidTurn or ActionErrorCodes.AlreadyDeployed or ActionErrorCodes.GameFinished => 409,
            ActionErrorCodes.InvalidFleet or ActionErrorCodes.InvalidShot or ActionErrorCodes.BadRequest => 400,
            _ => 400,
        };
        await WriteErrorAsync(res, status, result.ErrorCode ?? "error", result.Message ?? "Request failed.");
    }

    private static string? GetBearerToken(HttpListenerRequest req)
    {
        var header = req.Headers["Authorization"];
        if (string.IsNullOrEmpty(header)) return null;
        const string prefix = "Bearer ";
        if (!header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
        var token = header.AsSpan(prefix.Length).Trim();
        return token.Length == 0 ? null : token.ToString();
    }

    private static string BindListener(HttpListener listener, int port)
    {
        // Prefer binding to all interfaces (so LAN clients can reach us); fall back to
        // localhost-only when URL ACLs aren't registered for the current user.
        var publicPrefix = $"http://+:{port}/";
        var localPrefix = $"http://localhost:{port}/";

        listener.Prefixes.Clear();
        listener.Prefixes.Add(publicPrefix);
        try
        {
            listener.Start();
            return $"http://+:{port}";
        }
        catch (HttpListenerException)
        {
            listener.Prefixes.Clear();
            listener.Prefixes.Add(localPrefix);
            listener.Start();
            return $"http://localhost:{port}";
        }
    }

    private static string? GetLanAddress(int port)
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            var ip = host.AddressList.FirstOrDefault(a =>
                a.AddressFamily == AddressFamily.InterNetwork &&
                !IPAddress.IsLoopback(a));
            return ip is null ? null : $"http://{ip}:{port}";
        }
        catch
        {
            return null;
        }
    }

    private void RaiseStateChanged()
    {
        try { StateChanged?.Invoke(); } catch { }
    }
}
