using BattleShip.Core.Domain;

namespace BattleShip.LanServer.Contracts;

public sealed record ErrorResponse(string Error, string Message);

public sealed record ConnectResponse(
    Guid PlayerId,
    string Token,
    PlayerRole Role,
    string Name);

public sealed record ShipDto(
    ShipType Type,
    int Length,
    Orientation Orientation,
    IReadOnlyList<CoordinateDto> Coordinates,
    IReadOnlyList<CoordinateDto> Hits,
    bool IsSunk);

public sealed record PlayerPublicView(
    Guid PlayerId,
    string Name,
    PlayerRole Role,
    bool Connected,
    bool Deployed,
    int ShipsRemaining,
    IReadOnlyList<CoordinateDto> Hits,
    IReadOnlyList<CoordinateDto> Misses,
    IReadOnlyList<ShipDto> SunkShips);

public sealed record PlayerPrivateView(
    Guid PlayerId,
    string Name,
    IReadOnlyList<ShipDto> Fleet);

public sealed record StateResponse(
    GamePhase Phase,
    PlayerRole? Turn,
    PlayerRole? Winner,
    PlayerPublicView? Host,
    PlayerPublicView? Guest,
    PlayerPrivateView? Self);

public sealed record FireResponse(
    ShotOutcomeDto Outcome,
    CoordinateDto Target,
    ShipType? ShipSunk,
    bool FleetDestroyed,
    PlayerRole? NextTurn);

public enum ShotOutcomeDto
{
    Miss,
    Hit,
    Sunk,
}

public enum PlayerRole
{
    Host,
    Guest,
}

public enum GamePhase
{
    WaitingForClient,
    Deploying,
    InProgress,
    Finished,
}
