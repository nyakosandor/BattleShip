using BattleShip.Core.Domain;

namespace BattleShip.Core.Services;

public sealed class FleetRandomizer : IFleetRandomizer
{
    private const int MaxAttemptsPerShip = 500;

    private readonly IGameEngine _engine;
    private readonly Random _rng;

    public FleetRandomizer(IGameEngine engine, Random? rng = null)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _rng = rng ?? Random.Shared;
    }

    public bool AutoDeploy(Board board)
    {
        ArgumentNullException.ThrowIfNull(board);

        var alreadyPlaced = board.Ships.Select(s => s.Type).ToHashSet();
        var missing = Fleet.StandardComposition.Where(t => !alreadyPlaced.Contains(t));

        foreach (var shipType in missing)
        {
            if (!TryPlaceRandom(board, shipType))
            {
                return false;
            }
        }

        return true;
    }

    private bool TryPlaceRandom(Board board, ShipType type)
    {
        for (int attempt = 0; attempt < MaxAttemptsPerShip; attempt++)
        {
            var orientation = _rng.Next(2) == 0 ? Orientation.Horizontal : Orientation.Vertical;
            var length = type.Length();

            int maxX = orientation == Orientation.Horizontal ? board.Size - length : board.Size - 1;
            int maxY = orientation == Orientation.Vertical ? board.Size - length : board.Size - 1;
            if (maxX < 0 || maxY < 0) continue;

            var origin = new Coordinate(_rng.Next(maxX + 1), _rng.Next(maxY + 1));

            if (_engine.TryPlaceShip(board, type, origin, orientation).Success)
            {
                return true;
            }
        }
        return false;
    }
}
