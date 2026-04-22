using BattleShip.Core.Domain;

namespace BattleShip.LanServer.Contracts;

public readonly record struct CoordinateDto(int X, int Y)
{
    public Coordinate ToDomain() => new(X, Y);
    public static CoordinateDto From(Coordinate c) => new(c.X, c.Y);
}
