using BattleShip.Core.Domain;
using BattleShip.Core.Services;
using BattleShip.LanServer;
using BattleShip.LanServer.Contracts;
using Xunit;

namespace BattleShip.Tests;

public class LanGameSessionTests
{
    private static (LanGameSession session, GameEngine engine, Board hostBoard, ConnectResponse hostToken)
        BuildSessionWithHost(int randomSeed = 1)
    {
        var engine = new GameEngine();
        var randomizer = new FleetRandomizer(engine, new Random(randomSeed));
        var hostBoard = new Board();
        Assert.True(randomizer.AutoDeploy(hostBoard));

        var session = new LanGameSession(engine);
        var hostInfo = session.RegisterHost("Alice", hostBoard);
        return (session, engine, hostBoard, hostInfo);
    }

    private static IReadOnlyList<PlacementDto> BuildStandardPlacements()
    {
        // Pack the standard fleet along the left columns, one ship per row.
        return new[]
        {
            new PlacementDto(ShipType.Carrier, new CoordinateDto(0, 0), Orientation.Horizontal),
            new PlacementDto(ShipType.Battleship, new CoordinateDto(0, 1), Orientation.Horizontal),
            new PlacementDto(ShipType.Cruiser, new CoordinateDto(0, 2), Orientation.Horizontal),
            new PlacementDto(ShipType.Submarine, new CoordinateDto(0, 3), Orientation.Horizontal),
            new PlacementDto(ShipType.Destroyer, new CoordinateDto(0, 4), Orientation.Horizontal),
        };
    }

    // ---------- Connect ----------

    [Fact]
    public void RegisterHost_StartsInWaitingForClient()
    {
        var (session, _, _, _) = BuildSessionWithHost();

        Assert.Equal(GamePhase.WaitingForClient, session.Phase);
    }

    [Fact]
    public void RegisterGuest_MovesToDeployingPhase()
    {
        var (session, _, _, _) = BuildSessionWithHost();

        var guest = session.RegisterGuest("Bob");

        Assert.True(guest.Success);
        Assert.Equal(PlayerRole.Guest, guest.Value!.Role);
        Assert.Equal(GamePhase.Deploying, session.Phase);
    }

    [Fact]
    public void RegisterGuest_WhenFull_Returns409()
    {
        var (session, _, _, _) = BuildSessionWithHost();
        session.RegisterGuest("Bob");

        var second = session.RegisterGuest("Mallory");

        Assert.False(second.Success);
        Assert.Equal(ActionErrorCodes.SessionFull, second.ErrorCode);
    }

    [Fact]
    public void GetState_HidesOpponentFleet()
    {
        var (session, _, _, host) = BuildSessionWithHost();
        var guest = session.RegisterGuest("Bob").Value!;

        var hostView = session.GetState(host.Token);

        Assert.NotNull(hostView.Self);
        Assert.Equal(host.PlayerId, hostView.Self!.PlayerId);
        Assert.Equal(5, hostView.Self.Fleet.Count);

        // Public views never carry ship coordinates that aren't sunk.
        Assert.NotNull(hostView.Guest);
        Assert.Empty(hostView.Guest!.SunkShips);

        var guestView = session.GetState(guest.Token);
        Assert.Equal(guest.PlayerId, guestView.Self!.PlayerId);

        // Guest's self view must NOT be able to see host fleet coordinates.
        Assert.NotNull(guestView.Host);
        Assert.Empty(guestView.Host!.SunkShips);
    }

    // ---------- Deploy ----------

    [Fact]
    public void Deploy_WithoutToken_ReturnsUnauthorized()
    {
        var (session, _, _, _) = BuildSessionWithHost();
        session.RegisterGuest("Bob");

        var result = session.Deploy(token: "not-a-real-token", BuildStandardPlacements());

        Assert.False(result.Success);
        Assert.Equal(ActionErrorCodes.Unauthorized, result.ErrorCode);
    }

    [Fact]
    public void Deploy_BeforeGuestJoined_FailsInvalidPhase()
    {
        var (session, _, _, host) = BuildSessionWithHost();

        // Host is already deployed; attempt re-deploy should be rejected because phase is WaitingForClient.
        var result = session.Deploy(host.Token, BuildStandardPlacements());

        Assert.False(result.Success);
        Assert.Equal(ActionErrorCodes.InvalidPhase, result.ErrorCode);
    }

