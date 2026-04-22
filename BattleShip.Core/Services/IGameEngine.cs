using BattleShip.Core.Domain;

namespace BattleShip.Core.Services;

public interface IGameEngine
{
    IReadOnlyList<Coordinate> BuildShipCoordinates(Coordinate origin, ShipType type, Orientation orientation);

    PlacementResult TryPlaceShip(Board board, ShipType type, Coordinate origin, Orientation orientation);

    ShotResult ProcessShot(Board targetBoard, Coordinate coordinate);

    bool IsFleetDestroyed(Player player);
}
