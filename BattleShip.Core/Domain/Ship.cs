namespace BattleShip.Core.Domain;

public sealed class Ship
{
    private readonly HashSet<Coordinate> _hits = new();

    public ShipType Type { get; }
    public int Length { get; }
    public Orientation Orientation { get; }
    public IReadOnlyList<Coordinate> Coordinates { get; }

    public IReadOnlySet<Coordinate> Hits => _hits;

    public bool IsSunk => _hits.Count >= Length;

    public Ship(ShipType type, Orientation orientation, IEnumerable<Coordinate> coordinates)
    {
        Type = type;
        Orientation = orientation;
        Length = type.Length();

        Coordinates = coordinates.ToArray();

        if (Coordinates.Count != Length)
        {
            throw new ArgumentException(
                $"Ship {type} expects {Length} coordinates but received {Coordinates.Count}.",
                nameof(coordinates));
        }
    }

    /// <summary>
    /// Register a hit against this ship. Returns true if the coordinate belongs to the ship
    /// and had not already been hit.
    /// </summary>
    internal bool RegisterHit(Coordinate coordinate)
    {
        if (!Coordinates.Contains(coordinate))
        {
            return false;
        }

        return _hits.Add(coordinate);
    }

    public bool Occupies(Coordinate coordinate) => Coordinates.Contains(coordinate);
}
