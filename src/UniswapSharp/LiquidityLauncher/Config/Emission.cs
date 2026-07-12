using System.Numerics;

namespace UniswapSharp.LiquidityLauncher.Config;

/// <summary>Options for <see cref="Emission.DeriveConvexAuctionSteps"/>.</summary>
public record AuctionScheduleOptions(
    // Leading window where nothing is emitted (mps = 0). Default 0.
    BigInteger? PrebidBlocks = null,
    // Number of equal-token ramp steps before the final block. Default DEFAULT_AUCTION_STEPS.
    int? NumSteps = null,
    // Fraction (0,1) of supply reserved for the single final block. Default DEFAULT_FINAL_BLOCK_PCT.
    double? FinalBlockPct = null,
    // Convexity exponent for C(t) = t^alpha. Default DEFAULT_CONVEXITY_ALPHA.
    double? Alpha = null);

/// <summary>
/// Builds the CCA emission schedule as a moderately convex curve with a large final block. Ported
/// from sdks/liquidity-launcher-sdk/src/config/emission.ts.
/// </summary>
public static class Emission
{
    /// <summary>
    /// Builds the CCA emission schedule:
    /// <list type="bullet">
    /// <item>an optional leading prebid window emits nothing (mps = 0);</item>
    /// <item>the gradual ramp releases EQUAL token amounts across <c>numSteps</c> steps whose block
    /// durations DECREASE over time (boundaries from the inverse of C(t) = t^alpha);</item>
    /// <item>a single final block releases the reserved <c>finalBlockPct</c> (~30%).</item>
    /// </list>
    /// </summary>
    public static IReadOnlyList<AuctionStepInput> DeriveConvexAuctionSteps(
        BigInteger startBlock, BigInteger endBlock, AuctionScheduleOptions? options = null)
    {
        options ??= new AuctionScheduleOptions();
        BigInteger prebidBlocks = options.PrebidBlocks ?? BigInteger.Zero;
        int numSteps = options.NumSteps ?? Constants.DEFAULT_AUCTION_STEPS;
        double finalBlockPct = options.FinalBlockPct ?? Constants.DEFAULT_FINAL_BLOCK_PCT;
        double alpha = options.Alpha ?? Constants.DEFAULT_CONVEXITY_ALPHA;

        if (endBlock <= startBlock)
        {
            throw new LauncherSdkError(LauncherErrorCode.INVALID_AUCTION_WINDOW, "Auction must span at least one block");
        }
        if (prebidBlocks < 0)
        {
            throw new LauncherSdkError(LauncherErrorCode.INVALID_EMISSION_SCHEDULE, "Prebid window cannot be negative");
        }
        if (numSteps < 1)
        {
            throw new LauncherSdkError(
                LauncherErrorCode.INVALID_EMISSION_SCHEDULE, "Auction must have at least one emission step");
        }
        if (!(finalBlockPct > 0) || !(finalBlockPct < 1))
        {
            throw new LauncherSdkError(
                LauncherErrorCode.INVALID_EMISSION_SCHEDULE, "Final block percentage must be in (0, 1)");
        }
        if (!(alpha > 0))
        {
            throw new LauncherSdkError(LauncherErrorCode.INVALID_EMISSION_SCHEDULE, "Convexity alpha must be positive");
        }

        // Reserve exactly one block for the large final emission; the remainder (after prebid) is the ramp.
        BigInteger rampBlocks = endBlock - startBlock - prebidBlocks - 1;
        if (rampBlocks < 1)
        {
            throw new LauncherSdkError(
                LauncherErrorCode.INVALID_AUCTION_WINDOW, "The auction window is too short. Choose a longer window.");
        }

        var steps = new List<AuctionStepInput>();
        BigInteger cursor = startBlock;

        if (prebidBlocks > 0)
        {
            steps.Add(new AuctionStepInput(0, cursor, cursor + prebidBlocks));
            cursor += prebidBlocks;
        }

        // Clamp ramp steps so each spans at least one block (tiny windows degrade gracefully).
        int rampSteps = (int)Math.Min(numSteps, (double)rampBlocks);
        double stepTokens = (1 - finalBlockPct) / rampSteps * Constants.MPS_TOTAL;

        // Convex block boundaries within the ramp, forced strictly increasing and ending exactly at
        // rampBlocks (leaving one block per remaining step so no duration collapses to zero).
        var boundaries = new List<BigInteger> { BigInteger.Zero };
        for (int i = 1; i < rampSteps; i++)
        {
            double t = Math.Pow((double)i / rampSteps, 1.0 / alpha);
            var raw = new BigInteger(MathJs.Round(t * (double)rampBlocks));
            BigInteger lo = boundaries[i - 1] + 1;
            BigInteger hi = rampBlocks - (rampSteps - i);
            boundaries.Add(raw < lo ? lo : raw > hi ? hi : raw);
        }
        boundaries.Add(rampBlocks);

        long emittedMps = 0;
        for (int i = 0; i < rampSteps; i++)
        {
            BigInteger duration = boundaries[i + 1] - boundaries[i]; // >= 1 by construction
            int mps = (int)Math.Max(1, MathJs.Round(stepTokens / (double)duration));
            steps.Add(new AuctionStepInput(mps, cursor, cursor + duration));
            cursor += duration;
            emittedMps += (long)mps * (long)duration;
        }

        // Final block: a single block absorbing the remainder so per-block mps sums to exactly MPS_TOTAL.
        long finalMps = Constants.MPS_TOTAL - emittedMps;
        if (finalMps <= 0)
        {
            throw new LauncherSdkError(
                LauncherErrorCode.INVALID_EMISSION_SCHEDULE, "Emission schedule overshot the supply target");
        }
        steps.Add(new AuctionStepInput((int)finalMps, cursor, cursor + 1));
        cursor += 1;

        if (cursor != endBlock)
        {
            throw new LauncherSdkError(
                LauncherErrorCode.INVALID_EMISSION_SCHEDULE, "Emission schedule did not cover the auction window");
        }
        return steps;
    }
}