    [Fact]
    public void Deploy_WithFewerThanFiveShips_Rejected()
    {
        var (session, _, _, _) = BuildSessionWithHost();
        var guest = session.RegisterGuest("Bob").Value!;

        var partial = BuildStandardPlacements().Take(4).ToList();

        var result = session.Deploy(guest.Token, partial);

        Assert.False(result.Success);
        Assert.Equal(ActionErrorCodes.InvalidFleet, result.ErrorCode);
    }

    [Fact]
    public void Deploy_WithOverlappingPlacements_Rejected()
    {
        var (session, _, _, _) = BuildSessionWithHost();
        var guest = session.RegisterGuest("Bob").Value!;

        var overlapping = new[]
        {
            new PlacementDto(ShipType.Carrier, new CoordinateDto(0, 0), Orientation.Horizontal),
            new PlacementDto(ShipType.Battleship, new CoordinateDto(0, 0), Orientation.Horizontal), // overlaps
            new PlacementDto(ShipType.Cruiser, new CoordinateDto(0, 2), Orientation.Horizontal),
            new PlacementDto(ShipType.Submarine, new CoordinateDto(0, 3), Orientation.Horizontal),
            new PlacementDto(ShipType.Destroyer, new CoordinateDto(0, 4), Orientation.Horizontal),
        };

        var result = session.Deploy(guest.Token, overlapping);

        Assert.False(result.Success);
        Assert.Equal(ActionErrorCodes.InvalidFleet, result.ErrorCode);
    }

    [Fact]
    public void Deploy_WithOutOfBoundsPlacement_Rejected()
    {
        var (session, _, _, _) = BuildSessionWithHost();
        var guest = session.RegisterGuest("Bob").Value!;

        var oob = new[]
        {
            new PlacementDto(ShipType.Carrier, new CoordinateDto(8, 0), Orientation.Horizontal), // off the right edge
            new PlacementDto(ShipType.Battleship, new CoordinateDto(0, 1), Orientation.Horizontal),
            new PlacementDto(ShipType.Cruiser, new CoordinateDto(0, 2), Orientation.Horizontal),
            new PlacementDto(ShipType.Submarine, new CoordinateDto(0, 3), Orientation.Horizontal),
            new PlacementDto(ShipType.Destroyer, new CoordinateDto(0, 4), Orientation.Horizontal),
        };

        var result = session.Deploy(guest.Token, oob);

        Assert.False(result.Success);
        Assert.Equal(ActionErrorCodes.InvalidFleet, result.ErrorCode);
    }

    [Fact]
    public void Deploy_Twice_Rejected()
    {
        var (session, _, _, _) = BuildSessionWithHost();
        var guest = session.RegisterGuest("Bob").Value!;

        Assert.True(session.Deploy(guest.Token, BuildStandardPlacements()).Success);
        var second = session.Deploy(guest.Token, BuildStandardPlacements());

        Assert.False(second.Success);
        // After guest deploys, phase transitions to InProgress, so the second call fails on InvalidPhase.
        Assert.Equal(ActionErrorCodes.InvalidPhase, second.ErrorCode);
    }

    [Fact]
    public void Deploy_WhenGuestFinishes_TransitionsToInProgress()
    {
        var (session, _, _, _) = BuildSessionWithHost();
        var guest = session.RegisterGuest("Bob").Value!;

        var result = session.Deploy(guest.Token, BuildStandardPlacements());

        Assert.True(result.Success);
        Assert.Equal(GamePhase.InProgress, session.Phase);
        Assert.Equal(PlayerRole.Host, result.Value!.Turn);
    }

    // ---------- Fire ----------

    [Fact]
    public void Fire_BeforeInProgress_Rejected()
    {
        var (session, _, _, host) = BuildSessionWithHost();

        var result = session.Fire(host.Token, new CoordinateDto(0, 0));

        Assert.False(result.Success);
        Assert.Equal(ActionErrorCodes.InvalidPhase, result.ErrorCode);
    }

    [Fact]
    public void Fire_OutOfTurn_Rejected()
    {
        var (session, _, _, host) = BuildSessionWithHost();
        var guest = session.RegisterGuest("Bob").Value!;
        Assert.True(session.Deploy(guest.Token, BuildStandardPlacements()).Success);

        // Host has the first turn; guest should be blocked.
        var result = session.Fire(guest.Token, new CoordinateDto(0, 0));

        Assert.False(result.Success);
        Assert.Equal(ActionErrorCodes.InvalidTurn, result.ErrorCode);
    }

