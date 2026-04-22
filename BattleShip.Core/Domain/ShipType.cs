namespace BattleShip.Core.Domain;

/// <summary>
/// Distinct ship identities. Use <see cref="ShipTypeExtensions.Length"/> for the cell length.
/// </summary>
public enum ShipType
{
    Carrier,
    Battleship,
    Cruiser,
    Submarine,
    Destroyer,
}

public static class ShipTypeExtensions
{
    public static int Length(this ShipType type) => type switch
    {
        ShipType.Carrier => 5,
        ShipType.Battleship => 4,
        ShipType.Cruiser => 3,
        ShipType.Submarine => 3,
        ShipType.Destroyer => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown ship type."),
    };

    public static string DisplayName(this ShipType type) => type switch
    {
        ShipType.Carrier => "Carrier",
        ShipType.Battleship => "Battleship",
        ShipType.Cruiser => "Cruiser",
        ShipType.Submarine => "Submarine",
        ShipType.Destroyer => "Destroyer",
        _ => type.ToString(),
    };
}
