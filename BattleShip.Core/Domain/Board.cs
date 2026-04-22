namespace BattleShip.Core.Domain;

public sealed class Board
{
    public const int DefaultSize = 10;

    private readonly Cell[,] _cells;
    private readonly List<Ship> _ships = new();

    public int Size { get; }
    public IReadOnlyList<Ship> Ships => _ships;

    public Board(int size = DefaultSize)
    {
        if (size <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Board size must be positive.");
        }

        Size = size;
        _cells = new Cell[size, size];
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                _cells[x, y] = new Cell(new Coordinate(x, y));
            }
        }
    }

    public Cell this[Coordinate c] => _cells[c.X, c.Y];
    public Cell this[int x, int y] => _cells[x, y];

    public bool InBounds(Coordinate c) =>
        c.X >= 0 && c.X < Size && c.Y >= 0 && c.Y < Size;

    internal void AddShip(Ship ship)
    {
        _ships.Add(ship);
        foreach (var coord in ship.Coordinates)
        {
            var cell = _cells[coord.X, coord.Y];
            cell.State = CellState.Ship;
            cell.Ship = ship;
        }
    }

    public bool AllShipsSunk => _ships.Count > 0 && _ships.All(s => s.IsSunk);
}
