using System.Numerics;
using UniswapSharp.UniswapX.Order;

namespace UniswapSharp.UniswapX.Utils;

/// <summary>
/// Port of uniswapx-sdk <c>utils/dutchBlockDecay.ts</c>: the non-linear (block-based) dutch decay library.
/// These functions mirror the reactor Solidity as closely as possible so the same results are produced.
/// </summary>
public static class NonLinearDutchDecayLib
{
    private static (int Prev, int Next) LocateArrayPosition(NonlinearDutchDecay curve, long currentRelativeBlock)
    {
        var relativeBlocks = curve.RelativeBlocks;
        int prev = 0;
        int next = 0;
        for (; next < relativeBlocks.Count; next++)
        {
            if (relativeBlocks[next] >= currentRelativeBlock)
            {
                return (prev, next);
            }
            prev = next;
        }
        return (next - 1, next - 1);
    }

    /// <summary>Computes the decayed amount for <paramref name="curve"/> at <paramref name="currentBlock"/>.</summary>
    public static BigInteger Decay(
        NonlinearDutchDecay curve,
        BigInteger startAmount,
        long decayStartBlock,
        long currentBlock)
    {
        // mismatch of relativeAmounts and relativeBlocks
        if (curve.RelativeAmounts.Count > 16)
        {
            throw new InvalidOperationException("InvalidDecayCurve");
        }

        // handle current block before decay or no decay
        if (decayStartBlock >= currentBlock || curve.RelativeAmounts.Count == 0)
        {
            return startAmount;
        }

        long blockDelta = currentBlock - decayStartBlock;

        // Special case for when we need to use the decayStartBlock (0)
        if (curve.RelativeBlocks[0] > blockDelta)
        {
            return LinearDecay(
                0,
                curve.RelativeBlocks[0],
                blockDelta,
                startAmount,
                startAmount - curve.RelativeAmounts[0]);
        }

        // the current pos is within or after the curve
        var (prev, next) = LocateArrayPosition(curve, blockDelta);
        BigInteger lastAmount = startAmount - curve.RelativeAmounts[prev];
        BigInteger nextAmount = startAmount - curve.RelativeAmounts[next];
        return LinearDecay(
            curve.RelativeBlocks[prev],
            curve.RelativeBlocks[next],
            blockDelta,
            lastAmount,
            nextAmount);
    }

    /// <summary>Linear interpolation between two points with truncation toward zero (mulDivDown), matching the reactor.</summary>
    public static BigInteger LinearDecay(
        long startPoint,
        long endPoint,
        long currentPoint,
        BigInteger startAmount,
        BigInteger endAmount)
    {
        if (currentPoint >= endPoint)
        {
            return endAmount;
        }

        BigInteger elapsed = currentPoint - startPoint;
        BigInteger duration = endPoint - startPoint;
        BigInteger delta;
        if (endAmount < startAmount)
        {
            delta = BigInteger.Zero - (startAmount - endAmount) * elapsed / duration; // mulDivDown in contract
        }
        else
        {
            delta = (endAmount - startAmount) * elapsed / duration; // mulDivDown in contract
        }
        return startAmount + delta;
    }
}

/// <summary>Config for a block-based dutch decay (uniswapx-sdk <c>DutchBlockDecayConfig</c>).</summary>
public sealed record DutchBlockDecayConfig(
    long DecayStartBlock,
    BigInteger StartAmount,
    IReadOnlyList<int> RelativeBlocks,
    IReadOnlyList<BigInteger> RelativeAmounts);

/// <summary>Free functions from uniswapx-sdk <c>utils/dutchBlockDecay.ts</c>.</summary>
public static class DutchBlockDecay
{
    /// <summary>Returns the block-decayed amount at <paramref name="atBlock"/>.</summary>
    public static BigInteger GetBlockDecayedAmount(DutchBlockDecayConfig config, long atBlock)
    {
        var curve = new NonlinearDutchDecay
        {
            RelativeAmounts = config.RelativeAmounts,
            RelativeBlocks = config.RelativeBlocks,
        };
        return NonLinearDutchDecayLib.Decay(curve, config.StartAmount, config.DecayStartBlock, atBlock);
    }

    /// <summary>Returns the V3 decay end amount (<c>startAmount - relativeAmounts[last]</c>).</summary>
    public static BigInteger GetEndAmount(BigInteger startAmount, IReadOnlyList<BigInteger> relativeAmounts)
    {
        if (relativeAmounts.Count == 0)
        {
            throw new InvalidOperationException("Invalid config for getting V3 decay end amount");
        }
        return startAmount - relativeAmounts[relativeAmounts.Count - 1];
    }
}
