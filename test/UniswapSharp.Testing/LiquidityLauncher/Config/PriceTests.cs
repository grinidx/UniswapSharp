using System.Numerics;
using UniswapSharp.LiquidityLauncher;
using UniswapSharp.LiquidityLauncher.Config;

namespace UniswapSharp.Testing.LiquidityLauncher.Config;

// Ported from sdks/liquidity-launcher-sdk/src/config/price.test.ts.
public class PriceTests
{
    // ---- floorPriceToX96 ----

    [Fact]
    public void FloorPriceToX96_EncodesA1To1PriceAsExactly2Pow96() =>
        Assert.Equal(Constants.Q96, Price.FloorPriceToX96("1", 18, 18));

    [Fact]
    public void FloorPriceToX96_ScalesByTheCurrencyTokenDecimalDifference() =>
        // 1 token priced at 1 USDC (6 decimals) vs an 18-decimal token.
        Assert.Equal(Constants.Q96 / BigInteger.Pow(10, 12), Price.FloorPriceToX96("1", 18, 6));

    [Fact]
    public void FloorPriceToX96_RejectsANonNumericFloorPrice() =>
        Assert.Throws<LauncherSdkError>(() => Price.FloorPriceToX96("abc", 18, 18));

    // ---- requiredCurrencyRaised ----

    [Fact]
    public void RequiredCurrencyRaised_IsSupplyTimesFloorPriceOver2Pow96() =>
        Assert.Equal(new BigInteger(5_000), Price.RequiredCurrencyRaised(Constants.Q96, 5_000));

    // ---- deriveAuctionPricing ----

    [Fact]
    public void DeriveAuctionPricing_DerivesTickSpacingAndSnapsFloorDownToABoundary()
    {
        var (floorPriceX96, tickSpacing) = Price.DeriveAuctionPricing(12_345);
        Assert.Equal(new BigInteger(123), tickSpacing);
        Assert.Equal(new BigInteger(12_300), floorPriceX96);
        Assert.Equal(BigInteger.Zero, floorPriceX96 % tickSpacing);
    }
}
