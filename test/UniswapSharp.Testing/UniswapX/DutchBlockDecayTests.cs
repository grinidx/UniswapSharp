using System.Numerics;
using AwesomeAssertions;
using UniswapSharp.UniswapX.Order;
using UniswapSharp.UniswapX.Utils;

namespace UniswapSharp.Testing.UniswapX;

// Port of uniswapx-sdk src/utils/dutchBlockDecay.test.ts.
public class DutchBlockDecayTests
{
    private static NonlinearDutchDecay Curve(int[] blocks, long[] amounts) => new()
    {
        RelativeBlocks = blocks,
        RelativeAmounts = amounts.Select(a => (BigInteger)a).ToList(),
    };

    // describe("linearDecay")

    [Fact]
    public void SimpleLinearDecay()
    {
        NonLinearDutchDecayLib.LinearDecay(0, 10, 5, 100, 50).ToString().Should().Be("75");
    }

    [Fact]
    public void MulDivDownForEndAmountLessThanStartAmount()
    {
        NonLinearDutchDecayLib.LinearDecay(0, 10, 5, 100, 75).ToString().Should().Be("88");
    }

    [Fact]
    public void SimpleLinearDecayEndAmountGreaterThanStartAmount()
    {
        NonLinearDutchDecayLib.LinearDecay(0, 10, 5, 100, 120).ToString().Should().Be("110");
    }

    [Fact]
    public void MulDivDownForEndAmountGreaterThanStartAmount()
    {
        NonLinearDutchDecayLib.LinearDecay(0, 10, 5, 100, 125).ToString().Should().Be("112");
    }

    // describe("decay")

    [Fact]
    public void ReturnsStartAmountIfDecayHasntStarted()
    {
        var curve = Curve(new[] { 1, 2, 3, 4, 5 }, new long[] { 0, 10, 20, 30, 40 });
        NonLinearDutchDecayLib.Decay(curve, 100, 0, 0).ToString().Should().Be("100");
    }

    [Fact]
    public void CorrectlyCalculatesNonRoundingDecay()
    {
        var curve = Curve(new[] { 4 }, new long[] { 40 });
        NonLinearDutchDecayLib.Decay(curve, 100, 0, 2).ToString().Should().Be("80");
    }

    [Fact]
    public void CorrectlyCalculatesNonRoundingDecayWithMultiplePoints()
    {
        var curve = Curve(new[] { 4, 6 }, new long[] { 40, 20 });
        NonLinearDutchDecayLib.Decay(curve, 100, 0, 5).ToString().Should().Be("70");
    }

    [Fact]
    public void CorrectlyCalculatesNonRoundingNegativeDecay()
    {
        var curve = Curve(new[] { 4 }, new long[] { -40 });
        NonLinearDutchDecayLib.Decay(curve, 100, 0, 2).ToString().Should().Be("120");
    }

    [Fact]
    public void CorrectlyCalculatesNonRoundingNegativeDecayWithMultiplePoints()
    {
        var curve = Curve(new[] { 4, 6 }, new long[] { -40, -20 });
        NonLinearDutchDecayLib.Decay(curve, 100, 0, 5).ToString().Should().Be("130");
    }

    // describe("Tempo-realistic decay (60 blocks @ 0.5s ~= 30s wallclock)")

    private const int TempoDecayBlocks = 60;
    private const long DecayStartBlock = 1_000_000;
    private static readonly BigInteger TempoStartAmount = BigInteger.Parse("1000000000000000000"); // 1e18

    [Fact]
    public void TempoReturnsStartAmountAtDecayStartBlock()
    {
        var curve = Curve(new[] { TempoDecayBlocks }, new long[] { 100000000000000000 });
        NonLinearDutchDecayLib.Decay(curve, TempoStartAmount, DecayStartBlock, DecayStartBlock)
            .Should().Be(TempoStartAmount);
    }

    [Fact]
    public void TempoReturnsEndAmountAfterFullWindow()
    {
        BigInteger decayDelta = BigInteger.Parse("100000000000000000");
        var curve = Curve(new[] { TempoDecayBlocks }, new long[] { 100000000000000000 });
        NonLinearDutchDecayLib.Decay(curve, TempoStartAmount, DecayStartBlock, DecayStartBlock + TempoDecayBlocks)
            .Should().Be(TempoStartAmount - decayDelta);
    }

    [Fact]
    public void TempoLinearlyInterpolatesAtMidpoint()
    {
        BigInteger decayDelta = BigInteger.Parse("100000000000000000");
        var curve = Curve(new[] { TempoDecayBlocks }, new long[] { 100000000000000000 });
        NonLinearDutchDecayLib.Decay(curve, TempoStartAmount, DecayStartBlock, DecayStartBlock + TempoDecayBlocks / 2)
            .Should().Be(TempoStartAmount - decayDelta / 2);
    }

    [Fact]
    public void TempoClampsToEndAmountPastWindow()
    {
        BigInteger decayDelta = BigInteger.Parse("100000000000000000");
        var curve = Curve(new[] { TempoDecayBlocks }, new long[] { 100000000000000000 });
        NonLinearDutchDecayLib.Decay(curve, TempoStartAmount, DecayStartBlock, DecayStartBlock + TempoDecayBlocks * 10)
            .Should().Be(TempoStartAmount - decayDelta);
    }
}
