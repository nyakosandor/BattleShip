using BattleShip.Core.Domain;
using BattleShip.Core.Services;
using Xunit;

namespace BattleShip.Tests;

public class HuntTargetAiTests
{
    private static ShotResult MakeResult(ShotOutcome outcome, Coordinate coord) =>
        new(outcome, ShotError.None, coord, ShipHit: null, FleetDestroyed: false);

    [Fact]
    public void StartsInHuntMode()
    {
        var ai = new HuntTargetAi(rng: new Random(1));

        Assert.Equal(AiMode.Hunt, ai.Mode);
    }

    [Fact]
    public void NextShot_NeverRepeatsCoordinates()
    {
        var ai = new HuntTargetAi(rng: new Random(42));
        var seen = new HashSet<Coordinate>();

        for (int i = 0; i < 100; i++)
        {
            var shot = ai.NextShot();
            ai.RegisterResult(MakeResult(ShotOutcome.Miss, shot));
            Assert.True(seen.Add(shot), $"AI repeated coordinate {shot} on turn {i}.");
        }

        Assert.Equal(100, seen.Count);
    }

    [Fact]
    public void OnHit_SwitchesToTargetMode_AndQueuesOrthogonalNeighbors()
    {
        var ai = new HuntTargetAi(rng: new Random(3));
        var firstShot = ai.NextShot();
        ai.RegisterResult(MakeResult(ShotOutcome.Hit, firstShot));

        Assert.Equal(AiMode.Target, ai.Mode);

        var expectedNeighbors = new HashSet<Coordinate>
        {
            new(firstShot.X - 1, firstShot.Y),
            new(firstShot.X + 1, firstShot.Y),
            new(firstShot.X, firstShot.Y - 1),
            new(firstShot.X, firstShot.Y + 1),
        };
        expectedNeighbors.RemoveWhere(c => c.X < 0 || c.X >= 10 || c.Y < 0 || c.Y >= 10);

        var queued = new HashSet<Coordinate>();
        for (int i = 0; i < expectedNeighbors.Count; i++)
        {
            var next = ai.NextShot();
            queued.Add(next);
            ai.RegisterResult(MakeResult(ShotOutcome.Miss, next));
        }

        Assert.Equal(expectedNeighbors, queued);
    }

    [Fact]
    public void OnSunk_ClearsTargetQueue_AndReturnsToHuntMode()
    {
        var ai = new HuntTargetAi(rng: new Random(5));

        var first = ai.NextShot();
        ai.RegisterResult(MakeResult(ShotOutcome.Hit, first));
        Assert.Equal(AiMode.Target, ai.Mode);

        var second = ai.NextShot();
        ai.RegisterResult(MakeResult(ShotOutcome.Sunk, second));

        Assert.Equal(AiMode.Hunt, ai.Mode);
    }

    [Fact]
    public void TargetQueue_ExcludesAlreadyShotCells()
    {
        var ai = new HuntTargetAi(rng: new Random(11));

        // Force a known starting point by taking shots until we hit the middle of the board.
        // Easier: construct state directly through RegisterResult against a synthesised hit.
        var center = new Coordinate(5, 5);

        // Simulate the AI shooting (5,5) by briefly asking for a shot and registering a hit at center.
        // We accept whatever NextShot returns, but we need RegisterResult to consume `center`.
        // To keep the test self-contained we don't rely on the hunt result: we directly call
        // RegisterResult with the Hit outcome at (5,5) which is enough to enqueue its neighbors.
        ai.RegisterResult(MakeResult(ShotOutcome.Hit, center));

        // Now (5,5) is NOT in the shot set (we never called NextShot for it), so in principle the
        // AI could re-select it. In practice hunt/target both skip visited cells; let's verify the
        // four neighbors are produced without duplicates.
        var produced = new List<Coordinate>();
        for (int i = 0; i < 4; i++)
        {
            var shot = ai.NextShot();
            produced.Add(shot);
            ai.RegisterResult(MakeResult(ShotOutcome.Miss, shot));
        }

        Assert.Equal(4, produced.Distinct().Count());
        Assert.All(produced, c =>
        {
            var dx = Math.Abs(c.X - center.X);
            var dy = Math.Abs(c.Y - center.Y);
            Assert.Equal(1, dx + dy);
        });
    }

    [Fact]
    public void AiCanFinishAGame_AgainstAPlantedFleet()
    {
        var engine = new GameEngine();
        var randomizer = new FleetRandomizer(engine, new Random(19));
        var board = new Board();
        Assert.True(randomizer.AutoDeploy(board));

        var ai = new HuntTargetAi(rng: new Random(19));

        int maxTurns = board.Size * board.Size;
        for (int turn = 0; turn < maxTurns; turn++)
        {
            var shot = ai.NextShot();
            var result = engine.ProcessShot(board, shot);
            Assert.NotEqual(ShotOutcome.Invalid, result.Outcome);
            ai.RegisterResult(result);

            if (result.FleetDestroyed)
            {
                return;
            }
        }

        Assert.Fail("AI did not finish the game within the maximum number of turns.");
    }
}
