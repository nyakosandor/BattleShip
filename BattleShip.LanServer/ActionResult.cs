namespace BattleShip.LanServer;

public static class ActionErrorCodes
{
    public const string Unauthorized = "unauthorized";
    public const string SessionFull = "session_full";
    public const string InvalidPhase = "invalid_phase";
    public const string InvalidTurn = "invalid_turn";
    public const string AlreadyDeployed = "already_deployed";
    public const string InvalidFleet = "invalid_fleet";
    public const string InvalidShot = "invalid_shot";
    public const string BadRequest = "bad_request";
    public const string GameFinished = "game_finished";
}

public readonly record struct ActionResult<T>(bool Success, T? Value, string? ErrorCode, string? Message)
{
    public static ActionResult<T> Ok(T value) => new(true, value, null, null);
    public static ActionResult<T> Fail(string code, string message) => new(false, default, code, message);
}
