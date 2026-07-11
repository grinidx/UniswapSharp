using System.Numerics;
using UniswapSharp.V3.Utils;

namespace UniswapSharp.Testing.V3.Utils;

// Ported 1:1 from sdks/v3-sdk/src/utils/tickLibrary.test.ts (#getFeeGrowthInside)
public class TickLibraryTests
{
    private static readonly BigInteger Q128 = BigInteger.Pow(2, 128);
    private static readonly BigInteger Q127 = BigInteger.Pow(2, 127);

    private sealed class FeeGrowth : TickLibrary.FeeGrowthOutside
    {
        public BigInteger FeeGrowthOutside0X128 { get; set; }
        public BigInteger FeeGrowthOutside1X128 { get; set; }
    }

    private static FeeGrowth Outside(BigInteger v) => new() { FeeGrowthOutside0X128 = v, FeeGrowthOutside1X128 = v };

    [Fact]
    public void GetFeeGrowthInside_Zero()
    {
        var (inside0, inside1) = TickLibrary.GetFeeGrowthInside(
            Outside(0), Outside(0), -1, 1, 0, BigInteger.Zero, BigInteger.Zero);
        Assert.Equal(BigInteger.Zero, inside0);
        Assert.Equal(BigInteger.Zero, inside1);
    }

    [Fact]
    public void GetFeeGrowthInside_NonZeroAllInside()
    {
        var (inside0, inside1) = TickLibrary.GetFeeGrowthInside(
            Outside(0), Outside(0), -1, 1, 0, Q128, Q128);
        Assert.Equal(Q128, inside0);
        Assert.Equal(Q128, inside1);
    }

    [Fact]
    public void GetFeeGrowthInside_NonZeroAllOutside()
    {
        var (inside0, inside1) = TickLibrary.GetFeeGrowthInside(
            Outside(Q128), Outside(0), -1, 1, 0, Q128, Q128);
        Assert.Equal(BigInteger.Zero, inside0);
        Assert.Equal(BigInteger.Zero, inside1);
    }

    [Fact]
    public void GetFeeGrowthInside_NonZeroSomeOutside()
    {
        var (inside0, inside1) = TickLibrary.GetFeeGrowthInside(
            Outside(Q127), Outside(0), -1, 1, 0, Q128, Q128);
        Assert.Equal(Q127, inside0);
        Assert.Equal(Q127, inside1);
    }
}
