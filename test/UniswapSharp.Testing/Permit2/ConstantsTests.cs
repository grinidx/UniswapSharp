using System.Numerics;
using UniswapSharp.Permit2;

namespace UniswapSharp.Testing.Permit2;

// Port of permit2-sdk src/constants.test.ts.
public class ConstantsTests
{
    [Fact]
    public void MaxUint256()
    {
        Assert.Equal(BigInteger.Pow(2, 256) - 1, Constants.MaxUint256);
    }

    [Fact]
    public void MaxUint160()
    {
        Assert.Equal(BigInteger.Pow(2, 160) - 1, Constants.MaxUint160);
    }

    [Fact]
    public void MaxUint48()
    {
        Assert.Equal(BigInteger.Pow(2, 48) - 1, Constants.MaxUint48);
    }

    [Fact]
    public void InstantExpirationIsZero()
    {
        Assert.Equal(BigInteger.Zero, Constants.InstantExpiration);
    }

    // Not in the upstream .test.ts, but pins the exported addresses and the per-chain map.
    [Fact]
    public void Permit2AddressesAndPerChainMap()
    {
        Assert.Equal("0x000000000022D473030F116dDEE9F6B43aC78BA3", Constants.Permit2Address);
        Assert.Equal("0x000000000022D473030F116dDEE9F6B43aC78BA3", Constants.Permit2AddressFor(null));
        Assert.Equal("0x000000000022D473030F116dDEE9F6B43aC78BA3", Constants.Permit2AddressFor(1));
        Assert.Equal("0x0000000000225e31D15943971F47aD3022F714Fa", Constants.Permit2AddressFor(324));
    }
}
