using System.Numerics;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.V3.Utils;
using UniswapSharp.V4;
using Pool = UniswapSharp.V4.Entities.Pool;
using Position = UniswapSharp.V4.Entities.Position;
using Tick = UniswapSharp.V3.Entities.Tick;

namespace UniswapSharp.Testing.V4.Entities;

// Ported 1:1 from sdks/v4-sdk/src/entities/position.test.ts (mintAmountsWithSlippage, 0% slippage).
public class PositionTests
{
    private static readonly Token USDC = new(1, "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48", 6, "USDC", "USD Coin");
    private static readonly Token DAI = new(1, "0x6B175474E89094C44Da98b954EedeAC495271d0F", 18, "DAI", "DAI Stablecoin");

    private static readonly BigInteger POOL_SQRT_RATIO_START =
        EncodeSqrtRatioX96.Encode(new BigInteger(100_000_000), BigInteger.Parse("100000000000000000000"));
    private static readonly int POOL_TICK_CURRENT = TickMath.GetTickAtSqrtRatio(POOL_SQRT_RATIO_START);
    private const int TICK_SPACING = Constants.TICK_SPACING_TEN;

    private static readonly Pool DAI_USDC_POOL = new(
        DAI, USDC, 500, TICK_SPACING, Constants.ADDRESS_ZERO, POOL_SQRT_RATIO_START, 0, POOL_TICK_CURRENT, new List<Tick>());

    [Fact]
    public void MintAmountsWithSlippage_0Percent_PositionsBelow()
    {
        int tickLower = NearestUsableTick.Find(POOL_TICK_CURRENT, TICK_SPACING) + TICK_SPACING;
        int tickUpper = NearestUsableTick.Find(POOL_TICK_CURRENT, TICK_SPACING) + TICK_SPACING * 2;
        var liquidity = MaxLiquidity.MaxLiquidityForAmounts(
            DAI_USDC_POOL.SqrtRatioX96, TickMath.GetSqrtRatioAtTick(tickLower), TickMath.GetSqrtRatioAtTick(tickUpper),
            BigInteger.Parse("49949961958869841738198"), BigInteger.Zero, true);

        var position = new Position(DAI_USDC_POOL, liquidity, tickLower, tickUpper);
        var (amount0, amount1) = position.MintAmountsWithSlippage(new Percent(0));

        Assert.Equal(BigInteger.Parse("49949961958869841738198"), amount0);
        Assert.Equal(BigInteger.Zero, amount1);
    }

    [Fact]
    public void MintAmountsWithSlippage_0Percent_PositionsAbove()
    {
        int tickLower = NearestUsableTick.Find(POOL_TICK_CURRENT, TICK_SPACING) - TICK_SPACING * 2;
        int tickUpper = NearestUsableTick.Find(POOL_TICK_CURRENT, TICK_SPACING) - TICK_SPACING;
        var liquidity = MaxLiquidity.MaxLiquidityForAmounts(
            DAI_USDC_POOL.SqrtRatioX96, TickMath.GetSqrtRatioAtTick(tickLower), TickMath.GetSqrtRatioAtTick(tickUpper),
            BigInteger.Zero, BigInteger.Parse("49970077053"), true);

        var position = new Position(DAI_USDC_POOL, liquidity, tickLower, tickUpper);
        var (amount0, amount1) = position.MintAmountsWithSlippage(new Percent(0));

        Assert.Equal(BigInteger.Zero, amount0);
        Assert.Equal(BigInteger.Parse("49970077053"), amount1);
    }

    [Fact]
    public void MintAmountsWithSlippage_0Percent_PositionsWithin()
    {
        int tickLower = NearestUsableTick.Find(POOL_TICK_CURRENT, TICK_SPACING) - TICK_SPACING * 2;
        int tickUpper = NearestUsableTick.Find(POOL_TICK_CURRENT, TICK_SPACING) + TICK_SPACING * 2;
        var liquidity = MaxLiquidity.MaxLiquidityForAmounts(
            DAI_USDC_POOL.SqrtRatioX96, TickMath.GetSqrtRatioAtTick(tickLower), TickMath.GetSqrtRatioAtTick(tickUpper),
            BigInteger.Parse("120054069145287995740584"), BigInteger.Parse("79831926243"), true);

        var position = new Position(DAI_USDC_POOL, liquidity, tickLower, tickUpper);
        var (amount0, amount1) = position.MintAmountsWithSlippage(new Percent(0));

        Assert.Equal(BigInteger.Parse("120054069145287995740584"), amount0);
        Assert.Equal(BigInteger.Parse("79831926243"), amount1);
    }
}