    [Fact]
    public void Fire_Twice_SecondShotBlockedBecauseTurnSwitched()
    {
        var (session, _, _, host) = BuildSessionWithHost();
        var guest = session.RegisterGuest("Bob").Value!;
        Assert.True(session.Deploy(guest.Token, BuildStandardPlacements()).Success);

        // Host fires at (9,9); unlikely to be a ship with the chosen layout.
        var first = session.Fire(host.Token, new CoordinateDto(9, 9));
        Assert.True(first.Success);

        var second = session.Fire(host.Token, new CoordinateDto(9, 8));
        Assert.False(second.Success);
        Assert.Equal(ActionErrorCodes.InvalidTurn, second.ErrorCode);
    }

    [Fact]
    public void Fire_AlreadyShotCell_Rejected()
    {
        var (session, _, _, host) = BuildSessionWithHost();
        var guest = session.RegisterGuest("Bob").Value!;
        Assert.True(session.Deploy(guest.Token, BuildStandardPlacements()).Success);

        Assert.True(session.Fire(host.Token, new CoordinateDto(9, 9)).Success);
        Assert.True(session.Fire(guest.Token, new CoordinateDto(5, 5)).Success);

        var repeat = session.Fire(host.Token, new CoordinateDto(9, 9));
        Assert.False(repeat.Success);
        Assert.Equal(ActionErrorCodes.InvalidShot, repeat.ErrorCode);
    }

    [Fact]
    public void Fire_OutOfBounds_Rejected()
    {
        var (session, _, _, host) = BuildSessionWithHost();
        var guest = session.RegisterGuest("Bob").Value!;
        Assert.True(session.Deploy(guest.Token, BuildStandardPlacements()).Success);

        var result = session.Fire(host.Token, new CoordinateDto(10, 0));

        Assert.False(result.Success);
        Assert.Equal(ActionErrorCodes.InvalidShot, result.ErrorCode);
    }

    [Fact]
    public void Fire_SinksGuestFleet_EndsGameWithHostWinner()
    {
        var (session, _, _, host) = BuildSessionWithHost();
        var guest = session.RegisterGuest("Bob").Value!;
        Assert.True(session.Deploy(guest.Token, BuildStandardPlacements()).Success);

        // Guest fleet occupies (0,0)-(4,0), (0,1)-(3,1), (0,2)-(2,2), (0,3)-(2,3), (0,4)-(1,4) = 17 cells.
        // Host alternates turns with guest to knock out every guest cell.
        var guestCells = new List<CoordinateDto>();
        for (int x = 0; x < 5; x++) guestCells.Add(new CoordinateDto(x, 0));
        for (int x = 0; x < 4; x++) guestCells.Add(new CoordinateDto(x, 1));
        for (int x = 0; x < 3; x++) guestCells.Add(new CoordinateDto(x, 2));
        for (int x = 0; x < 3; x++) guestCells.Add(new CoordinateDto(x, 3));
        for (int x = 0; x < 2; x++) guestCells.Add(new CoordinateDto(x, 4));

        // Pick guest-side dummy targets far from its own fleet (the guest fires at host's board).
        var guestDummies = new Queue<CoordinateDto>();
        for (int y = 5; y < 10; y++)
            for (int x = 0; x < 10; x++)
                guestDummies.Enqueue(new CoordinateDto(x, y));

        FireResponse? last = null;
        foreach (var target in guestCells)
        {
            var hostShot = session.Fire(host.Token, target);
            Assert.True(hostShot.Success, $"Host shot at {target.X},{target.Y} failed: {hostShot.Message}");
            last = hostShot.Value;
            if (last!.FleetDestroyed) break;

            // Guest takes a throw-away shot somewhere host fleet definitely isn't.
            var dummy = guestDummies.Dequeue();
            Assert.True(session.Fire(guest.Token, dummy).Success);
        }

        Assert.NotNull(last);
        Assert.True(last!.FleetDestroyed);
        Assert.Equal(GamePhase.Finished, session.Phase);

        // After finish, additional fires are rejected.
        var afterFinish = session.Fire(host.Token, new CoordinateDto(0, 9));
        Assert.False(afterFinish.Success);
        Assert.Equal(ActionErrorCodes.GameFinished, afterFinish.ErrorCode);
    }
}
