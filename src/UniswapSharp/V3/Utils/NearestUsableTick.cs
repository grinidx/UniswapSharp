using UniswapSharp.V3.Entities;

namespace UniswapSharp.V3.Utils;

public static class NearestUsableTick
{
    /// <summary>
    /// Returns the closest tick that is nearest a given tick and usable for the given tick spacing
    /// </summary>
    /// <param name="tick">The target tick</param>
    /// <param name="tickSpacing">The spacing of the pool</param>
    /// <returns>The nearest usable tick</returns>
    public static int Find(int tick, int tickSpacing)
    {
        if (tickSpacing <= 0)
            throw new ArgumentException("TICK_SPACING");
        if (tick < Tick.MIN_TICK || tick > Tick.MAX_TICK)
            throw new ArgumentException("TICK_BOUND");

        int rounded = RoundHalfToPositiveInfinity(tick, tickSpacing) * tickSpacing;
        if (rounded < Tick.MIN_TICK) return rounded + tickSpacing;
        else if (rounded > Tick.MAX_TICK) return rounded - tickSpacing;
        else return rounded;
    }

    /// <summary>
    /// Integer equivalent of upstream's <c>Math.round(tick / tickSpacing)</c>. JavaScript's
    /// <c>Math.round</c> rounds a half toward positive infinity (round(0.5) = 1, round(-0.5) = -0),
    /// which differs from C#'s default banker's rounding. Computed with integers to keep floating
    /// point out of the tick math. Requires <paramref name="tickSpacing"/> &gt; 0.
    /// </summary>
    private static int RoundHalfToPositiveInfinity(int tick, int tickSpacing)
    {
        int quotient = tick / tickSpacing;
        int remainder = tick % tickSpacing;
        if (remainder < 0)
        {
            // Adjust C#'s trunc-toward-zero division to a floor division.
            quotient -= 1;
            remainder += tickSpacing;
        }

        // remainder is now in [0, tickSpacing); round the half up (toward +infinity).
        return 2 * remainder >= tickSpacing ? quotient + 1 : quotient;
    }
}
