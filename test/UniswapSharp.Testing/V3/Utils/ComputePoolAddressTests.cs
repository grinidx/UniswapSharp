using UniswapSharp.Core.Entities;
using UniswapSharp.V3;
using UniswapSharp.V3.Utils;

namespace UniswapSharp.Testing.V3.Utils;

// Ported from sdks/v3-sdk/src/utils/computePoolAddress.test.ts.
// The third upstream case (zkSync CREATE2 via computeZksyncCreate2Address) is omitted:
// the ZKSYNC code path is not yet ported (ComputePoolAddress only supports the standard CREATE2).
public class ComputePoolAddressTests
{
    private const string FactoryAddress = "0x1111111111111111111111111111111111111111";

    private static readonly Token USDC = new(1, "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48", 18, "USDC", "USD Coin");
    private static readonly Token DAI = new(1, "0x6B175474E89094C44Da98b954EedeAC495271d0F", 18, "DAI", "DAI Stablecoin");

    [Fact]
    public void ShouldCorrectlyComputeThePoolAddress()
    {
        var result = ComputePoolAddress.Compute(FactoryAddress, USDC, DAI, Constants.FeeAmount.LOW);
        Assert.Equal("0x90B1b09A9715CaDbFD9331b3A7652B24BfBEfD32", result);
    }

    [Fact]
    public void ShouldBeIndependentOfTokenOrder()
    {
        var resultA = ComputePoolAddress.Compute(FactoryAddress, USDC, DAI, Constants.FeeAmount.LOW);
        var resultB = ComputePoolAddress.Compute(FactoryAddress, DAI, USDC, Constants.FeeAmount.LOW);
        Assert.Equal(resultA, resultB);
    }
}
