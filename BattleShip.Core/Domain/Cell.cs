namespace BattleShip.Core.Domain;

public sealed class Cell
{
    public Coordinate Coordinate { get; }
    public CellState State { get; internal set; }
    public Ship? Ship { get; internal set; }

    public Cell(Coordinate coordinate)
    {
        Coordinate = coordinate;
        State = CellState.Empty;
    }

    public bool HasShip => Ship is not null;
}
