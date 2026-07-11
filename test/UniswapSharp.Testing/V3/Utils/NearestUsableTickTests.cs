using UniswapSharp.V3.Utils;

namespace UniswapSharp.Testing.V3.Utils;

// Ported 1:1 from sdks/v3-sdk/src/utils/nearestUsableTick.test.ts.
// (The upstream 'INTEGERS' non-integer cases are omitted: the C# API takes ints.)
public class NearestUsableTickTests
{
    [Fact]
    public void ThrowsIfTickSpacingIsZero()
    {
        var ex = Assert.Throws<ArgumentException>(() => NearestUsableTick.Find(1, 0));
        Assert.Equal("TICK_SPACING", ex.Message);
    }

    [Fact]
    public void ThrowsIfTickSpacingIsNegative()
    {
        var ex = Assert.Throws<ArgumentException>(() => NearestUsableTick.Find(1, -5));
        Assert.Equal("TICK_SPACING", ex.Message);
    }

    [Fact]
    public void ThrowsIfTickIsOutOfBounds()
    {
        var above = Assert.Throws<ArgumentException>(() => NearestUsableTick.Find(TickMath.MAX_TICK + 1, 1));
        Assert.Equal("TICK_BOUND", above.Message);

        var below = Assert.Throws<ArgumentException>(() => NearestUsableTick.Find(TickMath.MIN_TICK - 1, 1));
        Assert.Equal("TICK_BOUND", below.Message);
    }

    [Fact]
    public void RoundsAtPositiveHalf()
    {
        Assert.Equal(10, NearestUsableTick.Find(5, 10));
    }

    [Fact]
    public void RoundsDownBelowPositiveHalf()
    {
        Assert.Equal(0, NearestUsableTick.Find(4, 10));
    }

    [Fact]
    public void RoundsUpForNegativeHalf()
    {
        Assert.Equal(0, NearestUsableTick.Find(-5, 10));
    }

    [Fact]
    public void RoundsDownForNegativeBelowHalf()
    {
        Assert.Equal(-10, NearestUsableTick.Find(-6, 10));
    }

    [Fact]
    public void CannotRoundPastMinTick()
    {
        Assert.Equal(-(TickMath.MAX_TICK / 2 + 100), NearestUsableTick.Find(TickMath.MIN_TICK, TickMath.MAX_TICK / 2 + 100));
    }

    [Fact]
    public void CannotRoundPastMaxTick()
    {
        Assert.Equal(TickMath.MAX_TICK / 2 + 100, NearestUsableTick.Find(TickMath.MAX_TICK, TickMath.MAX_TICK / 2 + 100));
    }
}
