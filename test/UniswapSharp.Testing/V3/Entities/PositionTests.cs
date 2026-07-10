using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using UniswapSharp.Core.Entities;
using UniswapSharp.V3.Entities;
using UniswapSharp.V3.Utils;
using static UniswapSharp.V3.Constants;
// ReSharper disable InconsistentNaming

namespace UniswapSharp.Testing.V3.Entities;

public class PositionTests(ITestOutputHelper output) :
    XunitContextBase(output)
{
    private static readonly Token USDC = new Token(1, "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48", 6, "USDC", "USD Coin");
    private static readonly Token DAI = new Token(1, "0x6B175474E89094C44Da98b954EedeAC495271d0F", 18, "DAI", "DAI Stablecoin");
    private static readonly BigInteger POOL_SQRT_RATIO_START = EncodeSqrtRatioX96.Encode(BigInteger.Parse("100e6", NumberStyles.AllowExponent), BigInteger.Parse("100e18", NumberStyles.AllowExponent));
    private static readonly int POOL_TICK_CURRENT = TickMath.GetTickAtSqrtRatio(POOL_SQRT_RATIO_START);
    private static readonly int TICK_SPACING = TICK_SPACINGS[FeeAmount.LOW];
    private static readonly Pool DAI_USDC_POOL = new Pool(DAI, USDC, FeeAmount.LOW, POOL_SQRT_RATIO_START, 0, POOL_TICK_CURRENT, new int[] { });



    [Fact]
    public void CanBeConstructedAroundZeroTick()
    {
        var position = new Position(DAI_USDC_POOL, -10, 10, 1);
        Assert.Equal(BigInteger.One, position.Liquidity);
    }

    [Fact]
    public void CanUseMinAndMaxTicks()
    {
        var position = new Position(DAI_USDC_POOL, NearestUsableTick.Find(TickMath.MIN_TICK, TICK_SPACING), NearestUsableTick.Find(TickMath.MAX_TICK, TICK_SPACING), 1);
        Assert.Equal(BigInteger.One, position.Liquidity);
    }

    [Fact]
    public void TickLowerMustBeLessThanTickUpper()
    {
        Assert.Throws<ArgumentException>(() => new Position(DAI_USDC_POOL, 1, 10, -10));
    }

    [Fact]
    public void TickLowerCannotEqualTickUpper()
    {
        Assert.Throws<ArgumentException>(() => new Position(DAI_USDC_POOL, 1, -10, -10));
    }

    [Fact]
    public void TickLowerMustBeMultipleOfTickSpacing()
    {
        Assert.Throws<ArgumentException>(() => new Position(DAI_USDC_POOL, 1, -5, 10));
    }

    [Fact]
    public void TickLowerMustBeGreaterThanMinTick()
    {
        Assert.Throws<ArgumentException>(() => new Position(DAI_USDC_POOL, 1, NearestUsableTick.Find(TickMath.MIN_TICK, TICK_SPACING) - TICK_SPACING, 10));
    }

    [Fact]
    public void TickUpperMustBeMultipleOfTickSpacing()
    {
        Assert.Throws<ArgumentException>(() => new Position(DAI_USDC_POOL, 1, -10, 15));
    }

    [Fact]
    public void TickUpperMustBeLessThanMaxTick()
    {
        Assert.Throws<ArgumentException>(() => new Position(DAI_USDC_POOL, 1, -10, NearestUsableTick.Find(TickMath.MAX_TICK, TICK_SPACING) + TICK_SPACING));
    }

    // Additional tests for amount0, amount1, mintAmountsWithSlippage, burnAmountsWithSlippage, and mintAmounts can be added similarly
}
