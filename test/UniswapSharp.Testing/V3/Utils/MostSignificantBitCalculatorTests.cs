using System.Numerics;
using UniswapSharp.Core;
using UniswapSharp.V3.Utils;

namespace UniswapSharp.Testing.V3.Utils;

// Ported 1:1 from sdks/v3-sdk/src/utils/mostSignificantBit.test.ts
public class MostSignificantBitCalculatorTests
{
    [Fact]
    public void ThrowsForZero()
    {
        Assert.Throws<ArgumentException>(() => MostSignificantBitCalculator.MostSignificantBit(BigInteger.Zero));
    }

    [Fact]
    public void CorrectValueForEveryPowerOf2()
    {
        for (int i = 1; i < 256; i++)
        {
            BigInteger x = BigInteger.Pow(2, i);
            Assert.Equal(i, MostSignificantBitCalculator.MostSignificantBit(x));
        }
    }

    [Fact]
    public void CorrectValueForEveryPowerOf2MinusOne()
    {
        for (int i = 2; i < 256; i++)
        {
            BigInteger x = BigInteger.Pow(2, i) - 1;
            Assert.Equal(i - 1, MostSignificantBitCalculator.MostSignificantBit(x));
        }
    }

    [Fact]
    public void SucceedsForMaxUint256()
    {
        Assert.Equal(255, MostSignificantBitCalculator.MostSignificantBit(Constants.MaxUint256));
    }

    [Fact]
    public void ThrowsForMaxUint256PlusOne()
    {
        Assert.Throws<ArgumentException>(() => MostSignificantBitCalculator.MostSignificantBit(Constants.MaxUint256 + BigInteger.One));
    }
}
