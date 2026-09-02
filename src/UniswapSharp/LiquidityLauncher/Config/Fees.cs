namespace UniswapSharp.LiquidityLauncher.Config;

/// <summary>
/// Fee → tick-spacing resolution for a launch pool. Ported from
/// sdks/liquidity-launcher-sdk/src/config/fees.ts.
/// </summary>
public static class Fees
{
    /// <summary>
    /// Resolves the tick spacing a <b>new</b> v4 pool opened by this launcher is initialized with, from
    /// its fee: <c>max(round(fee / 100), 1)</c> — one tick of spacing per bip of fee, floored at 1.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The governing rule: a new pool's tick spacing equals its LP fee expressed in basis points — the
    /// <c>fee</c> field is denominated in hundredths of a basis point, so <c>fee / 100</c> is that
    /// conversion. Fees that are not a whole number of basis points round to the nearest integer, and
    /// the result is floored at 1.
    /// </para>
    /// <para>
    /// v4 has no protocol-level fee→tickSpacing map, so each caller picks a spacing when it initializes
    /// a pool; this derivation is the launcher's single source of truth for that choice (2500 → 25,
    /// 3000 → 30, 10000 → 100). It deliberately does <b>not</b> consult the v3 <c>TICK_SPACINGS</c>
    /// table: v3's fee→spacing pairs are factory-enforced on-chain and describe v3 pools, not the pools
    /// this launcher opens.
    /// </para>
    /// <para>
    /// Contract — this function decides the spacing of a pool that does not exist yet. It must NEVER be
    /// used to reconstruct, hash, or look up the key of a pool that already exists: an existing pool's
    /// spacing is a property of that pool, fixed when it was initialized, and this derivation can change
    /// independently of it. Resolve an existing pool's spacing from the pool's own stored, served, or
    /// on-chain key, or — when no key is available — by racing every entry of the relevant
    /// <c>*_ALLOWED_POOL_TICK_SPACINGS</c> grandfather set.
    /// </para>
    /// </remarks>
    public static int ResolveNewPoolTickSpacing(int fee)
    {
        int tickSpacing = (int)Math.Max(MathJs.Round(fee / 100.0), 1);
        if (tickSpacing > Constants.MAX_TICK_SPACING)
        {
            throw new LauncherSdkError(
                LauncherErrorCode.INVALID_FEE, $"Fee tier {Format.FormatFeePercent(fee)} is not supported.");
        }
        return tickSpacing;
    }

    /// <summary>
    /// Same behaviour as <see cref="ResolveNewPoolTickSpacing"/>, under a name that states what the
    /// result is for. Kept so existing callers keep working.
    /// </summary>
    [Obsolete("Use ResolveNewPoolTickSpacing, which carries the same behaviour under a clearer name.")]
    public static int FeeToTickSpacing(int fee) => ResolveNewPoolTickSpacing(fee);

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
