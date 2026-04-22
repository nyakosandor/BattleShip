namespace BattleShip.Core.Domain;

public sealed class Player
{
    public Guid Id { get; }
    public string Name { get; }
    public Board Board { get; }

    /// <summary>
    /// Convenience view of the player's ships. Alias for <see cref="Domain.Board.Ships"/>.
    /// </summary>
    public IReadOnlyList<Ship> Fleet => Board.Ships;

    public Player(string name, Board? board = null, Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Player name is required.", nameof(name));
        }

        Id = id ?? Guid.NewGuid();
        Name = name;
        Board = board ?? new Board();
    }

    public bool HasLost => Board.AllShipsSunk;
}
