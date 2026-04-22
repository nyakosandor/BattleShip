using BattleShip.Core.Domain;
using BattleShip.Core.Services;
using Xunit;

namespace BattleShip.Tests;

public class FleetRandomizerTests
{
    [Fact]
    public void AutoDeploy_OnEmptyBoard_PlacesFullStandardFleet()
    {
        var engine = new GameEngine();
        var randomizer = new FleetRandomizer(engine, new Random(1));
        var board = new Board();

        var ok = randomizer.AutoDeploy(board);

        Assert.True(ok);
        Assert.Equal(Fleet.StandardComposition.Count, board.Ships.Count);
        foreach (var type in Fleet.StandardComposition)
        {
            Assert.Contains(board.Ships, s => s.Type == type);
        }
    }

    [Fact]
    public void AutoDeploy_OnlyFillsMissingShips()
    {
        var engine = new GameEngine();
        var randomizer = new FleetRandomizer(engine, new Random(7));
        var board = new Board();

        Assert.True(engine.TryPlaceShip(board, ShipType.Carrier, new Coordinate(0, 0), Orientation.Horizontal).Success);

        var ok = randomizer.AutoDeploy(board);

        Assert.True(ok);
        Assert.Equal(Fleet.StandardComposition.Count, board.Ships.Count);

        var carrier = board.Ships.Single(s => s.Type == ShipType.Carrier);
        Assert.Equal(Orientation.Horizontal, carrier.Orientation);
        Assert.Equal(new Coordinate(0, 0), carrier.Coordinates[0]);
    }

    [Fact]
    public void AutoDeploy_ProducesNonOverlappingInBoundsPlacement()
    {
        var engine = new GameEngine();

        for (int seed = 0; seed < 25; seed++)
        {
            var randomizer = new FleetRandomizer(engine, new Random(seed));
            var board = new Board();

            Assert.True(randomizer.AutoDeploy(board));

            var occupied = new HashSet<Coordinate>();
            foreach (var ship in board.Ships)
            {
                foreach (var coord in ship.Coordinates)
                {
                    Assert.True(board.InBounds(coord));
                    Assert.True(occupied.Add(coord), $"Seed {seed}: overlap at {coord}.");
                }
            }
        }
    }
}
