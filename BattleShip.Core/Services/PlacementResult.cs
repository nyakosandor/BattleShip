namespace BattleShip.Core.Services;

public enum PlacementError
{
    None,
    OutOfBounds,
    Overlaps,
    DuplicateShipType,
}

public readonly record struct PlacementResult(bool Success, PlacementError Error)
{
    public static PlacementResult Ok() => new(true, PlacementError.None);
    public static PlacementResult Fail(PlacementError error) => new(false, error);
}
