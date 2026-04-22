namespace BattleShip.LanServer.Client;

/// <summary>
/// Uniform result for LAN client calls. Mirrors <see cref="ActionResult{T}"/> but adds a
/// <see cref="NetworkError"/> flag so callers can distinguish a protocol-level failure
/// (timeout, socket refused) from a server-returned error code.
/// </summary>
public readonly record struct LanClientResult<T>(
    bool Success,
    T? Value,
    string? ErrorCode,
    string? Message,
    bool NetworkError)
{
    public static LanClientResult<T> Ok(T value) => new(true, value, null, null, false);
    public static LanClientResult<T> Fail(string code, string message) => new(false, default, code, message, false);
    public static LanClientResult<T> Network(string message) => new(false, default, LanClientErrorCodes.NetworkError, message, true);
}

public static class LanClientErrorCodes
{
    public const string NetworkError = "network_error";
}
