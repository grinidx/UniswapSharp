using System.Numerics;
using AwesomeAssertions;
using UniswapSharp.UniswapX.Utils;

namespace UniswapSharp.Testing.UniswapX;

// Port of uniswapx-sdk src/utils/dutchDecay.test.ts.
public class DutchDecayTests
{
    [Fact]
    public void ReturnsEndAmountIfDecayIsOver()
    {
        BigInteger endAmount = BigInteger.Parse("100000000");
        DutchDecay.GetDecayedAmount(
            new DutchDecayConfig(endAmount / 2, endAmount, 1, 10), 11)
            .Should().Be(endAmount);
    }

    [Fact]
    public void ReturnsEndAmountIfEqToDecayEndTime()
    {
        BigInteger endAmount = BigInteger.Parse("100000000");
        DutchDecay.GetDecayedAmount(
            new DutchDecayConfig(endAmount / 2, endAmount, 1, 10), 10)
            .Should().Be(endAmount);
    }

    [Fact]
    public void ReturnsStartAmountIfDecayHasntStarted()
    {
        BigInteger startAmount = BigInteger.Parse("100000000");
        DutchDecay.GetDecayedAmount(
            new DutchDecayConfig(startAmount, startAmount * 2, 10, 100), 9)
            .Should().Be(startAmount);
    }

    [Fact]
    public void ReturnsStartAmountIfEqToDecayStartTime()
    {
        BigInteger startAmount = BigInteger.Parse("100000000");
        DutchDecay.GetDecayedAmount(
            new DutchDecayConfig(startAmount, startAmount * 2, 10, 100), 10)
            .Should().Be(startAmount);
    }

    [Fact]
    public void DecaysLinearlyUpwards()
    {
        BigInteger startAmount = BigInteger.Parse("100000000");
        DutchDecay.GetDecayedAmount(
            new DutchDecayConfig(startAmount, startAmount * 2, 10, 20), 15)
            .Should().Be(startAmount * 3 / 2);
    }

    [Fact]
    public void DecaysLinearlyDownwards()
    {
        BigInteger endAmount = BigInteger.Parse("100000000");
        DutchDecay.GetDecayedAmount(
            new DutchDecayConfig(endAmount * 2, endAmount, 10, 20), 15)
            .Should().Be(endAmount * 3 / 2);
    }
}
