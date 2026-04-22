using BattleShip.Core.Domain;
using BattleShip.Core.Services;
using Xunit;

namespace BattleShip.Tests;

public class GameEngineTests
{
    private readonly GameEngine _engine = new();

    // ---------- Ship placement ----------

    [Fact]
    public void PlaceShip_HorizontalInBounds_Succeeds()
    {
        var board = new Board();

        var result = _engine.TryPlaceShip(board, ShipType.Destroyer, new Coordinate(0, 0), Orientation.Horizontal);

        Assert.True(result.Success);
        Assert.Equal(PlacementError.None, result.Error);
        Assert.Single(board.Ships);
        Assert.Equal(CellState.Ship, board[0, 0].State);
        Assert.Equal(CellState.Ship, board[1, 0].State);
    }

    [Fact]
    public void PlaceShip_VerticalInBounds_Succeeds()
    {
        var board = new Board();

        var result = _engine.TryPlaceShip(board, ShipType.Carrier, new Coordinate(3, 0), Orientation.Vertical);

        Assert.True(result.Success);
        for (int y = 0; y < 5; y++)
        {
            Assert.Equal(CellState.Ship, board[3, y].State);
        }
    }

    [Fact]
    public void PlaceShip_OffRightEdge_FailsOutOfBounds()
    {
        var board = new Board();

        var result = _engine.TryPlaceShip(board, ShipType.Battleship, new Coordinate(8, 0), Orientation.Horizontal);

        Assert.False(result.Success);
        Assert.Equal(PlacementError.OutOfBounds, result.Error);
        Assert.Empty(board.Ships);
    }

    [Fact]
    public void PlaceShip_OffBottomEdge_FailsOutOfBounds()
    {
        var board = new Board();

        var result = _engine.TryPlaceShip(board, ShipType.Cruiser, new Coordinate(0, 9), Orientation.Vertical);

        Assert.False(result.Success);
        Assert.Equal(PlacementError.OutOfBounds, result.Error);
    }

    [Fact]
    public void PlaceShip_NegativeOrigin_FailsOutOfBounds()
    {
        var board = new Board();

        var result = _engine.TryPlaceShip(board, ShipType.Destroyer, new Coordinate(-1, 0), Orientation.Horizontal);

        Assert.False(result.Success);
        Assert.Equal(PlacementError.OutOfBounds, result.Error);
    }

    [Fact]
    public void PlaceShip_OverlappingAnotherShip_FailsOverlap()
    {
        var board = new Board();
        Assert.True(_engine.TryPlaceShip(board, ShipType.Battleship, new Coordinate(2, 2), Orientation.Horizontal).Success);

        var result = _engine.TryPlaceShip(board, ShipType.Cruiser, new Coordinate(3, 0), Orientation.Vertical);

        Assert.False(result.Success);
        Assert.Equal(PlacementError.Overlaps, result.Error);
        Assert.Single(board.Ships);
    }

    [Fact]
    public void PlaceShip_SameTypeTwice_FailsDuplicate()
    {
        var board = new Board();
        Assert.True(_engine.TryPlaceShip(board, ShipType.Destroyer, new Coordinate(0, 0), Orientation.Horizontal).Success);

        var result = _engine.TryPlaceShip(board, ShipType.Destroyer, new Coordinate(0, 5), Orientation.Horizontal);

        Assert.False(result.Success);
        Assert.Equal(PlacementError.DuplicateShipType, result.Error);
    }

    [Fact]
    public void PlaceShip_AdjacentNotOverlapping_Succeeds()
    {
        var board = new Board();
        Assert.True(_engine.TryPlaceShip(board, ShipType.Destroyer, new Coordinate(0, 0), Orientation.Horizontal).Success);

        var result = _engine.TryPlaceShip(board, ShipType.Submarine, new Coordinate(0, 1), Orientation.Horizontal);

        Assert.True(result.Success);
    }

    [Fact]
    public void ValidatePlacement_DoesNotMutateBoard()
    {
        var board = new Board();

        var result = _engine.ValidatePlacement(board, ShipType.Carrier, new Coordinate(0, 0), Orientation.Horizontal);

        Assert.True(result.Success);
        Assert.Empty(board.Ships);
        Assert.Equal(CellState.Empty, board[0, 0].State);
    }

    [Fact]
    public void ValidatePlacement_ReportsOverlapWithoutMutating()
    {
        var board = new Board();
        _engine.TryPlaceShip(board, ShipType.Destroyer, new Coordinate(0, 0), Orientation.Horizontal);

        var result = _engine.ValidatePlacement(board, ShipType.Cruiser, new Coordinate(0, 0), Orientation.Horizontal);

        Assert.False(result.Success);
        Assert.Equal(PlacementError.Overlaps, result.Error);
        Assert.Single(board.Ships);
    }

    // ---------- Shot processing ----------

    [Fact]
    public void ProcessShot_OnEmptyCell_RegistersMiss()
    {
        var board = new Board();
        _engine.TryPlaceShip(board, ShipType.Destroyer, new Coordinate(0, 0), Orientation.Horizontal);

        var result = _engine.ProcessShot(board, new Coordinate(5, 5));

        Assert.Equal(ShotOutcome.Miss, result.Outcome);
        Assert.Equal(ShotError.None, result.Error);
        Assert.Equal(CellState.Miss, board[5, 5].State);
        Assert.False(result.FleetDestroyed);
    }

