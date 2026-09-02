using System.Numerics;

namespace UniswapSharp.LiquidityLauncher;

/// <summary>A <c>FeeSplitter.FeesCollected</c> event's amounts (both sides of one collect for one position).</summary>
/// <param name="NativeAmount">The native ETH (currency0) fees collected.</param>
/// <param name="TokenAmount">The token (currency1) fees collected.</param>
public sealed record FeesCollectedAmounts(BigInteger NativeAmount, BigInteger TokenAmount);

/// <summary>A claim recipient <c>Claimed</c> event's amounts (vault or compounding recipient payout).</summary>
/// <param name="Currency0Amount">The native ETH (currency0) amount paid out.</param>
/// <param name="Currency1Amount">The token (currency1) amount paid out.</param>
public sealed record ClaimedAmounts(BigInteger Currency0Amount, BigInteger Currency1Amount);

/// <summary>The creator's share of collected fees, in bps per currency side (see the deployment registry).</summary>
/// <param name="CreatorFeeNativeBps">Native-side (ETH) bps forwarded to the beneficiary vault.</param>
/// <param name="CreatorFeeTokenBps">Token-side bps forwarded to the beneficiary vault.</param>
public sealed record CreatorFeeSplitBps(int CreatorFeeNativeBps, int CreatorFeeTokenBps);

/// <summary>A native + token amount pair.</summary>
public sealed record NativeAndTokenAmounts(BigInteger Native, BigInteger Token);

/// <summary>
/// Pure fee math over indexed Instant Launch fee events, so indexer/backend and frontend consumers
/// compute the three creator-fee metrics — accumulated, claimable, compounded — from the same
/// implementation. Ported from sdks/liquidity-launcher-sdk/src/instantLaunchFees.ts.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>Accumulated</b>: <c>FeeSplitter.FeesCollected(tokenId, token, nativeAmount, tokenAmount)</c>
/// fires once per collected position with the full realized amounts; the splitter then forwards each
/// immutable split leg as <c>floor(amount * bps / 10000)</c> and attributes it in the recipient.
/// Summing the per-event floors of the vault leg therefore reproduces the vault's on-chain
/// attribution exactly (sub-bps dust stays in the splitter and is flushed into the NEXT
/// <c>FeesCollected</c> amount).</item>
/// <item><b>Claimable</b>: attribution minus payouts — the vault's <c>Claimed</c> events carry the
/// actual paid amounts.</item>
/// <item><b>Compounded</b>: the CompoundingClaimRecipient enforces a same-transaction liquidity
/// increase on the claimed position, so its <c>Claimed</c> events are proofs of compounding.</item>
/// </list>
/// The per-splitter bps live in the deployment registry: 4000 native / 0 token on the fees-on
/// splitter, 0 / 0 on the fees-off one.
/// </remarks>
public static class InstantLaunchFees
{
    /// <summary>
    /// Creator fees accumulated for one position: the sum of the beneficiary-vault leg over its
    /// <c>FeesCollected</c> events, floored per event exactly as the FeeSplitter forwards it
    /// (<c>floor(amount * bps / 10000)</c> per collect). The fees-off splitter has 0 bps, so its
    /// launches always accumulate 0.
    /// </summary>
    public static NativeAndTokenAmounts CreatorFeesAccumulated(
        IReadOnlyList<FeesCollectedAmounts> feesCollectedEvents, CreatorFeeSplitBps split)
    {
        BigInteger nativeBps = ToBps(split.CreatorFeeNativeBps, nameof(split.CreatorFeeNativeBps));
        BigInteger tokenBps = ToBps(split.CreatorFeeTokenBps, nameof(split.CreatorFeeTokenBps));

        BigInteger native = BigInteger.Zero;
        BigInteger token = BigInteger.Zero;
        BigInteger denominator = Addresses.FEE_SPLIT_BPS_DENOMINATOR;

        foreach (var collected in feesCollectedEvents)
        {
            RequireNonNegative(collected.NativeAmount, "nativeAmount");
            RequireNonNegative(collected.TokenAmount, "tokenAmount");
            // BigInteger division truncates toward zero; both operands are non-negative here, so this
            // is the floor, matching the on-chain per-event integer division.
            native += collected.NativeAmount * nativeBps / denominator;
            token += collected.TokenAmount * tokenBps / denominator;
        }

        return new NativeAndTokenAmounts(native, token);
    }

    /// <summary>
    /// Creator fees still claimable: accumulated minus already claimed, floored at zero. Clamped
    /// because balance-backed donation attribution can push on-chain payouts above the event-derived
    /// accumulation; a negative claimable is never meaningful.
    /// </summary>
    public static BigInteger CreatorFeesClaimable(BigInteger accumulated, BigInteger claimed)
    {
        RequireNonNegative(accumulated, "accumulated");
        RequireNonNegative(claimed, "claimed");
        BigInteger remaining = accumulated - claimed;
        return remaining > BigInteger.Zero ? remaining : BigInteger.Zero;
    }

    /// <summary>
    /// Fees auto-compounded for one position: the sum of the CompoundingClaimRecipient's
    /// <c>Claimed</c> events. Each such claim is enforced on-chain to increase the same position's
    /// liquidity within the same transaction.
    /// </summary>
    public static NativeAndTokenAmounts FeesCompounded(IReadOnlyList<ClaimedAmounts> claimedEvents)
    {
        BigInteger native = BigInteger.Zero;
        BigInteger token = BigInteger.Zero;
        foreach (var claimed in claimedEvents)
        {
            RequireNonNegative(claimed.Currency0Amount, "currency0Amount");
            RequireNonNegative(claimed.Currency1Amount, "currency1Amount");
            native += claimed.Currency0Amount;
            token += claimed.Currency1Amount;
        }
        return new NativeAndTokenAmounts(native, token);
    }

    private static BigInteger ToBps(int bps, string name)
    {
        if (bps < 0 || bps > Addresses.FEE_SPLIT_BPS_DENOMINATOR)
        {
            throw new LauncherSdkError(
                LauncherErrorCode.INVALID_INPUT,
                $"{name} must be an integer between 0 and {Addresses.FEE_SPLIT_BPS_DENOMINATOR}");
        }
        return bps;
    }

    private static void RequireNonNegative(BigInteger amount, string name)
    {
        if (amount < BigInteger.Zero)
        {
            throw new LauncherSdkError(LauncherErrorCode.INVALID_INPUT, $"{name} must not be negative");
        }
    }
}
