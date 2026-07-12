using System.Numerics;
using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.Core.Utils;
using UniswapSharp.Router.Entities.MixedRoute;
using UniswapSharp.V3.Utils;
using V2Pair = UniswapSharp.V2.Entities.Pair;
using V3Pool = UniswapSharp.V3.Entities.Pool;
using V4Pool = UniswapSharp.V4.Entities.Pool;
using Tick = UniswapSharp.V3.Entities.Tick;
using V3Constants = UniswapSharp.V3.Constants;
using FeeAmount = UniswapSharp.V3.Constants.FeeAmount;

namespace UniswapSharp.Testing.Router.Entities.MixedRoute;

// Ported 1:1 from sdks/router-sdk/src/entities/mixedRoute/trade.test.ts
public class MixedRouteTradeTests
{
    private static readonly Ether ETHER = Ether.OnChain(1);
    private static readonly Token token0 = new(1, "0x0000000000000000000000000000000000000001", 18, "t0", "token0");
    private static readonly Token token1 = new(1, "0x0000000000000000000000000000000000000002", 18, "t1", "token1");
    private static readonly Token token2 = new(1, "0x0000000000000000000000000000000000000003", 18, "t2", "token2");
    private static readonly Token token3 = new(1, "0x0000000000000000000000000000000000000004", 18, "t3", "token3");
    private static readonly Token weth = Weth9.Tokens[1];

    private static CurrencyAmount<Token> CA(Token t, long v) => CurrencyAmount<Token>.FromRawAmount(t, v);
    private static CurrencyAmount<Ether> CAE(long v) => CurrencyAmount<Ether>.FromRawAmount(ETHER, v);
    private static CurrencyAmount<BaseCurrency> RB(BaseCurrency c, long v) => CurrencyAmount<BaseCurrency>.FromRawAmount(c, v);

    private static object V2StylePool(CurrencyAmount<BaseCurrency> reserve0, CurrencyAmount<BaseCurrency> reserve1, bool v4Pool = false)
    {
        var currency0 = reserve0.Currency;
        var currency1 = reserve1.Currency;
        const FeeAmount fee = FeeAmount.MEDIUM;
        var sqrtRatioX96 = EncodeSqrtRatioX96.Encode(reserve1.Quotient, reserve0.Quotient);
        var liquidity = (reserve0.Quotient * reserve1.Quotient).Sqrt();
        var tick = TickMath.GetTickAtSqrtRatio(sqrtRatioX96);
        var tickSpacing = V3Constants.TICK_SPACINGS[fee];
        var tickBitmap = new List<Tick>
        {
            new(NearestUsableTick.Find(TickMath.MIN_TICK, tickSpacing), liquidity, liquidity),
            new(NearestUsableTick.Find(TickMath.MAX_TICK, tickSpacing), liquidity * -1, liquidity),
        };
        if (!v4Pool)
        {
            return new V3Pool(currency0.Wrapped(), currency1.Wrapped(), fee, sqrtRatioX96, liquidity, tick, tickBitmap);
        }
        return new V4Pool(currency0, currency1, (int)fee, tickSpacing, UniswapSharp.Router.Constants.ADDRESS_ZERO, sqrtRatioX96, liquidity, tick, tickBitmap);
    }

    private static readonly object pool_v3_0_1 = V2StylePool(RB(token0, 100000), RB(token1, 100000));
    private static readonly object pool_v3_0_2 = V2StylePool(RB(token0, 100000), RB(token2, 110000));
    private static readonly object pool_v3_0_3 = V2StylePool(RB(token0, 100000), RB(token3, 90000));
    private static readonly object pool_v3_1_2 = V2StylePool(RB(token1, 120000), RB(token2, 100000));
    private static readonly object pool_v3_1_3 = V2StylePool(RB(token1, 120000), RB(token3, 130000));
    private static readonly object pool_v3_weth_0 = V2StylePool(RB(weth, 100000), RB(token0, 100000));
    private static readonly object pool_v3_weth_1 = V2StylePool(RB(weth, 100000), RB(token1, 100000));
    private static readonly object pool_v3_weth_2 = V2StylePool(RB(weth, 100000), RB(token2, 100000));