    [Fact]
    public void ProcessShot_OnShipCell_RegistersHit()
    {
        var board = new Board();
        _engine.TryPlaceShip(board, ShipType.Battleship, new Coordinate(2, 3), Orientation.Horizontal);

        var result = _engine.ProcessShot(board, new Coordinate(2, 3));

        Assert.Equal(ShotOutcome.Hit, result.Outcome);
        Assert.True(result.IsHit);
        Assert.Equal(CellState.Hit, board[2, 3].State);
        Assert.NotNull(result.ShipHit);
        Assert.False(result.ShipHit!.IsSunk);
    }

    [Fact]
    public void ProcessShot_SinksShip_WhenAllCellsHit()
    {
        var board = new Board();
        _engine.TryPlaceShip(board, ShipType.Destroyer, new Coordinate(0, 0), Orientation.Horizontal);

        _engine.ProcessShot(board, new Coordinate(0, 0));
        var finalShot = _engine.ProcessShot(board, new Coordinate(1, 0));

        Assert.Equal(ShotOutcome.Sunk, finalShot.Outcome);
        Assert.True(finalShot.ShipHit!.IsSunk);
    }

    [Fact]
    public void ProcessShot_OutOfBounds_ReturnsInvalid()
    {
        var board = new Board();

        var result = _engine.ProcessShot(board, new Coordinate(10, 0));

        Assert.Equal(ShotOutcome.Invalid, result.Outcome);
        Assert.Equal(ShotError.OutOfBounds, result.Error);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ProcessShot_OnAlreadyShotCell_ReturnsInvalid()
    {
        var board = new Board();
        _engine.TryPlaceShip(board, ShipType.Destroyer, new Coordinate(0, 0), Orientation.Horizontal);
        _engine.ProcessShot(board, new Coordinate(5, 5));

        var result = _engine.ProcessShot(board, new Coordinate(5, 5));

        Assert.Equal(ShotOutcome.Invalid, result.Outcome);
        Assert.Equal(ShotError.AlreadyShot, result.Error);
    }

    [Fact]
    public void ProcessShot_RepeatedHitOnSameCell_ReturnsInvalid_AndDoesNotDoubleCountDamage()
    {
        var board = new Board();
        _engine.TryPlaceShip(board, ShipType.Destroyer, new Coordinate(0, 0), Orientation.Horizontal);

        var first = _engine.ProcessShot(board, new Coordinate(0, 0));
        var second = _engine.ProcessShot(board, new Coordinate(0, 0));

        Assert.Equal(ShotOutcome.Hit, first.Outcome);
        Assert.Equal(ShotOutcome.Invalid, second.Outcome);
        Assert.Equal(ShotError.AlreadyShot, second.Error);
        Assert.False(first.ShipHit!.IsSunk);
    }

    // ---------- Win condition ----------

    [Fact]
    public void IsFleetDestroyed_EmptyFleet_ReturnsFalse()
    {
        var player = new Player("Alice");

        Assert.False(_engine.IsFleetDestroyed(player));
    }

    [Fact]
    public void IsFleetDestroyed_PartiallyDamagedFleet_ReturnsFalse()
    {
        var player = new Player("Alice");
        _engine.TryPlaceShip(player.Board, ShipType.Destroyer, new Coordinate(0, 0), Orientation.Horizontal);
        _engine.TryPlaceShip(player.Board, ShipType.Cruiser, new Coordinate(0, 5), Orientation.Horizontal);

        _engine.ProcessShot(player.Board, new Coordinate(0, 0));
        _engine.ProcessShot(player.Board, new Coordinate(1, 0));

        Assert.False(_engine.IsFleetDestroyed(player));
    }

    [Fact]
    public void IsFleetDestroyed_AllShipsSunk_ReturnsTrue()
    {
        var player = new Player("Alice");

        _engine.TryPlaceShip(player.Board, ShipType.Destroyer, new Coordinate(0, 0), Orientation.Horizontal);
        _engine.TryPlaceShip(player.Board, ShipType.Cruiser, new Coordinate(0, 2), Orientation.Horizontal);

        _engine.ProcessShot(player.Board, new Coordinate(0, 0));
        _engine.ProcessShot(player.Board, new Coordinate(1, 0));

        _engine.ProcessShot(player.Board, new Coordinate(0, 2));
        _engine.ProcessShot(player.Board, new Coordinate(1, 2));
        var finalShot = _engine.ProcessShot(player.Board, new Coordinate(2, 2));

        Assert.True(_engine.IsFleetDestroyed(player));
        Assert.True(player.HasLost);
        Assert.True(finalShot.FleetDestroyed);
    }

    // ---------- Coordinate helper ----------

    [Fact]
    public void BuildShipCoordinates_Horizontal_GeneratesContiguousRow()
    {
        var coords = _engine.BuildShipCoordinates(new Coordinate(2, 4), ShipType.Cruiser, Orientation.Horizontal);

        Assert.Equal(3, coords.Count);
        Assert.Equal(new Coordinate(2, 4), coords[0]);
        Assert.Equal(new Coordinate(3, 4), coords[1]);
        Assert.Equal(new Coordinate(4, 4), coords[2]);
    }

    [Fact]
    public void BuildShipCoordinates_Vertical_GeneratesContiguousColumn()
    {
        var coords = _engine.BuildShipCoordinates(new Coordinate(2, 4), ShipType.Cruiser, Orientation.Vertical);

        Assert.Equal(new Coordinate(2, 4), coords[0]);
        Assert.Equal(new Coordinate(2, 5), coords[1]);
        Assert.Equal(new Coordinate(2, 6), coords[2]);
    }
}
