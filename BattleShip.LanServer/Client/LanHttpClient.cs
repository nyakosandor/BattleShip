using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BattleShip.LanServer.Contracts;

namespace BattleShip.LanServer.Client;

/// <summary>
/// HTTP-backed <see cref="ILanClient"/>. Use <see cref="ConnectAsync"/> to create and
/// connect an instance in a single step; the returned client owns the underlying
/// <see cref="HttpClient"/> unless you pass <c>ownsHttpClient: false</c>.
/// </summary>
public sealed class LanHttpClient : ILanClient
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;

    private LanHttpClient(HttpClient http, bool ownsHttpClient, Uri baseAddress, ConnectResponse identity)
    {
        _http = http;
        _ownsHttpClient = ownsHttpClient;
        BaseAddress = baseAddress.ToString().TrimEnd('/');
        Token = identity.Token;
        Role = identity.Role;
        PlayerName = identity.Name;
    }

    public string Token { get; }
    public PlayerRole Role { get; }
    public string PlayerName { get; }
    public string BaseAddress { get; }

    /// <summary>
    /// Connect to a running <see cref="LanHost"/> and return a ready-to-use client.
    /// </summary>
    public static async Task<LanClientResult<LanHttpClient>> ConnectAsync(
        Uri baseAddress,
        string playerName,
        HttpClient? http = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(baseAddress);

        // Keep the per-request timeout tighter than the server's disconnect threshold so that
        // a silently dropped host surfaces as a NetworkError well before the 5s forfeit window.
        var ownsHttp = http is null;
        var client = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        client.BaseAddress = new Uri(baseAddress.ToString().TrimEnd('/') + "/");

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "connect")
            {
                Content = JsonContent.Create(new ConnectRequest(playerName), options: LanJson.Options),
            };
            using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
            var identity = await TryReadAsync<ConnectResponse>(response, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode || identity is null)
            {
                if (ownsHttp) client.Dispose();
                var err = await TryReadErrorAsync(response, ct).ConfigureAwait(false);
                return LanClientResult<LanHttpClient>.Fail(err.code, err.message);
            }

            var connected = new LanHttpClient(client, ownsHttp, baseAddress, identity);
            return LanClientResult<LanHttpClient>.Ok(connected);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            if (ownsHttp) client.Dispose();
            throw;
        }
        catch (HttpRequestException ex)
        {
            if (ownsHttp) client.Dispose();
            return LanClientResult<LanHttpClient>.Network(ex.Message);
        }
        catch (TaskCanceledException ex)
        {
            if (ownsHttp) client.Dispose();
            return LanClientResult<LanHttpClient>.Network("Request timed out: " + ex.Message);
        }
    }

    public async Task<LanClientResult<StateResponse>> GetStateAsync(CancellationToken ct = default)
    {
        return await SendAsync<StateResponse>(HttpMethod.Get, "state", content: null, ct).ConfigureAwait(false);
    }

    public async Task<LanClientResult<StateResponse>> DeployAsync(IReadOnlyList<PlacementDto> placements, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(placements);
        var body = JsonContent.Create(new DeployRequest(placements), options: LanJson.Options);
        return await SendAsync<StateResponse>(HttpMethod.Post, "action/deploy", body, ct).ConfigureAwait(false);
    }

    public async Task<LanClientResult<FireResponse>> FireAsync(CoordinateDto target, CancellationToken ct = default)
    {
        var body = JsonContent.Create(new FireRequest(target), options: LanJson.Options);
        return await SendAsync<FireResponse>(HttpMethod.Post, "action/fire", body, ct).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
    {
        if (_ownsHttpClient)
        {
            _http.Dispose();
        }
        return ValueTask.CompletedTask;
    }

    private async Task<LanClientResult<T>> SendAsync<T>(HttpMethod method, string relative, HttpContent? content, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(method, relative) { Content = content };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);

            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                var value = await TryReadAsync<T>(response, ct).ConfigureAwait(false);
                if (value is null)
                {
                    return LanClientResult<T>.Network("Server returned no body.");
                }
                return LanClientResult<T>.Ok(value);
            }

            var err = await TryReadErrorAsync(response, ct).ConfigureAwait(false);
            return LanClientResult<T>.Fail(err.code, err.message);
        }
        catch (HttpRequestException ex)
        {
            return LanClientResult<T>.Network(ex.Message);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            return LanClientResult<T>.Network("Request timed out: " + ex.Message);
        }
    }

    private static async Task<T?> TryReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(LanJson.Options, ct).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return default;
        }
        catch (NotSupportedException)
        {
            return default;
        }
    }

    private static async Task<(string code, string message)> TryReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var err = await TryReadAsync<ErrorResponse>(response, ct).ConfigureAwait(false);
        if (err is not null)
        {
            return (err.Error, err.Message);
        }
        return ("http_" + (int)response.StatusCode, response.ReasonPhrase ?? "Request failed.");
    }
}
