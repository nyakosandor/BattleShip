using BattleShip.Core.Domain;
using BattleShip.Core.Services;
using BattleShip.LanServer.Contracts;

namespace BattleShip.App.Services;

/// <summary>
/// Centralised formatting for shot-log entries. Kept pure / static so the
/// pages don't have to redeclare these strings and the tests (should we add
/// them) can pin the wording in one place.
/// </summary>
internal static class ShotLogFormatter
{
    public static string Coord(Coordinate c) => $"{(char)('A' + c.X)}{c.Y + 1}";
    public static string Coord(CoordinateDto c) => Coord(new Coordinate(c.X, c.Y));

    // ---------- Single-player (local engine) ----------

    public static string PlayerShot(ShotResult r) => r.Outcome switch
    {
        ShotOutcome.Hit => $"You hit at {Coord(r.Coordinate)}.",
        ShotOutcome.Sunk => $"You sank the enemy {r.ShipHit?.Type.DisplayName()} at {Coord(r.Coordinate)}.",
        ShotOutcome.Miss => $"You missed at {Coord(r.Coordinate)}.",
        _ => $"Invalid shot at {Coord(r.Coordinate)}.",
    };

    public static string AiShot(ShotResult r) => r.Outcome switch
    {
        ShotOutcome.Hit => $"Enemy hit at {Coord(r.Coordinate)}.",
        ShotOutcome.Sunk => $"Enemy sank your {r.ShipHit?.Type.DisplayName()} at {Coord(r.Coordinate)}.",
        ShotOutcome.Miss => $"Enemy missed at {Coord(r.Coordinate)}.",
        _ => $"Enemy fired invalidly at {Coord(r.Coordinate)}.",
    };

    // ---------- LAN (DTOs from the server) ----------

    public static string PlayerLanShot(FireResponse r)
    {
        var target = Coord(r.Target);
        return r.Outcome switch
        {
            ShotOutcomeDto.Hit => $"You hit at {target}.",
            ShotOutcomeDto.Sunk when r.ShipSunk is ShipType s => $"You sank the enemy {s.DisplayName()} at {target}.",
            ShotOutcomeDto.Sunk => $"You sank an enemy ship at {target}.",
            ShotOutcomeDto.Miss => $"You missed at {target}.",
            _ => $"Invalid shot at {target}.",
        };
    }

    public static string OpponentHit(Coordinate c) => $"Opponent hit your fleet at {Coord(c)}.";
    public static string OpponentMiss(Coordinate c) => $"Opponent missed at {Coord(c)}.";
    public static string OpponentSank(ShipType type) => $"Opponent sank your {type.DisplayName()}.";
}
