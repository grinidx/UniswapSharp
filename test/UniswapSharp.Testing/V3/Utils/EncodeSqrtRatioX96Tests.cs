using System.Numerics;
using UniswapSharp.V3.Utils;

namespace UniswapSharp.Testing.V3.Utils;

// Ported 1:1 from sdks/v3-sdk/src/utils/encodeSqrtRatioX96.test.ts
public class EncodeSqrtRatioX96Tests
{
    // Q96 = 2^96
    private static readonly BigInteger Q96 = BigInteger.Pow(2, 96);

    [Fact]
    public void OneOverOne()
    {
        Assert.Equal(Q96, EncodeSqrtRatioX96.Encode(1, 1));
    }

    [Fact]
    public void HundredOverOne()
    {
        Assert.Equal(BigInteger.Parse("792281625142643375935439503360"), EncodeSqrtRatioX96.Encode(100, 1));
    }

    [Fact]
    public void OneOverHundred()
    {
        Assert.Equal(BigInteger.Parse("7922816251426433759354395033"), EncodeSqrtRatioX96.Encode(1, 100));
    }

    [Fact]
    public void OneEleven_Over_ThreeThirtyThree()
    {
        Assert.Equal(BigInteger.Parse("45742400955009932534161870629"), EncodeSqrtRatioX96.Encode(111, 333));
    }

    [Fact]
    public void ThreeThirtyThree_Over_OneEleven()
    {
        Assert.Equal(BigInteger.Parse("137227202865029797602485611888"), EncodeSqrtRatioX96.Encode(333, 111));
    }
}
