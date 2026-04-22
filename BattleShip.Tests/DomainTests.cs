using BattleShip.Core.Domain;
using Xunit;

namespace BattleShip.Tests;

public class DomainTests
{
    [Fact]
    public void Board_DefaultSize_Is10x10()
    {
        var board = new Board();

        Assert.Equal(10, board.Size);
        Assert.Equal(CellState.Empty, board[0, 0].State);
        Assert.Equal(CellState.Empty, board[9, 9].State);
    }

    [Fact]
    public void Board_InBounds_ValidatesEdges()
    {
        var board = new Board();

        Assert.True(board.InBounds(new Coordinate(0, 0)));
        Assert.True(board.InBounds(new Coordinate(9, 9)));
        Assert.False(board.InBounds(new Coordinate(10, 0)));
        Assert.False(board.InBounds(new Coordinate(0, -1)));
    }

    [Fact]
    public void Board_AllShipsSunk_FalseWhenNoShips()
    {
        var board = new Board();

        Assert.False(board.AllShipsSunk);
    }

    [Fact]
    public void Ship_Ctor_RequiresCorrectCoordinateCount()
    {
        var tooFew = new[] { new Coordinate(0, 0) };

        Assert.Throws<ArgumentException>(() =>
            new Ship(ShipType.Destroyer, Orientation.Horizontal, tooFew));
    }

    [Fact]
    public void Ship_IsSunk_TrueOnlyAfterAllCellsHit()
    {
        var ship = new Ship(
            ShipType.Destroyer,
            Orientation.Horizontal,
            new[] { new Coordinate(0, 0), new Coordinate(1, 0) });

        Assert.False(ship.IsSunk);

        Assert.True(ship.RegisterHit(new Coordinate(0, 0)));
        Assert.False(ship.IsSunk);

        Assert.True(ship.RegisterHit(new Coordinate(1, 0)));
        Assert.True(ship.IsSunk);
    }

    [Fact]
    public void Ship_RegisterHit_IgnoresDuplicateAndForeignCoordinates()
    {
        var ship = new Ship(
            ShipType.Destroyer,
            Orientation.Horizontal,
            new[] { new Coordinate(0, 0), new Coordinate(1, 0) });

        Assert.True(ship.RegisterHit(new Coordinate(0, 0)));
        Assert.False(ship.RegisterHit(new Coordinate(0, 0)));
        Assert.False(ship.RegisterHit(new Coordinate(5, 5)));
    }

    [Fact]
    public void Player_DefaultsToFreshBoardAndId()
    {
        var player = new Player("Bob");

        Assert.NotEqual(Guid.Empty, player.Id);
        Assert.Equal("Bob", player.Name);
        Assert.Equal(10, player.Board.Size);
        Assert.Empty(player.Fleet);
    }

    [Fact]
    public void Player_EmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Player("  "));
    }
}
