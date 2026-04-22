using BattleShip.LanServer.Contracts;

namespace BattleShip.LanServer.Client;

/// <summary>
/// Uniform client API for the LAN server, regardless of transport. The guest side is
/// backed by <see cref="LanHttpClient"/>; the host uses <see cref="LocalLanClient"/>
/// so it can play the same match screen without doing a self-loopback HTTP roundtrip.
/// </summary>
public interface ILanClient : IAsyncDisposable
{
    /// <summary>Opaque bearer token issued by the server at connect time.</summary>
    string Token { get; }

    /// <summary>Role assigned to this client (Host or Guest).</summary>
    PlayerRole Role { get; }

    /// <summary>The player name the server accepted (may be sanitised).</summary>
    string PlayerName { get; }

    /// <summary>Best-effort display string for the remote address (e.g. "http://192.168.1.10:5100" or "in-process").</summary>
    string BaseAddress { get; }

    Task<LanClientResult<StateResponse>> GetStateAsync(CancellationToken ct = default);

    Task<LanClientResult<StateResponse>> DeployAsync(IReadOnlyList<PlacementDto> placements, CancellationToken ct = default);

    Task<LanClientResult<FireResponse>> FireAsync(CoordinateDto target, CancellationToken ct = default);
}
