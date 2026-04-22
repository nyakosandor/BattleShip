using BattleShip.Core.Domain;

namespace BattleShip.Core.Services;

public interface IGameEngine
{
    IReadOnlyList<Coordinate> BuildShipCoordinates(Coordinate origin, ShipType type, Orientation orientation);

    /// <summary>
    /// Check whether a ship can legally be placed at the given origin/orientation without mutating the board.
    /// </summary>
    PlacementResult ValidatePlacement(Board board, ShipType type, Coordinate origin, Orientation orientation);

    PlacementResult TryPlaceShip(Board board, ShipType type, Coordinate origin, Orientation orientation);

    ShotResult ProcessShot(Board targetBoard, Coordinate coordinate);

    bool IsFleetDestroyed(Player player);
}
