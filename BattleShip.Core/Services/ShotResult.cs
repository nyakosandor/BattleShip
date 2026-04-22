using BattleShip.Core.Domain;

namespace BattleShip.Core.Services;

public enum ShotOutcome
{
    Miss,
    Hit,
    Sunk,
    Invalid,
}

public enum ShotError
{
    None,
    OutOfBounds,
    AlreadyShot,
}

public readonly record struct ShotResult(
    ShotOutcome Outcome,
    ShotError Error,
    Coordinate Coordinate,
    Ship? ShipHit,
    bool FleetDestroyed)
{
    public bool IsValid => Outcome != ShotOutcome.Invalid;
    public bool IsHit => Outcome is ShotOutcome.Hit or ShotOutcome.Sunk;
}
