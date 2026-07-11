using UniswapSharp.V3.Entities;
using UniswapSharp.V3.Utils;

namespace UniswapSharp.Testing.V3.Utils;

// Ported 1:1 from sdks/v3-sdk/src/utils/tickList.test.ts
public class TickListTests
{
    // Tick(index, liquidityNet, liquidityGross)
    private static readonly Tick lowTick = new(TickMath.MIN_TICK + 1, 10, 10);
    private static readonly Tick midTick = new(0, -5, 5);
    private static readonly Tick highTick = new(TickMath.MAX_TICK - 1, -5, 5);
    private static readonly List<Tick> ticks = new() { lowTick, midTick, highTick };

    // ---- #validate ----
    [Fact]
    public void Validate_ErrorsForIncompleteLists()
    {
        var ex = Assert.Throws<ArgumentException>(() => TickList.ValidateList(new List<Tick> { lowTick }, 1));
        Assert.Equal("ZERO_NET", ex.Message);
    }

    [Fact]
    public void Validate_ErrorsForUnsortedLists()
    {
        var ex = Assert.Throws<ArgumentException>(() => TickList.ValidateList(new List<Tick> { highTick, lowTick, midTick }, 1));
        Assert.Equal("SORTED", ex.Message);
    }

    [Fact]
    public void Validate_ErrorsForTicksNotOnTickSpacingMultiples()
    {
        var ex = Assert.Throws<ArgumentException>(() => TickList.ValidateList(new List<Tick> { highTick, lowTick, midTick }, 1337));
        Assert.Equal("TICK_SPACING", ex.Message);
    }

    // ---- #isBelowSmallest ----
    [Fact]
    public void IsBelowSmallest()
    {
        Assert.True(TickList.IsBelowSmallest(ticks, TickMath.MIN_TICK));
        Assert.False(TickList.IsBelowSmallest(ticks, TickMath.MIN_TICK + 1));
    }

    // ---- #isAtOrAboveLargest ----
    [Fact]
    public void IsAtOrAboveLargest()
    {
        Assert.False(TickList.IsAtOrAboveLargest(ticks, TickMath.MAX_TICK - 2));
        Assert.True(TickList.IsAtOrAboveLargest(ticks, TickMath.MAX_TICK - 1));
    }

    // ---- #nextInitializedTick ----
    [Fact]
    public void NextInitializedTick_LowLteTrue()
    {
        Assert.Throws<ArgumentException>(() => TickList.NextInitializedTick(ticks, TickMath.MIN_TICK, true));
        Assert.Same(lowTick, TickList.NextInitializedTick(ticks, TickMath.MIN_TICK + 1, true));
        Assert.Same(lowTick, TickList.NextInitializedTick(ticks, TickMath.MIN_TICK + 2, true));
    }

    [Fact]
    public void NextInitializedTick_LowLteFalse()
    {
        Assert.Same(lowTick, TickList.NextInitializedTick(ticks, TickMath.MIN_TICK, false));
        Assert.Same(midTick, TickList.NextInitializedTick(ticks, TickMath.MIN_TICK + 1, false));
    }

    [Fact]
    public void NextInitializedTick_MidLteTrue()
    {
        Assert.Same(midTick, TickList.NextInitializedTick(ticks, 0, true));
        Assert.Same(midTick, TickList.NextInitializedTick(ticks, 1, true));
    }

    [Fact]
    public void NextInitializedTick_MidLteFalse()
    {
        Assert.Same(midTick, TickList.NextInitializedTick(ticks, -1, false));
        Assert.Same(highTick, TickList.NextInitializedTick(ticks, 0 + 1, false));
    }

    [Fact]
    public void NextInitializedTick_HighLteTrue()
    {
        Assert.Same(highTick, TickList.NextInitializedTick(ticks, TickMath.MAX_TICK - 1, true));
        Assert.Same(highTick, TickList.NextInitializedTick(ticks, TickMath.MAX_TICK, true));
    }

    [Fact]
    public void NextInitializedTick_HighLteFalse()
    {
        Assert.Throws<ArgumentException>(() => TickList.NextInitializedTick(ticks, TickMath.MAX_TICK - 1, false));
        Assert.Same(highTick, TickList.NextInitializedTick(ticks, TickMath.MAX_TICK - 2, false));
        Assert.Same(highTick, TickList.NextInitializedTick(ticks, TickMath.MAX_TICK - 3, false));
    }

    // ---- #nextInitializedTickWithinOneWord ----
    [Theory]
    [InlineData(-257, -512, false)]
    [InlineData(-256, -256, false)]
    [InlineData(-1, -256, false)]
    [InlineData(0, 0, true)]
    [InlineData(1, 0, true)]
    [InlineData(255, 0, true)]
    [InlineData(256, 256, false)]
    [InlineData(257, 256, false)]
    public void NextInitializedTickWithinOneWord_Around0_LteTrue(int tick, int expectedTick, bool expectedInitialized)
    {
        Assert.Equal((expectedTick, expectedInitialized), TickList.NextInitializedTickWithinOneWord(ticks, tick, true, 1));
    }

    [Theory]
    [InlineData(-258, -257, false)]
    [InlineData(-257, -1, false)]
    [InlineData(-256, -1, false)]
    [InlineData(-2, -1, false)]
    [InlineData(-1, 0, true)]
    [InlineData(0, 255, false)]
    [InlineData(1, 255, false)]
    [InlineData(254, 255, false)]
    [InlineData(255, 511, false)]
    [InlineData(256, 511, false)]
    public void NextInitializedTickWithinOneWord_Around0_LteFalse(int tick, int expectedTick, bool expectedInitialized)
    {
        Assert.Equal((expectedTick, expectedInitialized), TickList.NextInitializedTickWithinOneWord(ticks, tick, false, 1));
    }

    [Fact]
    public void NextInitializedTickWithinOneWord_TickSpacingGreaterThanOne()
    {
        var spacedTicks = new List<Tick>
        {
            new(0, 0, 0),
            new(511, 0, 0),
        };

        Assert.Equal((255, false), TickList.NextInitializedTickWithinOneWord(spacedTicks, 0, false, 1));
        Assert.Equal((510, false), TickList.NextInitializedTickWithinOneWord(spacedTicks, 0, false, 2));
    }
}
