using BattleShip.Core.Domain;

namespace BattleShip.Core.Services;

/// <summary>
/// Classic "Hunt &amp; Target" opponent.
/// In Hunt mode it picks a random unshot cell. On a hit it enqueues the four orthogonal neighbors
/// and switches to Target mode until the hit chain is resolved (either the target queue drains or
/// a ship is sunk).
/// </summary>
public sealed class HuntTargetAi : IAiPlayer
{
    private readonly int _boardSize;
    private readonly Random _rng;
    private readonly HashSet<Coordinate> _shotSet = new();
    private readonly HashSet<Coordinate> _unshot = new();
    private readonly Queue<Coordinate> _targets = new();
    private readonly HashSet<Coordinate> _targetLookup = new();

    public HuntTargetAi(int boardSize = Board.DefaultSize, Random? rng = null)
    {
        if (boardSize <= 0) throw new ArgumentOutOfRangeException(nameof(boardSize));
        _boardSize = boardSize;
        _rng = rng ?? Random.Shared;

        for (int x = 0; x < boardSize; x++)
        {
            for (int y = 0; y < boardSize; y++)
            {
                _unshot.Add(new Coordinate(x, y));
            }
        }
    }

    public AiMode Mode => _targets.Count > 0 ? AiMode.Target : AiMode.Hunt;

    public Coordinate NextShot()
    {
        while (_targets.TryDequeue(out var queued))
        {
            _targetLookup.Remove(queued);
            if (_shotSet.Contains(queued)) continue;
            if (!InBounds(queued)) continue;
            return Commit(queued);
        }

        if (_unshot.Count == 0)
        {
            throw new InvalidOperationException("AI has no cells left to shoot.");
        }

        var index = _rng.Next(_unshot.Count);
        Coordinate chosen = default;
        int i = 0;
        foreach (var c in _unshot)
        {
            if (i++ == index)
            {
                chosen = c;
                break;
            }
        }

        return Commit(chosen);
    }

    public void RegisterResult(ShotResult result)
    {
        switch (result.Outcome)
        {
            case ShotOutcome.Hit:
                EnqueueAdjacents(result.Coordinate);
                break;
            case ShotOutcome.Sunk:
                // Classic simplification: assume sunk ship's neighbors were the current chain.
                // Drop any pending targets so we return to pure Hunt mode.
                _targets.Clear();
                _targetLookup.Clear();
                break;
            case ShotOutcome.Miss:
            case ShotOutcome.Invalid:
            default:
                break;
        }
    }

    private Coordinate Commit(Coordinate coordinate)
    {
        _shotSet.Add(coordinate);
        _unshot.Remove(coordinate);
        return coordinate;
    }

    private void EnqueueAdjacents(Coordinate center)
    {
        Span<Coordinate> neighbors =
        [
            new(center.X - 1, center.Y),
            new(center.X + 1, center.Y),
            new(center.X, center.Y - 1),
            new(center.X, center.Y + 1),
        ];

        foreach (var n in neighbors)
        {
            if (!InBounds(n)) continue;
            if (_shotSet.Contains(n)) continue;
            if (!_targetLookup.Add(n)) continue;
            _targets.Enqueue(n);
        }
    }

    private bool InBounds(Coordinate c) =>
        c.X >= 0 && c.X < _boardSize && c.Y >= 0 && c.Y < _boardSize;
}

public sealed class HuntTargetAiFactory : IAiPlayerFactory
{
    public IAiPlayer Create(int boardSize = Board.DefaultSize) => new HuntTargetAi(boardSize);
}
