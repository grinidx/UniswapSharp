using System;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nethereum.RPC.Eth.Filters;
using UniswapSharp.V3.Entities;

namespace UniswapSharp.V3.Utils;

public static class TickList
{
    private static readonly BigInteger ZERO = BigInteger.Zero;

    public static void ValidateList(List<Tick> ticks, int tickSpacing)
    {
        if (tickSpacing <= 0)
            throw new ArgumentException("TICK_SPACING_NONZERO");

        if (ticks.Any(tick => tick.Index % tickSpacing != 0))
            throw new ArgumentException("TICK_SPACING");

        if (ticks.Aggregate(ZERO, (acc, tick) => acc + tick.LiquidityNet) != ZERO)
            throw new ArgumentException("ZERO_NET");

        if (!IsSorted(ticks))
            throw new ArgumentException("SORTED");
    }

    private static bool IsSorted(List<Tick> ticks)
    {
        return ticks.Zip(ticks.Skip(1), (a, b) => a.Index <= b.Index).All(x => x);
    }

    public static bool IsBelowSmallest(IReadOnlyList<Tick> ticks, int tick)
    {
        if (ticks.Count == 0)
            throw new ArgumentException("LENGTH");

        return tick < ticks[0].Index;
    }

    public static bool IsAtOrAboveLargest(IReadOnlyList<Tick> ticks, int tick)
    {
        if (ticks.Count == 0)
            throw new ArgumentException("LENGTH");

        return tick >= ticks[ticks.Count - 1].Index;
    }

    public static Tick GetTick(IReadOnlyList<Tick> ticks, int index)
    {
        var tick = ticks[BinarySearch(ticks, index)];
        if (tick.Index != index)
            throw new ArgumentException("NOT_CONTAINED");

        return tick;
    }

    private static int BinarySearch(IReadOnlyList<Tick> ticks, int tick)
    {
        if (IsBelowSmallest(ticks, tick))
            throw new ArgumentException("BELOW_SMALLEST");

        int l = 0;
        int r = ticks.Count - 1;

        while (true)
        {
            int i = (l + r) / 2;

            if (ticks[i].Index <= tick && (i == ticks.Count - 1 || ticks[i + 1].Index > tick))
                return i;

            if (ticks[i].Index < tick)
                l = i + 1;
            else
                r = i - 1;
        }
    }

    public static Tick NextInitializedTick(IReadOnlyList<Tick> ticks, int tick, bool lte)
    {
        if (lte)
        {
            if (IsBelowSmallest(ticks, tick))
                throw new ArgumentException("BELOW_SMALLEST");

            if (IsAtOrAboveLargest(ticks, tick))
                return ticks[ticks.Count - 1];

            int index = BinarySearch(ticks, tick);
            return ticks[index];
        }
        else
        {
            if (IsAtOrAboveLargest(ticks, tick))
                throw new ArgumentException("AT_OR_ABOVE_LARGEST");

            if (IsBelowSmallest(ticks, tick))
                return ticks[0];

            int index = BinarySearch(ticks, tick);
            return ticks[index + 1];
        }
    }

    public static (int, bool) NextInitializedTickWithinOneWord(IReadOnlyList<Tick> ticks, int tick, bool lte, int tickSpacing)
    {

        int compressed = (int)Math.Floor((decimal)tick / (decimal)tickSpacing);

        if (lte)
        {
            int wordPos = ((int)compressed) >> 8;
            int minimum = (wordPos << 8) * tickSpacing;

            if (IsBelowSmallest(ticks, tick))
                return (minimum, false);

            int index = NextInitializedTick(ticks, tick, lte).Index;
            int nextInitializedTick = Math.Max(minimum, index);


            return (nextInitializedTick, nextInitializedTick == index);
        }
        else
        {
            int wordPos = (compressed + 1) >> 8;
            int maximum = (((wordPos + 1) << 8) - 1) * tickSpacing;

            if (IsAtOrAboveLargest(ticks, tick))
                return (maximum, false);

            int index = NextInitializedTick(ticks, tick, lte).Index;
            int nextInitializedTick = Math.Min(maximum, index);
            return (nextInitializedTick, nextInitializedTick == index);
        }
    }
}
