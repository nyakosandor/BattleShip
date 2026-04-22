using BattleShip.Core.Domain;

namespace BattleShip.App.Services;

/// <summary>
/// Tiny in-process hand-off state between the Setup page and the Play page.
/// Setup writes the deployed player board here when the user presses Ready;
/// Play takes it (clearing the slot) and spins up a game.
/// </summary>
public sealed class GameHandoff
{
    private Board? _pendingBoard;

    public bool HasPendingBoard => _pendingBoard is not null;

    public void Submit(Board board)
    {
        ArgumentNullException.ThrowIfNull(board);
        _pendingBoard = board;
    }

    public Board? Take()
    {
        var b = _pendingBoard;
        _pendingBoard = null;
        return b;
    }

    public void Clear() => _pendingBoard = null;
}
