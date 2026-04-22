using BattleShip.Core.Domain;
using BattleShip.LanServer.Contracts;

namespace BattleShip.LanServer;

public interface ILanHost : IAsyncDisposable
{
    bool IsRunning { get; }
    string? BaseAddress { get; }
    LanGameSession? Session { get; }

    event Action? StateChanged;

    /// <summary>
    /// Starts the local HTTP server and seeds the session with the host's pre-deployed fleet.
    /// </summary>
    Task StartAsync(string hostName, Board hostBoard, int port, CancellationToken ct = default);

    Task StopAsync();
}
