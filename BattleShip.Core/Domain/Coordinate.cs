namespace BattleShip.Core.Domain;

/// <summary>
/// A zero-indexed grid coordinate. X is the column (0 = A), Y is the row (0 = 1).
/// </summary>
public readonly record struct Coordinate(int X, int Y)
{
    public override string ToString() => $"({X},{Y})";
}
