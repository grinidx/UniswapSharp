namespace UniswapSharp.LiquidityLauncher.Config;

/// <summary>
/// Fee → tick-spacing resolution for a launch pool. Ported from
/// sdks/liquidity-launcher-sdk/src/config/fees.ts.
/// </summary>
public static class Fees
{
    // Canonical v3 TICK_SPACINGS keyed by the raw fee amount (hundredths of a bip).
    private static readonly IReadOnlyDictionary<int, int> CanonicalTickSpacings =
        UniswapSharp.V3.Constants.TICK_SPACINGS.ToDictionary(kv => (int)kv.Key, kv => kv.Value);

    /// <summary>
    /// Resolves a pool's tick spacing from its fee. Prefers the canonical v3 <c>TICK_SPACINGS</c> for
    /// well-known tiers and otherwise derives it as the Uniswap interface does
    /// (<c>max(round(2*fee/100), 1)</c>). Only a fee whose derived spacing exceeds the v4 maximum is rejected.
    /// </summary>
    public static int FeeToTickSpacing(int fee)
    {
        int tickSpacing = CanonicalTickSpacings.TryGetValue(fee, out var canonical)
            ? canonical
            : (int)Math.Max(MathJs.Round(2.0 * fee / 100), 1);
        if (tickSpacing > Constants.MAX_TICK_SPACING)
        {
            throw new LauncherSdkError(
                LauncherErrorCode.INVALID_FEE, $"Fee tier {Format.FormatFeePercent(fee)} is not supported.");
        }
        return tickSpacing;
    }

    /// <summary>Resolves the pool <c>fee</c> field: the dynamic-fee flag, or the static fee in hundredths of a bip.</summary>
    public static int ResolvePoolFee(int fee, bool dynamic)
    {
        if (dynamic)
        {
            return Constants.DYNAMIC_FEE_FLAG;
        }
        if (fee > Constants.MAX_LP_FEE)
        {
            throw new LauncherSdkError(
                LauncherErrorCode.INVALID_FEE,
                $"Fee {Format.FormatFeePercent(fee)} exceeds the maximum of {Format.FormatFeePercent(Constants.MAX_LP_FEE)}.");
        }
        return fee;
    }
}
