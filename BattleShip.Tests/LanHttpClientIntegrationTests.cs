using System.Net;
using System.Net.Sockets;
using BattleShip.Core.Domain;
using BattleShip.Core.Services;
using BattleShip.LanServer;
using BattleShip.LanServer.Client;
using BattleShip.LanServer.Contracts;
using Xunit;

namespace BattleShip.Tests;

/// <summary>
/// Spins up a real <see cref="LanHost"/> on a loopback port and drives a full
/// connect/deploy/fire/win loop through <see cref="LanHttpClient"/>. This is the
/// integration backstop for Phase 5 — if these pass, two app instances on the
/// same network should behave the same way.
/// </summary>
public class LanHttpClientIntegrationTests
{
    private static int PickFreePort()
    {
        // HttpListener can't auto-assign, so lease a port from the OS via TcpListener
        // and release it immediately before the host binds. Race is acceptable for tests.
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task<(LanHost host, Uri baseUri)> StartHostAsync(string hostName = "Alice", int seed = 1)
    {
        var engine = new GameEngine();
        var randomizer = new FleetRandomizer(engine, new Random(seed));
        var board = new Board();
        Assert.True(randomizer.AutoDeploy(board));

        var host = new LanHost(engine);

        // Retry a couple of times if the port is stolen between pick and bind.
        Exception? last = null;
        for (int i = 0; i < 3; i++)
        {
            var port = PickFreePort();
            try
            {
                await host.StartAsync(hostName, board, port);
                // HttpListener prefix-matches literally against the Host header, so we must
                // hit it via the same authority (localhost) that LanHost binds to.
                return (host, new Uri($"http://localhost:{port}/"));
            }
            catch (Exception ex)
            {
                last = ex;
                await host.StopAsync();
            }
        }
        throw new InvalidOperationException("Could not bind LanHost to a loopback port.", last);
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
    public async Task Connect_ReturnsGuestIdentity()
    {
        var (host, baseUri) = await StartHostAsync();
        try
        {
            var result = await LanHttpClient.ConnectAsync(baseUri, "Bob");

            Assert.True(result.Success, result.Message);
            Assert.NotNull(result.Value);
            Assert.Equal(PlayerRole.Guest, result.Value!.Role);
            Assert.Equal("Bob", result.Value.PlayerName);
            await result.Value.DisposeAsync();
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task Connect_WhenHostNotRunning_ReturnsNetworkError()
    {
        var port = PickFreePort();
        var baseUri = new Uri($"http://localhost:{port}/");

        var result = await LanHttpClient.ConnectAsync(baseUri, "Bob");

        Assert.False(result.Success);
        Assert.True(result.NetworkError, "Expected network error when host isn't running.");
    }

    [Fact]
    public async Task Deploy_WithMissingShips_ReturnsInvalidFleet()
    {
        var (host, baseUri) = await StartHostAsync();
        try
        {
            var connect = await LanHttpClient.ConnectAsync(baseUri, "Bob");
            Assert.True(connect.Success);
            await using var guest = connect.Value!;

            var partial = BuildStandardPlacements().Take(3).ToList();
            var result = await guest.DeployAsync(partial);

            Assert.False(result.Success);
            Assert.Equal(ActionErrorCodes.InvalidFleet, result.ErrorCode);
            Assert.False(result.NetworkError);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task FullMatch_HostSinksGuestFleet_EndsWithFleetDestroyed()
    {
        var (host, baseUri) = await StartHostAsync();
        try
        {
            Assert.NotNull(host.HostIdentity);
            await using var hostClient = new LocalLanClient(host.Session!, host.HostIdentity!);

            var connect = await LanHttpClient.ConnectAsync(baseUri, "Bob");
            Assert.True(connect.Success);
            await using var guestClient = connect.Value!;

            var deploy = await guestClient.DeployAsync(BuildStandardPlacements());
            Assert.True(deploy.Success, deploy.Message);
            Assert.Equal(GamePhase.InProgress, deploy.Value!.Phase);

            // Guest fleet cells (packed along the top rows, see BuildStandardPlacements).
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

            FireResponse? last = null;
            foreach (var target in guestCells)
            {
                var shot = await hostClient.FireAsync(target);
                Assert.True(shot.Success, shot.Message);
                last = shot.Value;
                if (last!.FleetDestroyed) break;

                var dummy = guestDummies.Dequeue();
                var guestShot = await guestClient.FireAsync(dummy);
                Assert.True(guestShot.Success, guestShot.Message);
            }

            Assert.NotNull(last);
            Assert.True(last!.FleetDestroyed);

            var finalState = await guestClient.GetStateAsync();
            Assert.True(finalState.Success);
            Assert.Equal(GamePhase.Finished, finalState.Value!.Phase);
            Assert.Equal(EndReason.FleetDestroyed, finalState.Value.EndReason);
            Assert.Equal(PlayerRole.Host, finalState.Value.Winner);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task GetState_AfterHostStops_ReturnsNetworkError()
    {
        var (host, baseUri) = await StartHostAsync();
        var connect = await LanHttpClient.ConnectAsync(baseUri, "Bob");
        Assert.True(connect.Success);
        await using var guest = connect.Value!;

        // Confirm the client is alive while the host is running.
        var before = await guest.GetStateAsync();
        Assert.True(before.Success);

        await host.StopAsync();

        var after = await guest.GetStateAsync();
        Assert.False(after.Success);
        Assert.True(after.NetworkError);
    }
}
