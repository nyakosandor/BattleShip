using BattleShip.Core.Domain;
using BattleShip.LanServer.Contracts;

namespace BattleShip.App.Services;

/// <summary>
/// Pure projection of a <see cref="StateResponse"/> into the coordinate sets
/// the match page renders. Built once per state tick so the UI doesn't
/// recompute these sets on every render.
/// </summary>
internal sealed record MatchStateProjection(
    StateResponse State,
    PlayerRole Role,
    PlayerPublicView? SelfPublic,
    PlayerPublicView? OpponentPublic,
    HashSet<Coordinate> SelfHits,
    HashSet<Coordinate> SelfMisses,
    HashSet<Coordinate> SelfShipCells,
    HashSet<Coordinate> OpponentHits,
    HashSet<Coordinate> OpponentMisses,
    HashSet<Coordinate> OpponentVisibleShips)
{
    public static MatchStateProjection From(StateResponse state, PlayerRole role)
    {
        ArgumentNullException.ThrowIfNull(state);

        var selfPublic = role == PlayerRole.Host ? state.Host : state.Guest;
        var opponentPublic = role == PlayerRole.Host ? state.Guest : state.Host;

        return new MatchStateProjection(
            State: state,
            Role: role,
            SelfPublic: selfPublic,
            OpponentPublic: opponentPublic,
            SelfHits: ToSet(selfPublic?.Hits),
            SelfMisses: ToSet(selfPublic?.Misses),
            SelfShipCells: state.Self is null
                ? new()
                : state.Self.Fleet.SelectMany(s => s.Coordinates).Select(c => c.ToDomain()).ToHashSet(),
            OpponentHits: ToSet(opponentPublic?.Hits),
            OpponentMisses: ToSet(opponentPublic?.Misses),
            // Only sunk opponent ships expose their coordinates; reveal them
            // as ship tiles on the offense board.
            OpponentVisibleShips: opponentPublic is null
                ? new()
                : opponentPublic.SunkShips.SelectMany(s => s.Coordinates).Select(c => c.ToDomain()).ToHashSet());
    }

    private static HashSet<Coordinate> ToSet(IReadOnlyList<CoordinateDto>? coords) =>
        coords is null ? new() : coords.Select(c => c.ToDomain()).ToHashSet();
}
