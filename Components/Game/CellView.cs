using BattleShip.Core.Domain;

namespace BattleShip.App.Components.Game;

/// <summary>
/// Which visual treatment a board cell should receive, independent of the underlying
/// <see cref="Cell.State"/>. Grids compose this from board state + any preview overlay.
/// </summary>
public enum CellView
{
    Empty,
    Ship,
    Hit,
    Miss,
    PreviewValid,
    PreviewInvalid,
}