    private static V2Pair Pair(Token a, int aAmt, Token b, int bAmt) => new(CA(a, aAmt), CA(b, bAmt));
    private static readonly V2Pair pair_0_1 = Pair(token0, 1000, token1, 1000);
    private static readonly V2Pair pair_0_2 = Pair(token0, 1000, token2, 1100);
    private static readonly V2Pair pair_0_3 = Pair(token0, 1000, token3, 900);
    private static readonly V2Pair pair_1_2 = Pair(token1, 1200, token2, 1000);
    private static readonly V2Pair pair_1_3 = Pair(token1, 1200, token3, 1300);
    private static readonly V2Pair pair_weth_0 = Pair(weth, 1000, token0, 1000);
    private static readonly V2Pair empty_pair_0_1 = Pair(token0, 0, token1, 0);

    private static MixedRouteSDK<TIn, TOut> Route<TIn, TOut>(IEnumerable<object> pools, TIn input, TOut output)
        where TIn : BaseCurrency where TOut : BaseCurrency => new(pools.ToList(), input, output);

    private static void AssertPath(BaseCurrency[] expected, List<BaseCurrency> actual)
    {
        Assert.Equal(expected.Length, actual.Count);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.True(actual[i].Equals(expected[i]), $"path[{i}] mismatch: expected {expected[i].Symbol}");
        }
    }

    private static void AssertPrice<TB, TQ>(Price<TB, TQ> price, BaseCurrency b, BaseCurrency q, int num, int den)
        where TB : BaseCurrency where TQ : BaseCurrency
    {
        Assert.True(price.BaseCurrency.Equals(b));
        Assert.True(price.QuoteCurrency.Equals(q));
        Assert.Equal((BigInteger)num, price.Numerator);
        Assert.Equal((BigInteger)den, price.Denominator);
    }

    // ---- #fromRoute (v3) ----
    [Fact]
    public async Task FromRoute_EtherInput()
    {
        var trade = await MixedRouteTrade<Ether, Token>.FromRoute(Route(new[] { pool_v3_weth_0 }, ETHER, token0), CAE(10000), TradeType.EXACT_INPUT);
        Assert.True(trade.InputAmount.Currency.Equals(ETHER));
        Assert.True(trade.OutputAmount.Currency.Equals(token0));
    }

    [Fact]
    public async Task FromRoute_EtherOutput()
    {
        var trade = await MixedRouteTrade<Token, Ether>.FromRoute(Route(new[] { pool_v3_weth_0 }, token0, ETHER), CA(token0, 10000), TradeType.EXACT_INPUT);
        Assert.True(trade.InputAmount.Currency.Equals(token0));
        Assert.True(trade.OutputAmount.Currency.Equals(ETHER));
    }

    [Fact]
    public async Task FromRoute_ThrowsForExactOutput()
    {
        // upstream passes a token0 amount here, but TRADE_TYPE is checked before the input currency, so the amount is irrelevant
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            MixedRouteTrade<Ether, Token>.FromRoute(Route(new[] { pool_v3_weth_0 }, ETHER, token0), CAE(10000), TradeType.EXACT_OUTPUT));
        Assert.Equal("TRADE_TYPE", ex.Message);
    }

    // ---- #fromRoutes ----
    [Fact]
    public async Task FromRoutes_EtherInput()
    {
        var trade = await MixedRouteTrade<Ether, Token>.FromRoutes(
            new List<(CurrencyAmount<Ether>, MixedRouteSDK<Ether, Token>)> { (CAE(10000), Route(new[] { pool_v3_weth_0 }, ETHER, token0)) },
            TradeType.EXACT_INPUT);
        Assert.True(trade.InputAmount.Currency.Equals(ETHER));
        Assert.True(trade.OutputAmount.Currency.Equals(token0));
    }

    [Fact]
    public async Task FromRoutes_EtherOutput()
    {
        var trade = await MixedRouteTrade<Token, Ether>.FromRoutes(
            new List<(CurrencyAmount<Token>, MixedRouteSDK<Token, Ether>)>
            {
                (CA(token0, 3000), Route(new[] { pool_v3_weth_0 }, token0, ETHER)),
                (CA(token0, 7000), Route(new[] { pool_v3_0_1, pool_v3_weth_1 }, token0, ETHER)),
            },
            TradeType.EXACT_INPUT);
        Assert.True(trade.InputAmount.Currency.Equals(token0));
        Assert.True(trade.OutputAmount.Currency.Equals(ETHER));
    }

    [Fact]
    public async Task FromRoutes_ThrowsIfPoolsReused()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => MixedRouteTrade<Token, Ether>.FromRoutes(
            new List<(CurrencyAmount<Token>, MixedRouteSDK<Token, Ether>)>
            {
                (CA(token0, 4500), Route(new[] { pool_v3_0_1, pool_v3_weth_1 }, token0, ETHER)),
                (CA(token0, 5500), Route(new[] { pool_v3_0_1, pool_v3_1_2, pool_v3_weth_2 }, token0, ETHER)),
            },
            TradeType.EXACT_INPUT));
        Assert.Equal("POOLS_DUPLICATED", ex.Message);
    }

    [Fact]
    public async Task FromRoutes_ThrowsIfExactOutput()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => MixedRouteTrade<Ether, Token>.FromRoutes(
            new List<(CurrencyAmount<Ether>, MixedRouteSDK<Ether, Token>)> { (CAE(10000), Route(new[] { pool_v3_weth_0 }, ETHER, token0)) },
            TradeType.EXACT_OUTPUT));
        Assert.Equal("TRADE_TYPE", ex.Message);
    }

    // ---- #createUncheckedTrade ----
    [Fact]
    public void CreateUncheckedTrade_ThrowsInputMismatch()
    {
        var ex = Assert.Throws<ArgumentException>(() => MixedRouteTrade<Token, Token>.CreateUncheckedTrade(
            Route(new[] { pool_v3_0_1 }, token0, token1), CA(token2, 10000), CA(token1, 10000), TradeType.EXACT_INPUT));
        Assert.Equal("INPUT_CURRENCY_MATCH", ex.Message);
    }

    [Fact]
    public void CreateUncheckedTrade_ThrowsOutputMismatch()
    {
        var ex = Assert.Throws<ArgumentException>(() => MixedRouteTrade<Token, Token>.CreateUncheckedTrade(
            Route(new[] { pool_v3_0_1 }, token0, token1), CA(token0, 10000), CA(token2, 10000), TradeType.EXACT_INPUT));
        Assert.Equal("OUTPUT_CURRENCY_MATCH", ex.Message);
    }

    [Fact]
    public void CreateUncheckedTrade_ThrowsExactOutput()
    {
        var ex = Assert.Throws<ArgumentException>(() => MixedRouteTrade<Token, Token>.CreateUncheckedTrade(
            Route(new[] { pool_v3_0_1 }, token1, token0), CA(token1, 10000), CA(token0, 100000), TradeType.EXACT_OUTPUT));
        Assert.Equal("TRADE_TYPE", ex.Message);
    }

    [Fact]
    public void CreateUncheckedTrade_ExactInput()
    {
        MixedRouteTrade<Token, Token>.CreateUncheckedTrade(
            Route(new[] { pool_v3_0_1 }, token0, token1), CA(token0, 10000), CA(token1, 100000), TradeType.EXACT_INPUT);
    }

    // ---- #createUncheckedTradeWithMultipleRoutes ----
    private static MixedRouteSwap<Token, Token> Swap(MixedRouteSDK<Token, Token> route, CurrencyAmount<Token> input, CurrencyAmount<Token> output) => new(route, input, output);

    [Fact]
    public void CreateUncheckedMulti_ThrowsInputMismatch()
    {
        var ex = Assert.Throws<ArgumentException>(() => MixedRouteTrade<Token, Token>.CreateUncheckedTradeWithMultipleRoutes(
            new List<MixedRouteSwap<Token, Token>>
            {
                Swap(Route(new[] { pool_v3_1_2 }, token2, token1), CA(token2, 2000), CA(token1, 2000)),
                Swap(Route(new[] { pool_v3_0_1 }, token0, token1), CA(token2, 8000), CA(token1, 8000)),
            },
            TradeType.EXACT_INPUT));
        Assert.Equal("INPUT_CURRENCY_MATCH", ex.Message);
    }

    [Fact]
    public void CreateUncheckedMulti_ThrowsOutputMismatch()
    {
        var ex = Assert.Throws<ArgumentException>(() => MixedRouteTrade<Token, Token>.CreateUncheckedTradeWithMultipleRoutes(
            new List<MixedRouteSwap<Token, Token>>
            {
                Swap(Route(new[] { pool_v3_0_2 }, token0, token2), CA(token0, 10000), CA(token2, 10000)),
                Swap(Route(new[] { pool_v3_0_1 }, token0, token1), CA(token0, 10000), CA(token2, 10000)),
            },
            TradeType.EXACT_INPUT));
        Assert.Equal("OUTPUT_CURRENCY_MATCH", ex.Message);
    }

    [Fact]
    public void CreateUncheckedMulti_ThrowsExactOutput()
    {
        var ex = Assert.Throws<ArgumentException>(() => MixedRouteTrade<Token, Token>.CreateUncheckedTradeWithMultipleRoutes(
            new List<MixedRouteSwap<Token, Token>>
            {
                Swap(Route(new[] { pool_v3_0_1 }, token0, token1), CA(token0, 5000), CA(token1, 50000)),
                Swap(Route(new[] { pool_v3_0_2, pool_v3_1_2 }, token0, token1), CA(token0, 5000), CA(token1, 50000)),
            },
            TradeType.EXACT_OUTPUT));
        Assert.Equal("TRADE_TYPE", ex.Message);
    }

    [Fact]
    public void CreateUncheckedMulti_ExactInput()
    {
        MixedRouteTrade<Token, Token>.CreateUncheckedTradeWithMultipleRoutes(
            new List<MixedRouteSwap<Token, Token>>
            {
                Swap(Route(new[] { pool_v3_0_1 }, token0, token1), CA(token0, 5000), CA(token1, 50000)),
                Swap(Route(new[] { pool_v3_0_2, pool_v3_1_2 }, token0, token1), CA(token0, 5000), CA(token1, 50000)),
            },
            TradeType.EXACT_INPUT);
    }

    // ---- #route and #swaps ----
    private static MixedRouteTrade<Token, Token> SingleRoute() => MixedRouteTrade<Token, Token>.CreateUncheckedTrade(
        Route(new[] { pool_v3_0_1, pool_v3_1_2 }, token0, token2), CA(token0, 100), CA(token2, 69), TradeType.EXACT_INPUT);

    private static MixedRouteTrade<Token, Token> MultiRoute() => MixedRouteTrade<Token, Token>.CreateUncheckedTradeWithMultipleRoutes(
        new List<MixedRouteSwap<Token, Token>>
        {
            Swap(Route(new[] { pool_v3_0_1, pool_v3_1_2 }, token0, token2), CA(token0, 50), CA(token2, 35)),
            Swap(Route(new[] { pool_v3_0_2 }, token0, token2), CA(token0, 50), CA(token2, 34)),
        },
        TradeType.EXACT_INPUT);

    [Fact]
    public void SwapsAvailable()
    {
        Assert.Single(SingleRoute().Swaps);
        Assert.Equal(2, MultiRoute().Swaps.Count);
    }

    [Fact]
    public void Route_ThrowsOnMultiRoute()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => MultiRoute().Route);
        Assert.Equal("MULTIPLE_ROUTES", ex.Message);
    }

    // ---- #worstExecutionPrice ----
    private static MixedRouteTrade<Token, Token> ExactIn() => SingleRoute();

    [Fact]
    public void WorstExecutionPrice_ThrowsIfLessThanZero()
    {
        Assert.Throws<ArgumentException>(() => ExactIn().MinimumAmountOut(new Percent(-1, 100)));
    }

    [Fact]
    public void WorstExecutionPrice_ReturnsExactIfZero()
    {
        var exactIn = ExactIn();
        Assert.True(exactIn.WorstExecutionPrice(new Percent(0, 100)).Equals(exactIn.ExecutionPrice));
    }

    [Fact]
    public void WorstExecutionPrice_Nonzero()
    {
        var exactIn = ExactIn();
        AssertPrice(exactIn.WorstExecutionPrice(new Percent(0, 100)), token0, token2, 69, 100);
        AssertPrice(exactIn.WorstExecutionPrice(new Percent(5, 100)), token0, token2, 65, 100);
        AssertPrice(exactIn.WorstExecutionPrice(new Percent(200, 100)), token0, token2, 0, 100);
    }

    [Fact]
    public void WorstExecutionPrice_NonzeroMultiRoute()
    {
        var m = MixedRouteTrade<Token, Token>.CreateUncheckedTradeWithMultipleRoutes(
            new List<MixedRouteSwap<Token, Token>>
            {
                Swap(Route(new[] { pool_v3_0_1, pool_v3_1_2 }, token0, token2), CA(token0, 50), CA(token2, 35)),
                Swap(Route(new[] { pool_v3_0_2 }, token0, token2), CA(token0, 50), CA(token2, 34)),
            },
            TradeType.EXACT_INPUT);
        AssertPrice(m.WorstExecutionPrice(new Percent(0, 100)), token0, token2, 69, 100);
        AssertPrice(m.WorstExecutionPrice(new Percent(5, 100)), token0, token2, 65, 100);
        AssertPrice(m.WorstExecutionPrice(new Percent(200, 100)), token0, token2, 0, 100);
    }

    // ---- #priceImpact ----
    [Fact]
    public void PriceImpact_V3_Correct()
    {
        var exactIn = MixedRouteTrade<Token, Token>.CreateUncheckedTradeWithMultipleRoutes(
            new List<MixedRouteSwap<Token, Token>> { Swap(Route(new[] { pool_v3_0_1, pool_v3_1_2 }, token0, token2), CA(token0, 100), CA(token2, 69)) },
            TradeType.EXACT_INPUT);
        Assert.Equal("17.2", exactIn.PriceImpact.ToSignificant(3));
    }

    [Fact]
    public void PriceImpact_V3_CorrectMultiRoute()
    {
        var exactIn = MixedRouteTrade<Token, Token>.CreateUncheckedTradeWithMultipleRoutes(
            new List<MixedRouteSwap<Token, Token>>
            {
                Swap(Route(new[] { pool_v3_0_1, pool_v3_1_2 }, token0, token2), CA(token0, 90), CA(token2, 62)),
                Swap(Route(new[] { pool_v3_0_2 }, token0, token2), CA(token0, 10), CA(token2, 7)),
            },
            TradeType.EXACT_INPUT);
        Assert.Equal("19.8", exactIn.PriceImpact.ToSignificant(3));
    }

    [Fact]
    public void PriceImpact_Mixed_Correct()
    {
        var exactIn = MixedRouteTrade<Token, Token>.CreateUncheckedTradeWithMultipleRoutes(
            new List<MixedRouteSwap<Token, Token>> { Swap(Route(new object[] { pool_v3_0_1, pair_1_2 }, token0, token2), CA(token0, 100), CA(token2, 69)) },
            TradeType.EXACT_INPUT);
        Assert.Equal("17.2", exactIn.PriceImpact.ToSignificant(3));
    }

    [Fact]
    public void PriceImpact_Mixed_CorrectMultiRoute()
    {
        var exactIn = MixedRouteTrade<Token, Token>.CreateUncheckedTradeWithMultipleRoutes(
            new List<MixedRouteSwap<Token, Token>>
            {
                Swap(Route(new object[] { pool_v3_0_1, pair_1_2 }, token0, token2), CA(token0, 90), CA(token2, 62)),
                Swap(Route(new[] { pool_v3_0_2 }, token0, token2), CA(token0, 10), CA(token2, 7)),
            },
            TradeType.EXACT_INPUT);
        Assert.Equal("19.8", exactIn.PriceImpact.ToSignificant(3));
    }

    // ---- #bestTradeExactIn (v3) ----
    [Fact]
    public async Task BestTradeExactIn_ThrowsEmptyPools()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => MixedRouteTrade<Token, Token>.BestTradeExactIn(new List<object>(), CA(token0, 10000), token2));
        Assert.Equal("POOLS", ex.Message);
    }

    [Fact]
    public async Task BestTradeExactIn_ThrowsMaxHopsZero()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => MixedRouteTrade<Token, Token>.BestTradeExactIn(
            new List<object> { pool_v3_0_2 }, CA(token0, 10000), token2, new MixedRouteTrade<Token, Token>.BestTradeOptions { MaxHops = 0 }));
        Assert.Equal("MAX_HOPS", ex.Message);
    }

    [Fact]
    public async Task BestTradeExactIn_ProvidesBestRoute()
    {
        var result = await MixedRouteTrade<Token, Token>.BestTradeExactIn(new List<object> { pool_v3_0_1, pool_v3_0_2, pool_v3_1_2 }, CA(token0, 10000), token2);
        Assert.Equal(2, result.Count);
        Assert.Single(result[0].Swaps[0].Route.Pools);
        AssertPath(new BaseCurrency[] { token0, token2 }, result[0].Swaps[0].Route.Path);
        Assert.True(result[0].InputAmount.Equals(CA(token0, 10000)));
        Assert.True(result[0].OutputAmount.Equals(CA(token2, 9971)));
        Assert.Equal(2, result[1].Swaps[0].Route.Pools.Count);
        AssertPath(new BaseCurrency[] { token0, token1, token2 }, result[1].Swaps[0].Route.Path);
        Assert.True(result[1].InputAmount.Equals(CA(token0, 10000)));
        Assert.True(result[1].OutputAmount.Equals(CA(token2, 7004)));
    }

    [Fact]
    public async Task BestTradeExactIn_RespectsMaxHops()
    {
        var result = await MixedRouteTrade<Token, Token>.BestTradeExactIn(new List<object> { pool_v3_0_1, pool_v3_0_2, pool_v3_1_2 }, CA(token0, 10), token2, new MixedRouteTrade<Token, Token>.BestTradeOptions { MaxHops = 1 });
        Assert.Single(result);
        Assert.Single(result[0].Swaps[0].Route.Pools);
        AssertPath(new BaseCurrency[] { token0, token2 }, result[0].Swaps[0].Route.Path);
    }

    [Fact]
    public async Task BestTradeExactIn_InsufficientInput()
    {
        var result = await MixedRouteTrade<Token, Token>.BestTradeExactIn(new List<object> { pool_v3_0_1, pool_v3_0_2, pool_v3_1_2 }, CA(token0, 1), token2);
        Assert.Equal(2, result.Count);
        Assert.Single(result[0].Swaps[0].Route.Pools);
        AssertPath(new BaseCurrency[] { token0, token2 }, result[0].Swaps[0].Route.Path);
        Assert.True(result[0].OutputAmount.Currency.Equals(token2));
        Assert.Equal(BigInteger.Zero, result[0].OutputAmount.Quotient);
    }

    [Fact]
    public async Task BestTradeExactIn_RespectsN()
    {
        var result = await MixedRouteTrade<Token, Token>.BestTradeExactIn(new List<object> { pool_v3_0_1, pool_v3_0_2, pool_v3_1_2 }, CA(token0, 10), token2, new MixedRouteTrade<Token, Token>.BestTradeOptions { MaxNumResults = 1 });
        Assert.Single(result);
    }

    [Fact]
    public async Task BestTradeExactIn_NoPath()
    {
        var result = await MixedRouteTrade<Token, Token>.BestTradeExactIn(new List<object> { pool_v3_0_1, pool_v3_0_3, pool_v3_1_3 }, CA(token0, 10), token2);
        Assert.Empty(result);
    }

    [Fact]
    public async Task BestTradeExactIn_EtherInput()
    {
        var result = await MixedRouteTrade<Ether, Token>.BestTradeExactIn(new List<object> { pool_v3_weth_0, pool_v3_0_1, pool_v3_0_3, pool_v3_1_3 }, CAE(100), token3);
        Assert.Equal(2, result.Count);
        Assert.True(result[0].InputAmount.Currency.Equals(ETHER));
        AssertPath(new BaseCurrency[] { weth, token0, token1, token3 }, result[0].Swaps[0].Route.Path);
        Assert.True(result[0].OutputAmount.Currency.Equals(token3));
        Assert.True(result[1].InputAmount.Currency.Equals(ETHER));
        AssertPath(new BaseCurrency[] { weth, token0, token3 }, result[1].Swaps[0].Route.Path);
        Assert.True(result[1].OutputAmount.Currency.Equals(token3));
    }

    [Fact]
    public async Task BestTradeExactIn_EtherOutput()
    {
        var result = await MixedRouteTrade<Token, Ether>.BestTradeExactIn(new List<object> { pool_v3_weth_0, pool_v3_0_1, pool_v3_0_3, pool_v3_1_3 }, CA(token3, 100), ETHER);
        Assert.Equal(2, result.Count);
        Assert.True(result[0].InputAmount.Currency.Equals(token3));
        AssertPath(new BaseCurrency[] { token3, token0, weth }, result[0].Swaps[0].Route.Path);
        Assert.True(result[0].OutputAmount.Currency.Equals(ETHER));
        Assert.True(result[1].InputAmount.Currency.Equals(token3));
        AssertPath(new BaseCurrency[] { token3, token1, token0, weth }, result[1].Swaps[0].Route.Path);
        Assert.True(result[1].OutputAmount.Currency.Equals(ETHER));
    }

    // ---- #maximumAmountIn (v3) ----
    [Fact]
    public async Task MaximumAmountIn_V3()
    {
        var exactIn = await MixedRouteTrade<Token, Token>.FromRoute(Route(new[] { pool_v3_0_1, pool_v3_1_2 }, token0, token2), CA(token0, 100), TradeType.EXACT_INPUT);
        Assert.Throws<ArgumentException>(() => exactIn.MaximumAmountIn(new Percent(-1, 100)));
        Assert.True(exactIn.MaximumAmountIn(new Percent(0, 100)).Equals(exactIn.InputAmount));
        Assert.True(exactIn.MaximumAmountIn(new Percent(0, 100)).Equals(CA(token0, 100)));
        Assert.True(exactIn.MaximumAmountIn(new Percent(5, 100)).Equals(CA(token0, 100)));
        Assert.True(exactIn.MaximumAmountIn(new Percent(200, 100)).Equals(CA(token0, 100)));
    }

    // ---- #minimumAmountOut (v3) ----
    [Fact]
    public async Task MinimumAmountOut_V3()
    {
        var exactIn = await MixedRouteTrade<Token, Token>.FromRoute(Route(new[] { pool_v3_0_1, pool_v3_1_2 }, token0, token2), CA(token0, 10000), TradeType.EXACT_INPUT);
        Assert.Throws<ArgumentException>(() => exactIn.MinimumAmountOut(new Percent(-1, 100)));
        Assert.True(exactIn.MinimumAmountOut(new Percent(0, 10000)).Equals(exactIn.OutputAmount));
        Assert.True(exactIn.MinimumAmountOut(new Percent(0, 100)).Equals(CA(token2, 7004)));
        Assert.True(exactIn.MinimumAmountOut(new Percent(5, 100)).Equals(CA(token2, 6653)));
        Assert.True(exactIn.MinimumAmountOut(new Percent(200, 100)).Equals(CA(token2, 0)));
    }

    // ---- backwards compatible with pure v2 routes ----
    [Fact]
    public async Task V2_EtherInput()
    {
        var trade = await MixedRouteTrade<Ether, Token>.FromRoute(Route(new object[] { pair_weth_0 }, ETHER, token0), CAE(100), TradeType.EXACT_INPUT);
        Assert.True(trade.InputAmount.Currency.Equals(ETHER));
        Assert.True(trade.OutputAmount.Currency.Equals(token0));
    }

    [Fact]
    public async Task V2_EtherOutput()
    {
        var trade = await MixedRouteTrade<Token, Ether>.FromRoute(Route(new object[] { pair_weth_0 }, token0, ETHER), CA(token0, 100), TradeType.EXACT_INPUT);
        Assert.True(trade.InputAmount.Currency.Equals(token0));
        Assert.True(trade.OutputAmount.Currency.Equals(ETHER));
    }

    [Fact]
    public async Task V2_BestTradeExactIn_ThrowsEmptyPairs()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => MixedRouteTrade<Token, Token>.BestTradeExactIn(new List<object>(), CA(token0, 100), token2));
        Assert.Equal("POOLS", ex.Message);
    }

    [Fact]
    public async Task V2_BestTradeExactIn_ProvidesBestRoute()
    {
        var result = await MixedRouteTrade<Token, Token>.BestTradeExactIn(new List<object> { pair_0_1, pair_0_2, pair_1_2 }, CA(token0, 100), token2);
        Assert.Equal(2, result.Count);
        Assert.Single(result[0].Swaps[0].Route.Pools);
        AssertPath(new BaseCurrency[] { token0, token2 }, result[0].Swaps[0].Route.Path);
        Assert.True(result[0].InputAmount.Equals(CA(token0, 100)));
        Assert.True(result[0].OutputAmount.Equals(CA(token2, 99)));
        Assert.Equal(2, result[1].Swaps[0].Route.Pools.Count);
        AssertPath(new BaseCurrency[] { token0, token1, token2 }, result[1].Swaps[0].Route.Path);
        Assert.True(result[1].OutputAmount.Equals(CA(token2, 69)));
    }

    [Fact]
    public async Task V2_BestTradeExactIn_ZeroLiquidity()
    {
        var result = await MixedRouteTrade<Token, Token>.BestTradeExactIn(new List<object> { empty_pair_0_1 }, CA(token0, 100), token1);
        Assert.Empty(result);
    }

    [Fact]
    public async Task V2_BestTradeExactIn_InsufficientInput()
    {
        var result = await MixedRouteTrade<Token, Token>.BestTradeExactIn(new List<object> { pair_0_1, pair_0_2, pair_1_2 }, CA(token0, 1), token2);
        Assert.Single(result);
        Assert.Single(result[0].Swaps[0].Route.Pools);
        AssertPath(new BaseCurrency[] { token0, token2 }, result[0].Swaps[0].Route.Path);
        Assert.True(result[0].OutputAmount.Equals(CA(token2, 1)));
    }

    [Fact]
    public async Task V2_BestTradeExactIn_NoPath()
    {
        var result = await MixedRouteTrade<Token, Token>.BestTradeExactIn(new List<object> { pair_0_1, pair_0_3, pair_1_3 }, CA(token0, 10), token2);
        Assert.Empty(result);
    }

    [Fact]
    public async Task V2_BestTradeExactIn_EtherInput()
    {
        var result = await MixedRouteTrade<Ether, Token>.BestTradeExactIn(new List<object> { pair_weth_0, pair_0_1, pair_0_3, pair_1_3 }, CAE(100), token3);
        Assert.Equal(2, result.Count);
        Assert.True(result[0].InputAmount.Currency.Equals(ETHER));
        AssertPath(new BaseCurrency[] { weth, token0, token1, token3 }, result[0].Swaps[0].Route.Path);
        Assert.True(result[1].InputAmount.Currency.Equals(ETHER));
        AssertPath(new BaseCurrency[] { weth, token0, token3 }, result[1].Swaps[0].Route.Path);
    }

    [Fact]
    public async Task V2_MaximumAmountIn()
    {
        var exactIn = await MixedRouteTrade<Token, Token>.FromRoute(Route(new object[] { pair_0_1, pair_1_2 }, token0, token2), CA(token0, 100), TradeType.EXACT_INPUT);
        Assert.Throws<ArgumentException>(() => exactIn.MaximumAmountIn(new Percent(-1, 100)));
        Assert.True(exactIn.MaximumAmountIn(new Percent(0, 100)).Equals(exactIn.InputAmount));
        Assert.True(exactIn.MaximumAmountIn(new Percent(5, 100)).Equals(CA(token0, 100)));
    }
}
