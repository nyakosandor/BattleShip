using BattleShip.Core.Domain;
using BattleShip.Core.Services;
using BattleShip.LanServer;
using BattleShip.LanServer.Contracts;
using Xunit;

namespace BattleShip.Tests;

public class LanGameSessionForfeitTests
{
    private static IReadOnlyList<PlacementDto> BuildStandardPlacements() => new[]
    {
        new PlacementDto(ShipType.Carrier, new CoordinateDto(0, 0), Orientation.Horizontal),
        new PlacementDto(ShipType.Battleship, new CoordinateDto(0, 1), Orientation.Horizontal),
        new PlacementDto(ShipType.Cruiser, new CoordinateDto(0, 2), Orientation.Horizontal),
        new PlacementDto(ShipType.Submarine, new CoordinateDto(0, 3), Orientation.Horizontal),
        new PlacementDto(ShipType.Destroyer, new CoordinateDto(0, 4), Orientation.Horizontal),
    };

    /// <summary>Test clock that advances only when the test tells it to.</summary>
    private sealed class FakeClock
    {
        public DateTimeOffset Now { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public DateTimeOffset Read() => Now;
        public void Advance(TimeSpan delta) => Now += delta;
    }

    private static (LanGameSession session, FakeClock clock, ConnectResponse host, ConnectResponse guest)
        BuildInProgressSession(TimeSpan? disconnectTimeout = null)
    {
        var engine = new GameEngine();
        var randomizer = new FleetRandomizer(engine, new Random(1));
        var hostBoard = new Board();
        Assert.True(randomizer.AutoDeploy(hostBoard));

        var clock = new FakeClock();
        var session = new LanGameSession(engine, clock.Read, disconnectTimeout ?? TimeSpan.FromSeconds(5));
        var host = session.RegisterHost("Alice", hostBoard);
        var guest = session.RegisterGuest("Bob").Value!;
        Assert.True(session.Deploy(guest.Token, BuildStandardPlacements()).Success);

        Assert.Equal(GamePhase.InProgress, session.Phase);
        return (session, clock, host, guest);
    }

    [Fact]
    public void StateResponse_CarriesEndReasonNone_WhileMatchInProgress()
    {
        var (session, _, host, _) = BuildInProgressSession();

        var state = session.GetState(host.Token);

        Assert.Equal(GamePhase.InProgress, state.Phase);
        Assert.Equal(EndReason.None, state.EndReason);
        Assert.Null(state.Winner);
    }

    [Fact]
    public void FleetDestroyed_SetsEndReasonFleetDestroyed()
    {
        var (session, _, host, guest) = BuildInProgressSession();

        // Guest fleet pinned at known cells; host sinks them all (reuses LanGameSessionTests layout).
        var guestCells = new List<CoordinateDto>();
        for (int x = 0; x < 5; x++) guestCells.Add(new CoordinateDto(x, 0));
        for (int x = 0; x < 4; x++) guestCells.Add(new CoordinateDto(x, 1));
        for (int x = 0; x < 3; x++) guestCells.Add(new CoordinateDto(x, 2));
        for (int x = 0; x < 3; x++) guestCells.Add(new CoordinateDto(x, 3));
        for (int x = 0; x < 2; x++) guestCells.Add(new CoordinateDto(x, 4));

        var guestDummies = new Queue<CoordinateDto>();
        for (int y = 5; y < 10; y++)
            for (int x = 0; x < 10; x++)
                guestDummies.Enqueue(new CoordinateDto(x, y));

        foreach (var target in guestCells)
        {
            var fired = session.Fire(host.Token, target);
            if (fired.Value!.FleetDestroyed) break;
            Assert.True(session.Fire(guest.Token, guestDummies.Dequeue()).Success);
        }

        var state = session.GetState(host.Token);
        Assert.Equal(GamePhase.Finished, state.Phase);
        Assert.Equal(EndReason.FleetDestroyed, state.EndReason);
        Assert.Equal(PlayerRole.Host, state.Winner);
    }

    [Fact]
    public void GuestSilent_PastTimeout_HostPoll_AwardsHostForfeit()
    {
        var (session, clock, host, _) = BuildInProgressSession();

        // Both players polled implicitly at t0 via setup. Advance time, then host polls again.
        clock.Advance(TimeSpan.FromSeconds(5.1));
        var state = session.GetState(host.Token);

        Assert.Equal(GamePhase.Finished, state.Phase);
        Assert.Equal(EndReason.Forfeit, state.EndReason);
        Assert.Equal(PlayerRole.Host, state.Winner);
    }

    [Fact]
    public void HostSilent_PastTimeout_GuestPoll_AwardsGuestForfeit()
    {
        var (session, clock, _, guest) = BuildInProgressSession();

        clock.Advance(TimeSpan.FromSeconds(5.1));
        var state = session.GetState(guest.Token);

        Assert.Equal(GamePhase.Finished, state.Phase);
        Assert.Equal(EndReason.Forfeit, state.EndReason);
        Assert.Equal(PlayerRole.Guest, state.Winner);
    }

    [Fact]
    public void Forfeit_IsNotTriggered_DuringDeployingPhase()
    {
        var engine = new GameEngine();
        var randomizer = new FleetRandomizer(engine, new Random(1));
        var hostBoard = new Board();
        Assert.True(randomizer.AutoDeploy(hostBoard));

        var clock = new FakeClock();
        var session = new LanGameSession(engine, clock.Read, TimeSpan.FromSeconds(5));
        var host = session.RegisterHost("Alice", hostBoard);
        session.RegisterGuest("Bob");
        Assert.Equal(GamePhase.Deploying, session.Phase);

        clock.Advance(TimeSpan.FromSeconds(60));
        var state = session.GetState(host.Token);

        Assert.Equal(GamePhase.Deploying, state.Phase);
        Assert.Equal(EndReason.None, state.EndReason);
    }

    [Fact]
    public void Poll_WithinTimeout_KeepsOpponentAlive()
    {
        var (session, clock, host, guest) = BuildInProgressSession();

        // Both sides ping every 4 seconds for 20 seconds; nobody should forfeit.
        for (int i = 0; i < 5; i++)
        {
            clock.Advance(TimeSpan.FromSeconds(4));
            Assert.Equal(GamePhase.InProgress, session.GetState(host.Token).Phase);
            Assert.Equal(GamePhase.InProgress, session.GetState(guest.Token).Phase);
        }
    }
}
