using BattleShip.Core.Domain;

namespace BattleShip.Core.Services;

public interface IFleetRandomizer
{
    /// <summary>
    /// Randomly place all ship types from <see cref="Fleet.StandardComposition"/> that are not yet on the board.
    /// Returns false only if placement fails after an exhaustive attempt (shouldn't happen on a 10x10 with the
    /// standard fleet, but the method is defensive).
    /// </summary>
    bool AutoDeploy(Board board);
}
