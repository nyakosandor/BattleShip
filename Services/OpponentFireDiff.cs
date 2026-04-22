using BattleShip.Core.Domain;
using BattleShip.LanServer.Contracts;

namespace BattleShip.App.Services;

/// <summary>
/// Keeps track of the previously-seen hit/miss/sunk sets on the *self* side
/// so incoming state snapshots can be diffed into readable opponent-fire log
/// entries.
/// </summary>
internal sealed class OpponentFireDiff
{
    private bool _baselineReady;
    private HashSet<Coordinate> _prevHits = new();
    private HashSet<Coordinate> _prevMisses = new();
    private HashSet<ShipType> _prevSunkTypes = new();

    /// <summary>
    /// Compute log lines representing new opponent fire since the last call.
    /// Returns lines in the order they should be prepended to the log
    /// (caller should iterate and <c>Insert(0, ...)</c> to preserve
    /// newest-first ordering).
    /// </summary>
    public IEnumerable<string> Diff(MatchStateProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        var currentSunkTypes = (projection.SelfPublic?.SunkShips ?? Array.Empty<ShipDto>())
            .Select(s => s.Type)
            .ToHashSet();

        var lines = new List<string>();

        if (_baselineReady)
        {
            // A sunk ship's cells also land in Hits, so suppress the per-cell
            // hit line when a new sunk entry appears on the same turn to keep
            // the log readable.
            var sunkCellsJustRevealed = (projection.SelfPublic?.SunkShips ?? Array.Empty<ShipDto>())
                .Where(s => !_prevSunkTypes.Contains(s.Type))
                .SelectMany(s => s.Coordinates.Select(c => c.ToDomain()))
                .ToHashSet();

            foreach (var hit in projection.SelfHits.Except(_prevHits).OrderBy(c => c.Y).ThenBy(c => c.X))
            {
                if (sunkCellsJustRevealed.Contains(hit)) continue;
                lines.Add(ShotLogFormatter.OpponentHit(hit));
            }

            foreach (var miss in projection.SelfMisses.Except(_prevMisses).OrderBy(c => c.Y).ThenBy(c => c.X))
            {
                lines.Add(ShotLogFormatter.OpponentMiss(miss));
            }

            foreach (var type in currentSunkTypes.Except(_prevSunkTypes))
            {
                lines.Add(ShotLogFormatter.OpponentSank(type));
            }
        }

        _prevHits = new HashSet<Coordinate>(projection.SelfHits);
        _prevMisses = new HashSet<Coordinate>(projection.SelfMisses);
        _prevSunkTypes = currentSunkTypes;
        _baselineReady = true;

        return lines;
    }
}
