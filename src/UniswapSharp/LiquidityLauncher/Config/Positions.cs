using System.Numerics;
using UniswapSharp.V3.Utils;

namespace UniswapSharp.LiquidityLauncher.Config;

/// <summary>Price-range strategy selector.</summary>
public enum PriceRangeKind
{
    CONCENTRATED_FULL_RANGE,
    FULL_RANGE,
    CUSTOM_RANGE,
}

public record CustomRangeInput(double MinPercentFromClearing, double MaxPercentFromClearing, double LiquidityPercent);

/// <summary>
/// Price-range strategy → <c>PositionDefinition[]</c> (the LP positions the migrator opens). Offsets
/// are ticks relative to the final auction (clearing) tick. Ported from src/config/positions.ts.
/// </summary>
public static class Positions
{
    /// <summary>
    /// Parses a hex address as an unsigned integer for v4 currency ordering. The leading "0" keeps
    /// <see cref="BigInteger.Parse(string, System.Globalization.NumberStyles)"/> from reading a
    /// high top nibble as a negative sign.
    /// </summary>
    private static BigInteger ParseAddress(string address) =>
        BigInteger.Parse(
            "0" + (address.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? address[2..] : address),
            System.Globalization.NumberStyles.HexNumber);

    private static int SnapDown(int tick, int tickSpacing) =>
        (int)Math.Floor((double)tick / tickSpacing) * tickSpacing;

    private static int SnapUp(int tick, int tickSpacing) =>
        (int)Math.Ceiling((double)tick / tickSpacing) * tickSpacing;

    /// <summary>
    /// Tick offset (from the clearing tick) for a percent change in price.
    /// pct = 0 → 0; pct = +Inf sentinel → MAX_TICK; pct &lt;= -100 → MIN_TICK.
    /// </summary>
    public static int PercentToTickOffset(double pct)
    {
        if (pct >= Constants.UNBOUNDED_PERCENT)
        {
            return TickMath.MAX_TICK;
        }
        if (pct <= -100)
        {
            return TickMath.MIN_TICK;
        }
        // The price ratio is (100 + pct) / 100. encodeSqrtRatioX96(amount1, amount0) takes integer
        // numerator/denominator, so scale both by 1000 to preserve fractional percents; the factor cancels.
        var num = new BigInteger(MathJs.Round((100 + pct) * 1000));
        BigInteger den = 100 * 1000;
        if (num <= 0)
        {
            return TickMath.MIN_TICK;
        }
        BigInteger sqrtRatioX96 = EncodeSqrtRatioX96.Encode(num, den);
        return TickMath.GetTickAtSqrtRatio(sqrtRatioX96);
    }

    // A single full-range position. overridePositionRecipient = address(0) defers to the migrator.
    private static PositionDefinition FullRangeDefinition() =>
        new(TickMath.MIN_TICK, TickMath.MAX_TICK, Constants.MPS_TOTAL, Constants.ZERO_ADDRESS);

    /// <summary>Builds the LP <c>PositionDefinition[]</c> for a price-range strategy.</summary>
    /// <remarks>
    /// <paramref name="currency"/> / <paramref name="token"/> (the raised currency and the launched
    /// token) determine v4 currency ordering. When <paramref name="currency"/> sorts as
    /// <c>currency0</c> the custom offsets are mirrored onto the reciprocal price band. Native ETH is
    /// <c>ZERO_ADDRESS</c>, which always sorts as <c>currency0</c>.
    /// </remarks>
    public static IReadOnlyList<PositionDefinition> BuildPositionDefinitions(
        PriceRangeKind strategy, IReadOnlyList<CustomRangeInput> customRanges, int tickSpacing,
        string currency, string token)
    {
        if (strategy != PriceRangeKind.CUSTOM_RANGE)
        {
            // Concentrated-full-range and full-range both resolve to a single full-range position for v1.
            // The full-range sentinel is mirror-invariant, so ordering does not affect it.
            return new[] { FullRangeDefinition() };
        }

        // Matches the contract's `currency < token` (LBPStrategy) ordering; ZERO_ADDRESS (native) is
        // always currency0.
        bool currencyIsCurrency0 = ParseAddress(currency) < ParseAddress(token);
        if (customRanges.Count == 0)
        {
            throw new LauncherSdkError(
                LauncherErrorCode.INVALID_PRICE_RANGE, "Custom price range strategy requires at least one range");
        }
        if (customRanges.Count > 10)
        {
            throw new LauncherSdkError(
                LauncherErrorCode.INVALID_PRICE_RANGE, "At most 10 custom price ranges are allowed");
        }
        int weightSum = 0;
        var definitions = new List<PositionDefinition>();
        foreach (var range in customRanges)
        {
            double spanLo = Math.Min(range.MinPercentFromClearing, range.MaxPercentFromClearing);
            double spanHi = Math.Max(range.MinPercentFromClearing, range.MaxPercentFromClearing);
            // Clearing settles at 0% offset; LP ticks must bracket that price, so the percent interval
            // must contain zero.
            if (spanLo > 0 || spanHi < 0)
            {
                throw new LauncherSdkError(
                    LauncherErrorCode.INVALID_PRICE_RANGE,
                    "Custom price range must include the clearing price: min and max percent from clearing must bracket 0%");
            }
            int weight = (int)MathJs.Round(range.LiquidityPercent * (Constants.MPS_TOTAL / 100.0));
            weightSum += weight;
            int rawLower = PercentToTickOffset(range.MinPercentFromClearing);
            int rawUpper = PercentToTickOffset(range.MaxPercentFromClearing);
            // Clamp to the valid tick range: snapping an unbounded range outward can push the offset
            // past MAX_TICK / below MIN_TICK, which reverts on-chain as an invalid tick.
            int offsetLower = Math.Max(SnapDown(rawLower, tickSpacing), TickMath.MIN_TICK);
            int offsetUpper = Math.Min(SnapUp(rawUpper, tickSpacing), TickMath.MAX_TICK);
            // When currency is currency0 the pool price is the reciprocal of currency-per-token, so
            // mirror the range onto the reciprocal band. Negate-and-swap keeps the values tick-aligned
            // and in range (MIN_TICK == -MAX_TICK), and preserves upper > lower.
            if (currencyIsCurrency0)
            {
                (offsetLower, offsetUpper) = (-offsetUpper, -offsetLower);
            }
            if (offsetUpper <= offsetLower)
            {
                throw new LauncherSdkError(
                    LauncherErrorCode.INVALID_PRICE_RANGE, "Custom price range upper bound must exceed the lower bound");
            }
            definitions.Add(new PositionDefinition(offsetLower, offsetUpper, weight, Constants.ZERO_ADDRESS));
        }
        if (weightSum > Constants.MPS_TOTAL)
        {
            throw new LauncherSdkError(
                LauncherErrorCode.INVALID_PRICE_RANGE, "Custom price range liquidity percentages exceed 100%");
        }
        // Under-allocation is just as invalid: weights summing to less than MPS_TOTAL leave a slice of
        // LP liquidity with no destination position.
        if (weightSum < Constants.MPS_TOTAL)
        {
            throw new LauncherSdkError(
                LauncherErrorCode.INVALID_PRICE_RANGE, "Custom price range liquidity percentages must sum to 100%");
        }
        return definitions;
    }
}
