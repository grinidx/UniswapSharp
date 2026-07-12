namespace UniswapSharp.LiquidityLauncher;

/// <summary>
/// Stable error codes for every input-validation failure the SDK can raise. Ported from
/// sdks/liquidity-launcher-sdk/src/errors.ts.
/// </summary>
public enum LauncherErrorCode
{
    UNSUPPORTED_CHAIN,
    INVALID_FLOOR_PRICE,
    INVALID_TIME,
    INVALID_AUCTION_WINDOW,
    INVALID_FEE,
    INVALID_PRICE_RANGE,
    INVALID_LP_ALLOCATION,
    INVALID_EMISSION_SCHEDULE,
    INVALID_AUCTION_STEP,
    INVALID_INPUT,
}

/// <summary>
/// Error thrown by all SDK input validation. Carries a stable <see cref="LauncherErrorCode"/> and a
/// user-facing message. Consumers forward both.
/// </summary>
public class LauncherSdkError : Exception
{
    public LauncherErrorCode Code { get; }

    public LauncherSdkError(LauncherErrorCode code, string message)
        : base(message)
    {
        Code = code;
    }
}

/// <summary>Type guard for forwarding a launcher validation error.</summary>
public static class LauncherErrors
{
    /// <summary>
    /// Whether <paramref name="error"/> is a <see cref="LauncherSdkError"/>. Upstream's
    /// <c>isLauncherSdkError</c> also does a structural check to survive a dual cjs/esm install where
    /// two copies of the class defeat <c>instanceof</c>; that scenario has no analogue in a
    /// single-assembly C# port, so the check reduces to a type test.
    /// </summary>
    public static bool IsLauncherSdkError(object? error) => error is LauncherSdkError;
}
