using BattleShip.Core.Domain;

namespace BattleShip.Core.Services;

public enum AiMode
{
    Hunt,
    Target,
}

/// <summary>
/// A stateful AI shooter. A single instance corresponds to a single game.
/// </summary>
public interface IAiPlayer
{
    AiMode Mode { get; }

    /// <summary>
    /// Pick the next coordinate to fire at. The AI will never return the same coordinate twice.
    /// </summary>
    Coordinate NextShot();

    /// <summary>
    /// Feed the outcome of the last shot back into the AI so it can update its state.
    /// Call exactly once per <see cref="NextShot"/>.
    /// </summary>
    void RegisterResult(ShotResult result);
}

public interface IAiPlayerFactory
{
    IAiPlayer Create(int boardSize = Board.DefaultSize);
}
