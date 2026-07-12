namespace UniswapSharp.LiquidityLauncher.Config;

/// <summary>
/// JavaScript-compatible numeric helpers. Several upstream config helpers derive block ranges and
/// milli-percent (mps) weights with IEEE-754 <c>number</c> arithmetic (block-time cadence estimates,
/// the convexity curve <c>t^alpha</c>). Those are inherently floating point in the reference and the
/// on-chain contracts validate the results with tolerance; matching upstream to the digit means
/// mirroring the exact same double math, including <c>Math.round</c>'s round-half-toward-+Infinity.
/// </summary>
internal static class MathJs
{
    /// <summary>
    /// JavaScript <c>Math.round</c>: rounds to the nearest integer, ties toward +Infinity
    /// (<c>floor(x + 0.5)</c>). C#'s <see cref="Math.Round(double)"/> defaults to banker's rounding,
    /// so it cannot be used here.
    /// </summary>
    public static double Round(double x) => Math.Floor(x + 0.5);
}
