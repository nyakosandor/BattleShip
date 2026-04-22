using BattleShip.LanServer.Contracts;

namespace BattleShip.LanServer.Client;

/// <summary>
/// In-process <see cref="ILanClient"/> implementation. The host already owns the
/// authoritative <see cref="LanGameSession"/>, so there is no reason to bounce its
/// actions through HTTP — they go straight into the session under the usual locks.
/// </summary>
public sealed class LocalLanClient : ILanClient
{
    private readonly LanGameSession _session;

    public LocalLanClient(LanGameSession session, ConnectResponse identity)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        ArgumentNullException.ThrowIfNull(identity);
        Token = identity.Token;
        Role = identity.Role;
        PlayerName = identity.Name;
    }

    public string Token { get; }
    public PlayerRole Role { get; }
    public string PlayerName { get; }
    public string BaseAddress => "in-process";

    public Task<LanClientResult<StateResponse>> GetStateAsync(CancellationToken ct = default)
    {
        var state = _session.GetState(Token);
        return Task.FromResult(LanClientResult<StateResponse>.Ok(state));
    }

    public Task<LanClientResult<StateResponse>> DeployAsync(IReadOnlyList<PlacementDto> placements, CancellationToken ct = default)
    {
        var result = _session.Deploy(Token, placements);
        return Task.FromResult(ToClientResult(result));
    }

    public Task<LanClientResult<FireResponse>> FireAsync(CoordinateDto target, CancellationToken ct = default)
    {
        var result = _session.Fire(Token, target);
        return Task.FromResult(ToClientResult(result));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static LanClientResult<T> ToClientResult<T>(ActionResult<T> result) => result.Success && result.Value is not null
        ? LanClientResult<T>.Ok(result.Value)
        : LanClientResult<T>.Fail(result.ErrorCode ?? "error", result.Message ?? "Request failed.");
}
