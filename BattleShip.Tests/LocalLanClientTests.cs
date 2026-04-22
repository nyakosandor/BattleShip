using BattleShip.Core.Domain;
using BattleShip.Core.Services;
using BattleShip.LanServer;
using BattleShip.LanServer.Client;
using BattleShip.LanServer.Contracts;
using Xunit;

namespace BattleShip.Tests;

public class LocalLanClientTests
{
    private static (LanGameSession session, LocalLanClient hostClient) BuildHost()
    {
        var engine = new GameEngine();
        var randomizer = new FleetRandomizer(engine, new Random(1));
        var hostBoard = new Board();
        Assert.True(randomizer.AutoDeploy(hostBoard));

        var session = new LanGameSession(engine);
        var hostIdentity = session.RegisterHost("Alice", hostBoard);
        var client = new LocalLanClient(session, hostIdentity);
        return (session, client);
    }

    private static IReadOnlyList<PlacementDto> BuildStandardPlacements() => new[]
    {
        new PlacementDto(ShipType.Carrier, new CoordinateDto(0, 0), Orientation.Horizontal),
        new PlacementDto(ShipType.Battleship, new CoordinateDto(0, 1), Orientation.Horizontal),
        new PlacementDto(ShipType.Cruiser, new CoordinateDto(0, 2), Orientation.Horizontal),
        new PlacementDto(ShipType.Submarine, new CoordinateDto(0, 3), Orientation.Horizontal),
        new PlacementDto(ShipType.Destroyer, new CoordinateDto(0, 4), Orientation.Horizontal),
    };

    [Fact]
    public async Task HostClient_CarriesIdentityFromConnectResponse()
    {
        var (_, hostClient) = BuildHost();

        Assert.Equal(PlayerRole.Host, hostClient.Role);
        Assert.Equal("Alice", hostClient.PlayerName);
        Assert.False(string.IsNullOrEmpty(hostClient.Token));
        Assert.Equal("in-process", hostClient.BaseAddress);
        await hostClient.DisposeAsync();
    }

    [Fact]
    public async Task GetStateAsync_ReflectsSessionPhase()
    {
        var (session, hostClient) = BuildHost();

        var initial = await hostClient.GetStateAsync();
        Assert.True(initial.Success);
        Assert.Equal(GamePhase.WaitingForClient, initial.Value!.Phase);

        session.RegisterGuest("Bob");

        var afterJoin = await hostClient.GetStateAsync();
        Assert.True(afterJoin.Success);
        Assert.Equal(GamePhase.Deploying, afterJoin.Value!.Phase);
        await hostClient.DisposeAsync();
    }

    [Fact]
    public async Task GuestClientViaLocalAdapter_CanDeployAndFire()
    {
        var (session, hostClient) = BuildHost();
        var guestIdentity = session.RegisterGuest("Bob").Value!;
        await using var guestClient = new LocalLanClient(session, guestIdentity);

        var deploy = await guestClient.DeployAsync(BuildStandardPlacements());
        Assert.True(deploy.Success);
        Assert.Equal(GamePhase.InProgress, deploy.Value!.Phase);

        // Host fires first.
        var fire = await hostClient.FireAsync(new CoordinateDto(0, 0));
        Assert.True(fire.Success);
        Assert.Equal(ShotOutcomeDto.Hit, fire.Value!.Outcome);

        // Guest firing out of turn should be rejected but not crash.
        var outOfTurn = await hostClient.FireAsync(new CoordinateDto(1, 0));
        Assert.False(outOfTurn.Success);
        Assert.Equal(ActionErrorCodes.InvalidTurn, outOfTurn.ErrorCode);
        await hostClient.DisposeAsync();
    }

    [Fact]
    public async Task DeployAsync_SurfacingServerError_KeepsSameCode()
    {
        var (session, hostClient) = BuildHost();
        var guestIdentity = session.RegisterGuest("Bob").Value!;
        await using var guestClient = new LocalLanClient(session, guestIdentity);

        var tooFew = BuildStandardPlacements().Take(3).ToList();
        var result = await guestClient.DeployAsync(tooFew);

        Assert.False(result.Success);
        Assert.Equal(ActionErrorCodes.InvalidFleet, result.ErrorCode);
        Assert.False(result.NetworkError);
        await hostClient.DisposeAsync();
    }
}
