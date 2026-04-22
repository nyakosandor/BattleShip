using BattleShip.Core.Domain;

namespace BattleShip.Core.Services;

public sealed class GameEngine : IGameEngine
{
    public IReadOnlyList<Coordinate> BuildShipCoordinates(
        Coordinate origin,
        ShipType type,
        Orientation orientation)
    {
        int length = type.Length();
        var coords = new Coordinate[length];
        for (int i = 0; i < length; i++)
        {
            coords[i] = orientation == Orientation.Horizontal
                ? new Coordinate(origin.X + i, origin.Y)
                : new Coordinate(origin.X, origin.Y + i);
        }
        return coords;
    }

    public PlacementResult ValidatePlacement(
        Board board,
        ShipType type,
        Coordinate origin,
        Orientation orientation)
    {
        ArgumentNullException.ThrowIfNull(board);

        if (board.Ships.Any(s => s.Type == type))
        {
            return PlacementResult.Fail(PlacementError.DuplicateShipType);
        }

        var coords = BuildShipCoordinates(origin, type, orientation);

        foreach (var c in coords)
        {
            if (!board.InBounds(c))
            {
                return PlacementResult.Fail(PlacementError.OutOfBounds);
            }
        }

        foreach (var c in coords)
        {
            if (board[c].HasShip)
            {
                return PlacementResult.Fail(PlacementError.Overlaps);
            }
        }

        return PlacementResult.Ok();
    }

    public PlacementResult TryPlaceShip(
        Board board,
        ShipType type,
        Coordinate origin,
        Orientation orientation)
    {
        var validation = ValidatePlacement(board, type, origin, orientation);
        if (!validation.Success)
        {
            return validation;
        }

        var coords = BuildShipCoordinates(origin, type, orientation);
        var ship = new Ship(type, orientation, coords);
        board.AddShip(ship);
        return PlacementResult.Ok();
    }

    public ShotResult ProcessShot(Board targetBoard, Coordinate coordinate)
    {
        ArgumentNullException.ThrowIfNull(targetBoard);

        if (!targetBoard.InBounds(coordinate))
        {
            return new ShotResult(
                ShotOutcome.Invalid,
                ShotError.OutOfBounds,
                coordinate,
                ShipHit: null,
                FleetDestroyed: false);
        }

        var cell = targetBoard[coordinate];

        if (cell.State is CellState.Hit or CellState.Miss)
        {
            return new ShotResult(
                ShotOutcome.Invalid,
                ShotError.AlreadyShot,
                coordinate,
                ShipHit: cell.Ship,
                FleetDestroyed: false);
        }

        if (cell.Ship is Ship ship)
        {
            ship.RegisterHit(coordinate);
            cell.State = CellState.Hit;
            var outcome = ship.IsSunk ? ShotOutcome.Sunk : ShotOutcome.Hit;
            return new ShotResult(
                outcome,
                ShotError.None,
                coordinate,
                ship,
                FleetDestroyed: targetBoard.AllShipsSunk);
        }

        cell.State = CellState.Miss;
        return new ShotResult(
            ShotOutcome.Miss,
            ShotError.None,
            coordinate,
            ShipHit: null,
            FleetDestroyed: false);
    }

    public bool IsFleetDestroyed(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return player.Board.AllShipsSunk;
    }
}
