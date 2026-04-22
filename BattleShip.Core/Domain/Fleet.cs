namespace BattleShip.Core.Domain;

/// <summary>
/// The canonical Battleship fleet composition: Carrier, Battleship, Cruiser, Submarine, Destroyer.
/// </summary>
public static class Fleet
{
    public static IReadOnlyList<ShipType> StandardComposition { get; } = new[]
    {
        ShipType.Carrier,
        ShipType.Battleship,
        ShipType.Cruiser,
        ShipType.Submarine,
        ShipType.Destroyer,
    };
}
