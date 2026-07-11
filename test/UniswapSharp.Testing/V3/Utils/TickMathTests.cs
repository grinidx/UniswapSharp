using System.Numerics;
using UniswapSharp.V3.Utils;

namespace UniswapSharp.Testing.V3.Utils;

// Ported 1:1 from sdks/v3-sdk/src/utils/tickMath.test.ts
// (the non-integer `getSqrtRatioAtTick(1.5)` case is omitted: the C# API takes an int).
public class TickMathTests
{
    [Fact]
    public void MinTick_EqualsCorrectValue()
    {
        Assert.Equal(-887272, TickMath.MIN_TICK);
    }

    [Fact]
    public void MaxTick_EqualsCorrectValue()
    {
        Assert.Equal(887272, TickMath.MAX_TICK);
    }

    [Fact]
    public void GetSqrtRatioAtTick_ThrowsForTickTooSmall()
    {
        Assert.Throws<ArgumentException>(() => TickMath.GetSqrtRatioAtTick(TickMath.MIN_TICK - 1));
    }

    [Fact]
    public void GetSqrtRatioAtTick_ThrowsForTickTooLarge()
    {
        Assert.Throws<ArgumentException>(() => TickMath.GetSqrtRatioAtTick(TickMath.MAX_TICK + 1));
    }

    [Fact]
    public void GetSqrtRatioAtTick_MinTick()
    {
        Assert.Equal(TickMath.MIN_SQRT_RATIO, TickMath.GetSqrtRatioAtTick(TickMath.MIN_TICK));
    }

    [Fact]
    public void GetSqrtRatioAtTick_Tick0()
    {
        Assert.Equal(BigInteger.One << 96, TickMath.GetSqrtRatioAtTick(0));
    }

    [Fact]
    public void GetSqrtRatioAtTick_MaxTick()
    {
        Assert.Equal(TickMath.MAX_SQRT_RATIO, TickMath.GetSqrtRatioAtTick(TickMath.MAX_TICK));
    }

    [Fact]
    public void GetTickAtSqrtRatio_MinSqrtRatio()
    {
        Assert.Equal(TickMath.MIN_TICK, TickMath.GetTickAtSqrtRatio(TickMath.MIN_SQRT_RATIO));
    }

    [Fact]
    public void GetTickAtSqrtRatio_MaxSqrtRatioMinusOne()
    {
        Assert.Equal(TickMath.MAX_TICK - 1, TickMath.GetTickAtSqrtRatio(TickMath.MAX_SQRT_RATIO - BigInteger.One));
    }
}
