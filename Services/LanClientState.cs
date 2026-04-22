using BattleShip.LanServer.Client;
using BattleShip.LanServer.Contracts;

namespace BattleShip.App.Services;

/// <summary>
/// Process-wide state for the active LAN match. Holds the connected <see cref="ILanClient"/>
/// (either an in-process host adapter or an HTTP guest) and carries hand-off flags between
/// the join / setup / match screens.
/// </summary>
public sealed class LanClientState
{
    private readonly object _sync = new();

    private ILanClient? _client;
    private string? _hostBaseAddress;

    public event Action? Changed;

    public ILanClient? Client
    {
        get { lock (_sync) { return _client; } }
    }

    public bool IsConnected => Client is not null;

    public PlayerRole? Role => Client?.Role;

    /// <summary>Human-readable label for the remote the client is talking to.</summary>
    public string? DisplayEndpoint
    {
        get
        {
            lock (_sync)
            {
                if (_client is null) return null;
                return _hostBaseAddress ?? _client.BaseAddress;
            }
        }
    }

    /// <summary>
    /// The guest just connected over HTTP. The page holding this state is responsible for
    /// disposing the previous client (if any).
    /// </summary>
    public void AttachGuestClient(LanHttpClient client, string advertisedBaseAddress)
    {
        ArgumentNullException.ThrowIfNull(client);
        lock (_sync)
        {
            _client = client;
            _hostBaseAddress = advertisedBaseAddress;
        }
        RaiseChanged();
    }

    /// <summary>The host just started the server. Attach an in-process client bound to the host slot.</summary>
    public void AttachHostClient(LocalLanClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        lock (_sync)
        {
            _client = client;
            _hostBaseAddress = null;
        }
        RaiseChanged();
    }

    public async Task DisposeAndClearAsync()
    {
        ILanClient? snapshot;
        lock (_sync)
        {
            snapshot = _client;
            _client = null;
            _hostBaseAddress = null;
        }
        if (snapshot is not null)
        {
            try { await snapshot.DisposeAsync().ConfigureAwait(false); }
            catch { /* swallow — we're tearing down anyway */ }
        }
        RaiseChanged();
    }

    private void RaiseChanged()
    {
        try { Changed?.Invoke(); } catch { }
    }
}
