using System.Numerics;
using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.Core.Utils;
using UniswapSharp.Router;
using UniswapSharp.Router.Entities;
using UniswapSharp.Router.Entities.MixedRoute;
using UniswapSharp.Router.Utils;
using UniswapSharp.V3.Utils;
using V2Pair = UniswapSharp.V2.Entities.Pair;
using V2Route = UniswapSharp.V2.Entities.Route<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;
using V3Pool = UniswapSharp.V3.Entities.Pool;
using V3Route = UniswapSharp.V3.Entities.Route<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;
using V4Pool = UniswapSharp.V4.Entities.Pool;
using V4Route = UniswapSharp.V4.Entities.Route<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;
using Tick = UniswapSharp.V3.Entities.Tick;
using V3Constants = UniswapSharp.V3.Constants;
using FeeAmount = UniswapSharp.V3.Constants.FeeAmount;

namespace UniswapSharp.Testing.Router.Entities;

// Ported from sdks/router-sdk/src/entities/trade.test.ts (representative subset covering all behaviors)
public class TradeTests
{
    private static readonly Ether ETHER = Ether.OnChain(1);
    private static readonly Token weth = Weth9.Tokens[1];
    private static readonly Token token0 = new(1, "0x0000000000000000000000000000000000000001", 18, "t0", "token0");
    private static readonly Token token1 = new(1, "0x0000000000000000000000000000000000000002", 18, "t1", "token1");
    private static readonly Token token2 = new(1, "0x0000000000000000000000000000000000000003", 18, "t2", "token2");
    private static readonly Token token3 = new(1, "0x0000000000000000000000000000000000000004", 18, "t3", "token3");
    private static readonly BigInteger SQRT_RATIO_ONE = EncodeSqrtRatioX96.Encode(1, 1);

    private static readonly Token token4WithTax = new(1, "0x0000000000000000000000000000000000000005", 18, "t4", "token4", false, 100, 100);
    private static readonly Token token5WithTax = new(1, "0x0000000000000000000000000000000000000005", 18, "t5", "token5", false, 500, 500);

    private static CurrencyAmount<Token> CA(Token t, long v) => CurrencyAmount<Token>.FromRawAmount(t, v);
    private static CurrencyAmount<BaseCurrency> RB(BaseCurrency c, long v) => CurrencyAmount<BaseCurrency>.FromRawAmount(c, v);

    private static V3Pool V2StylePool(CurrencyAmount<Token> reserve0, CurrencyAmount<Token> reserve1, FeeAmount fee = FeeAmount.MEDIUM)
    {
        var sqrtRatioX96 = EncodeSqrtRatioX96.Encode(reserve1.Quotient, reserve0.Quotient);
        var liquidity = (reserve0.Quotient * reserve1.Quotient).Sqrt();
        var spacing = V3Constants.TICK_SPACINGS[fee];
        return new V3Pool(reserve0.Currency, reserve1.Currency, fee, sqrtRatioX96, liquidity, TickMath.GetTickAtSqrtRatio(sqrtRatioX96), new List<Tick>
        {
            new(NearestUsableTick.Find(TickMath.MIN_TICK, spacing), liquidity, liquidity),
            new(NearestUsableTick.Find(TickMath.MAX_TICK, spacing), liquidity * -1, liquidity),
        });
    }

    private static readonly V3Pool pool_0_1 = V2StylePool(CA(token0, 100000), CA(token1, 100000));
    private static readonly V3Pool pool_1_2 = V2StylePool(CA(token1, 12000), CA(token2, 10000));
    private static readonly V3Pool pool_0_3 = V2StylePool(CA(token0, 10000), CA(token3, 10000));
    private static readonly V3Pool pool_weth_0 = V2StylePool(CA(weth, 100000), CA(token0, 100000));
    private static readonly V3Pool pool_weth_1 = V2StylePool(CA(weth, 100000), CA(token1, 100000));
    private static readonly V3Pool pool_weth_2 = V2StylePool(CA(weth, 100000), CA(token2, 100000));

    private static V2Pair Pair(Token a, int aAmt, Token b, int bAmt) => new(CA(a, aAmt), CA(b, bAmt));
    private static readonly V2Pair pair_0_1 = Pair(token0, 12000, token1, 12000);
    private static readonly V2Pair pair_1_2 = Pair(token1, 12000, token2, 10000);
    private static readonly V2Pair pair_2_3 = Pair(token2, 10000, token3, 10000);
    private static readonly V2Pair pair_weth_0 = Pair(weth, 10000, token0, 10000);
    private static readonly V2Pair pair_weth_2 = Pair(weth, 10000, token2, 10000);
    private static readonly V2Pair pair_tax_output = Pair(weth, 100000, token4WithTax, 100000);
    private static readonly V2Pair pair_tax_input = Pair(token5WithTax, 100000, weth, 100000);

    private static readonly V4Pool pool_v4_1_eth = new(token1, ETHER, 3000, 60, V3Constants.ADDRESS_ZERO, SQRT_RATIO_ONE, BigInteger.Parse("10000000000000"), 0, new List<Tick>
    {
        new(NearestUsableTick.Find(TickMath.MIN_TICK, 60), BigInteger.Parse("10000000000000"), BigInteger.Parse("10000000000000")),
        new(NearestUsableTick.Find(TickMath.MAX_TICK, 60), BigInteger.Parse("-10000000000000"), BigInteger.Parse("10000000000000")),
    });

    private static V3Route V3(IEnumerable<V3Pool> pools, BaseCurrency i, BaseCurrency o) => new(pools.ToList(), i, o);
    private static V2Route V2(IEnumerable<V2Pair> pairs, BaseCurrency i, BaseCurrency o) => new(pairs.ToList(), i, o);
    private static MixedRouteSDK<BaseCurrency, BaseCurrency> Mixed(IEnumerable<object> pools, BaseCurrency i, BaseCurrency o) => new(pools.ToList(), i, o);
    private static V4Route V4(IEnumerable<V4Pool> pools, BaseCurrency i, BaseCurrency o) => new(pools.ToList(), i, o);

    private static Task<Trade<BaseCurrency, BaseCurrency>> FromRoute(object route, CurrencyAmount<BaseCurrency> amount, TradeType t) =>
        Trade<BaseCurrency, BaseCurrency>.FromRoute(route, amount, t);

    // ---- #fromRoute ----
    [Fact]
    public async Task FromRoute_V3()
    {
        var trade = await FromRoute(new RouteV3<BaseCurrency, BaseCurrency>(V3(new[] { pool_0_1 }, token0, token1)), RB(token0, 1000), TradeType.EXACT_INPUT);
        var (expectedOut, _) = await pool_0_1.GetOutputAmount(CA(token0, 1000));
        Assert.True(trade.InputAmount.Currency.Equals(token0));
        Assert.True(trade.OutputAmount.Currency.Equals(token1));
        Assert.True(trade.InputAmount.Equals(CA(token0, 1000)));
        Assert.Equal(expectedOut.Quotient, trade.OutputAmount.Quotient);
        Assert.Single(trade.Swaps);
        Assert.Single(trade.Routes);
        Assert.Equal(TradeType.EXACT_INPUT, trade.TradeType);
        Assert.Null(trade.Amounts.InputAmountNative);
        Assert.Null(trade.Amounts.OutputAmountNative);
    }

    [Fact]
    public async Task FromRoute_V2()
    {
        var trade = await FromRoute(new RouteV2<BaseCurrency, BaseCurrency>(V2(new[] { pair_0_1 }, token0, token1)), RB(token1, 1000), TradeType.EXACT_OUTPUT);
        var (expectedIn, _) = pair_0_1.GetInputAmount(CA(token1, 1000));
        Assert.True(trade.OutputAmount.Equals(CA(token1, 1000)));
        Assert.Equal(expectedIn.Quotient, trade.InputAmount.Quotient);
        Assert.Equal(TradeType.EXACT_OUTPUT, trade.TradeType);
        Assert.Null(trade.Amounts.InputAmountNative);
        Assert.Null(trade.Amounts.OutputAmountNative);
    }

    [Fact]
    public async Task FromRoute_Mixed()
    {
        var trade = await FromRoute(new MixedRoute<BaseCurrency, BaseCurrency>(Mixed(new object[] { pool_0_1 }, token0, token1)), RB(token0, 1000), TradeType.EXACT_INPUT);
        var (expectedOut, _) = await pool_0_1.GetOutputAmount(CA(token0, 1000));
        Assert.True(trade.InputAmount.Equals(CA(token0, 1000)));
        Assert.Equal(expectedOut.Quotient, trade.OutputAmount.Quotient);
        Assert.Null(trade.Amounts.InputAmountNative);
        Assert.Null(trade.Amounts.OutputAmountNative);
    }

    [Fact]
    public async Task FromRoute_EtherInput_V3()
    {
        var trade = await FromRoute(new RouteV3<BaseCurrency, BaseCurrency>(V3(new[] { pool_weth_0 }, ETHER, token0)), RB(ETHER, 10), TradeType.EXACT_INPUT);
        Assert.True(trade.InputAmount.Currency.Equals(ETHER));
        Assert.True(trade.OutputAmount.Currency.Equals(token0));
        Assert.NotNull(trade.Amounts.InputAmountNative);
        Assert.Equal(BigInteger.Zero, trade.Amounts.InputAmountNative!.Quotient);
        Assert.Null(trade.Amounts.OutputAmountNative);
    }

    [Fact]
    public async Task FromRoute_EtherOutput_V3_ExactInput()
    {
        var trade = await FromRoute(new RouteV3<BaseCurrency, BaseCurrency>(V3(new[] { pool_weth_0 }, token0, ETHER)), RB(token0, 100), TradeType.EXACT_INPUT);
        var (expectedOut, _) = await pool_weth_0.GetOutputAmount(CA(token0, 100));
        Assert.True(trade.InputAmount.Currency.Equals(token0));
        Assert.True(trade.OutputAmount.Currency.Equals(ETHER));
        Assert.True(trade.InputAmount.Equals(CA(token0, 100)));
        Assert.Equal(expectedOut.Quotient, trade.OutputAmount.Wrapped()!.Quotient);
        Assert.NotNull(trade.Amounts.OutputAmountNative);
        Assert.Equal(BigInteger.Zero, trade.Amounts.OutputAmountNative!.Quotient);
        Assert.Null(trade.Amounts.InputAmountNative);
    }

    [Fact]
    public async Task FromRoute_EtherInput_V2_ExactInput()
    {
        var trade = await FromRoute(new RouteV2<BaseCurrency, BaseCurrency>(V2(new[] { pair_weth_2 }, ETHER, token2)), RB(ETHER, 10), TradeType.EXACT_INPUT);
        Assert.True(trade.InputAmount.Currency.Equals(ETHER));
        Assert.True(trade.OutputAmount.Currency.Equals(token2));
        Assert.NotNull(trade.Amounts.InputAmountNative);
        Assert.Equal(BigInteger.Zero, trade.Amounts.InputAmountNative!.Quotient);
        Assert.Null(trade.Amounts.OutputAmountNative);
    }

    [Fact]
    public async Task FromRoute_EtherOutput_V2_ExactInput()
    {
        var trade = await FromRoute(new RouteV2<BaseCurrency, BaseCurrency>(V2(new[] { pair_weth_2 }, token2, ETHER)), RB(token2, 100), TradeType.EXACT_INPUT);
        Assert.True(trade.InputAmount.Currency.Equals(token2));
        Assert.True(trade.OutputAmount.Currency.Equals(ETHER));
        Assert.NotNull(trade.Amounts.OutputAmountNative);
        Assert.Null(trade.Amounts.InputAmountNative);
    }

    [Fact]
    public async Task FromRoute_EtherInput_Mixed_ExactInput()
    {
        var trade = await FromRoute(new MixedRoute<BaseCurrency, BaseCurrency>(Mixed(new object[] { pool_weth_0 }, ETHER, token0)), RB(ETHER, 10), TradeType.EXACT_INPUT);
        Assert.True(trade.InputAmount.Currency.Equals(ETHER));
        Assert.True(trade.OutputAmount.Currency.Equals(token0));
        Assert.NotNull(trade.Amounts.InputAmountNative);
        Assert.Equal(BigInteger.Zero, trade.Amounts.InputAmountNative!.Quotient);
        Assert.Null(trade.Amounts.OutputAmountNative);
    }

    [Fact]
    public async Task FromRoute_ThrowsInputMismatch_V3()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => FromRoute(new RouteV3<BaseCurrency, BaseCurrency>(V3(new[] { pool_0_1 }, token0, token1)), RB(token2, 1000), TradeType.EXACT_INPUT));
        Assert.Equal("INPUT", ex.Message);
    }

    [Fact]
    public async Task FromRoute_ThrowsOutputMismatch_V3()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => FromRoute(new RouteV3<BaseCurrency, BaseCurrency>(V3(new[] { pool_0_1 }, token0, token1)), RB(token2, 1000), TradeType.EXACT_OUTPUT));
        Assert.Equal("OUTPUT", ex.Message);
    }

    [Fact]
    public async Task FromRoute_ThrowsInputMismatch_Mixed()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => FromRoute(new MixedRoute<BaseCurrency, BaseCurrency>(Mixed(new object[] { pool_0_1 }, token0, token1)), RB(token2, 1000), TradeType.EXACT_INPUT));
        Assert.Equal("INPUT", ex.Message);
    }

    // ---- #fromRoutes ----
    [Fact]
    public async Task FromRoutes_V2AndV3()
    {
        var trade = await Trade<BaseCurrency, BaseCurrency>.FromRoutes(
            new List<(V2Route, CurrencyAmount<BaseCurrency>)> { (V2(new[] { pair_0_1, pair_1_2 }, token0, token2), RB(token0, 100)) },
            new List<(V3Route, CurrencyAmount<BaseCurrency>)> { (V3(new[] { pool_0_1, pool_1_2 }, token0, token2), RB(token0, 1000)) },
            TradeType.EXACT_INPUT);
        Assert.True(trade.InputAmount.Currency.Equals(token0));
        Assert.True(trade.OutputAmount.Currency.Equals(token2));
        Assert.True(trade.InputAmount.Equals(CA(token0, 1100)));
        Assert.Equal(2, trade.Swaps.Count);
        Assert.Equal(2, trade.Routes.Count);
        Assert.Equal(TradeType.EXACT_INPUT, trade.TradeType);
    }

    [Fact]
    public async Task FromRoutes_V2V3Mixed()
    {
        var trade = await Trade<BaseCurrency, BaseCurrency>.FromRoutes(
            new List<(V2Route, CurrencyAmount<BaseCurrency>)> { (V2(new[] { pair_0_1, pair_1_2 }, token0, token2), RB(token0, 100)) },
            new List<(V3Route, CurrencyAmount<BaseCurrency>)> { (V3(new[] { pool_0_1, pool_1_2 }, token0, token2), RB(token0, 1000)) },
            TradeType.EXACT_INPUT,
            new List<(MixedRouteSDK<BaseCurrency, BaseCurrency>, CurrencyAmount<BaseCurrency>)> { (Mixed(new object[] { pool_weth_0, pair_weth_2 }, token0, token2), RB(token0, 1000)) });
        Assert.Equal(3, trade.Swaps.Count);
        Assert.Equal(3, trade.Routes.Count);
        Assert.True(trade.InputAmount.Equals(CA(token0, 2100)));
    }

    [Fact]
    public async Task FromRoutes_MultipleV2V3_ExactOutput_Paths()
    {
        var trade = await Trade<BaseCurrency, BaseCurrency>.FromRoutes(
            new List<(V2Route, CurrencyAmount<BaseCurrency>)>
            {
                (V2(new[] { pair_0_1, pair_1_2 }, token0, token2), RB(token2, 100)),
                (V2(new[] { pair_weth_0, pair_weth_2 }, token0, token2), RB(token2, 1000)),
            },
            new List<(V3Route, CurrencyAmount<BaseCurrency>)>
            {
                (V3(new[] { pool_0_1, pool_1_2 }, token0, token2), RB(token2, 1000)),
                (V3(new[] { pool_weth_0, pool_weth_2 }, token0, token2), RB(token2, 1000)),
            },
            TradeType.EXACT_OUTPUT);
        Assert.True(trade.InputAmount.Currency.Equals(token0));
        Assert.True(trade.OutputAmount.Currency.Equals(token2));
        Assert.Equal(4, trade.Routes.Count);
        Assert.Equal(TradeType.EXACT_OUTPUT, trade.TradeType);
        AssertPath(new BaseCurrency[] { token0, token1, token2 }, trade.Routes[0].Path);
        AssertPath(new BaseCurrency[] { token0, weth, token2 }, trade.Routes[1].Path);
        AssertPath(new BaseCurrency[] { token0, token1, token2 }, trade.Routes[2].Path);
        AssertPath(new BaseCurrency[] { token0, weth, token2 }, trade.Routes[3].Path);
    }

    [Fact]
    public async Task FromRoutes_EtherInput_ExactInput()
    {
        var trade = await Trade<BaseCurrency, BaseCurrency>.FromRoutes(
            new List<(V2Route, CurrencyAmount<BaseCurrency>)> { (V2(new[] { pair_weth_0, pair_0_1 }, ETHER, token1), RB(ETHER, 100)) },
            new List<(V3Route, CurrencyAmount<BaseCurrency>)> { (V3(new[] { pool_weth_0, pool_0_1 }, ETHER, token1), RB(ETHER, 1000)) },
            TradeType.EXACT_INPUT,
            new List<(MixedRouteSDK<BaseCurrency, BaseCurrency>, CurrencyAmount<BaseCurrency>)> { (Mixed(new object[] { pool_weth_2, pair_1_2 }, ETHER, token1), RB(ETHER, 1000)) });
        Assert.True(trade.InputAmount.Currency.Equals(ETHER));
        Assert.True(trade.OutputAmount.Currency.Equals(token1));
        Assert.Equal(3, trade.Swaps.Count);
        Assert.True(trade.Swaps.All(s => s.InputAmount.Currency.IsNative));
        Assert.True(trade.Swaps.All(s => s.Route.Input.IsNative));
        Assert.True(trade.Swaps.All(s => TPool.InvolvesToken(s.Route.Pools[0], weth)));
    }

    [Fact]
    public async Task FromRoutes_V4EthAndV2Weth_ExactInput_NativeAmounts()
    {
        var amountv2 = RB(ETHER, 100);
        var amountv4 = RB(ETHER, 1000);
        var trade = await Trade<BaseCurrency, BaseCurrency>.FromRoutes(
            new List<(V2Route, CurrencyAmount<BaseCurrency>)> { (V2(new[] { pair_weth_0, pair_0_1 }, ETHER, token1), amountv2) },
            new List<(V3Route, CurrencyAmount<BaseCurrency>)>(),
            TradeType.EXACT_INPUT,
            null,
            new List<(V4Route, CurrencyAmount<BaseCurrency>)> { (V4(new[] { pool_v4_1_eth }, ETHER, token1), amountv4) });
        Assert.Equal(2, trade.Swaps.Count);
        Assert.True(trade.Swaps.All(s => s.InputAmount.Currency.IsNative));
        Assert.True(trade.Swaps[0].Route.PathInput.Equals(weth));
        Assert.True(trade.Swaps[1].Route.PathInput.Equals(ETHER));
        Assert.True(trade.Amounts.InputAmount.Equals(RB(ETHER, 1100)));
        Assert.NotNull(trade.Amounts.InputAmountNative);
        Assert.True(trade.Amounts.InputAmountNative!.Equals(RB(ETHER, 1000)));
        Assert.Null(trade.Amounts.OutputAmountNative);
    }

    [Fact]
    public async Task FromRoutes_V4EthAndV2Weth_EtherOutput_ExactInput_NativeAmounts()
    {
        var trade = await Trade<BaseCurrency, BaseCurrency>.FromRoutes(
            new List<(V2Route, CurrencyAmount<BaseCurrency>)> { (V2(new[] { pair_0_1, pair_weth_0 }, token1, ETHER), RB(token1, 100)) },
            new List<(V3Route, CurrencyAmount<BaseCurrency>)>(),
            TradeType.EXACT_INPUT,
            null,
            new List<(V4Route, CurrencyAmount<BaseCurrency>)> { (V4(new[] { pool_v4_1_eth }, token1, ETHER), RB(token1, 1000)) });
        Assert.True(trade.InputAmount.Currency.Equals(token1));
        Assert.True(trade.OutputAmount.Currency.Equals(ETHER));
        Assert.True(trade.Swaps.All(s => s.OutputAmount.Currency.IsNative));
        Assert.True(trade.Swaps[0].Route.PathOutput.Equals(weth));
        Assert.True(trade.Swaps[1].Route.PathOutput.Equals(ETHER));
        Assert.Null(trade.Amounts.InputAmountNative);
        Assert.NotNull(trade.Amounts.OutputAmountNative);
        Assert.True(trade.Amounts.OutputAmountNative!.GreaterThan(BigInteger.Zero));
    }

    // ---- #priceImpact ----
    [Fact]
    public void PriceImpact_FotSell()
    {
        var trade = new Trade<BaseCurrency, BaseCurrency>(TradeType.EXACT_INPUT,
            v2Routes: new[] { new V2RouteAmounts<BaseCurrency, BaseCurrency>(V2(new[] { pair_tax_output }, weth, token4WithTax), RB(weth, 100), RB(token4WithTax, 69)) });
        Assert.Equal("30.3", trade.PriceImpact.ToSignificant(3));
    }

    [Fact]
    public void PriceImpact_FotBuy()
    {
        var trade = new Trade<BaseCurrency, BaseCurrency>(TradeType.EXACT_INPUT,
            v2Routes: new[] { new V2RouteAmounts<BaseCurrency, BaseCurrency>(V2(new[] { pair_tax_input }, token5WithTax, weth), RB(token5WithTax, 100), RB(weth, 69)) });
        Assert.Equal("27.4", trade.PriceImpact.ToSignificant(3));
    }

    [Fact]
    public void PriceImpact_ExactInput_V3AndMixedMatch()
    {
        var trade = new Trade<BaseCurrency, BaseCurrency>(TradeType.EXACT_INPUT,
            v3Routes: new[] { new V3RouteAmounts<BaseCurrency, BaseCurrency>(V3(new[] { pool_0_1, pool_1_2 }, token0, token2), RB(token0, 100), RB(token2, 69)) });
        var mixedTrade = new Trade<BaseCurrency, BaseCurrency>(TradeType.EXACT_INPUT,
            mixedRoutes: new[] { new MixedRouteAmounts<BaseCurrency, BaseCurrency>(Mixed(new object[] { pool_0_1, pool_1_2 }, token0, token2), RB(token0, 100), RB(token2, 69)) });
        Assert.Equal("17.2", trade.PriceImpact.ToSignificant(3));
        Assert.Equal(trade.PriceImpact.ToSignificant(3), mixedTrade.PriceImpact.ToSignificant(3));
        Assert.True(mixedTrade.PriceImpact.Equals(trade.PriceImpact));
    }

    [Fact]
    public void PriceImpact_ExactOutput()
    {
        var exactOut = new Trade<BaseCurrency, BaseCurrency>(TradeType.EXACT_OUTPUT,
            v3Routes: new[] { new V3RouteAmounts<BaseCurrency, BaseCurrency>(V3(new[] { pool_0_1, pool_1_2 }, token0, token2), RB(token0, 156), RB(token2, 100)) });
        Assert.Equal("23.1", exactOut.PriceImpact.ToSignificant(3));
    }

    // ---- #minimumAmountOut / #maximumAmountIn ----
    [Fact]
    public void MinimumAmountOut_ExactInput()
    {
        var exactIn = new Trade<BaseCurrency, BaseCurrency>(TradeType.EXACT_INPUT,
            v3Routes: new[] { new V3RouteAmounts<BaseCurrency, BaseCurrency>(V3(new[] { pool_0_1, pool_1_2 }, token0, token2), RB(token0, 100), RB(token2, 69)) });
        Assert.Throws<ArgumentException>(() => exactIn.MinimumAmountOut(new Percent(-1, 100)));
        Assert.True(exactIn.MinimumAmountOut(new Percent(0, 100)).Equals(exactIn.OutputAmount));
        Assert.True(exactIn.MinimumAmountOut(new Percent(0, 100)).Equals(CA(token2, 69)));
        Assert.True(exactIn.MinimumAmountOut(new Percent(5, 100)).Equals(CA(token2, 65)));
        Assert.True(exactIn.MinimumAmountOut(new Percent(200, 100)).Equals(CA(token2, 0)));
    }

    [Fact]
    public void MaximumAmountIn_ExactOutput()
    {
        var exactOut = new Trade<BaseCurrency, BaseCurrency>(TradeType.EXACT_OUTPUT,
            v3Routes: new[] { new V3RouteAmounts<BaseCurrency, BaseCurrency>(V3(new[] { pool_0_1, pool_1_2 }, token0, token2), RB(token0, 100), RB(token2, 69)) });
        Assert.Throws<ArgumentException>(() => exactOut.MaximumAmountIn(new Percent(-1, 100)));
        Assert.True(exactOut.MaximumAmountIn(new Percent(0, 100)).Equals(exactOut.InputAmount));
        Assert.True(exactOut.MaximumAmountIn(new Percent(0, 100)).Equals(CA(token0, 100)));
        Assert.True(exactOut.MaximumAmountIn(new Percent(5, 100)).Equals(CA(token0, 105)));
        Assert.True(exactOut.MaximumAmountIn(new Percent(200, 100)).Equals(CA(token0, 300)));
    }

    // ---- #executionPrice ----
    [Fact]
    public async Task ExecutionPrice_ExactInput()
    {
        var trade = await Trade<BaseCurrency, BaseCurrency>.FromRoutes(
            new List<(V2Route, CurrencyAmount<BaseCurrency>)> { (V2(new[] { pair_0_1 }, token0, token1), RB(token0, 100)) },
            new List<(V3Route, CurrencyAmount<BaseCurrency>)> { (V3(new[] { pool_0_1 }, token0, token1), RB(token0, 100)) },
            TradeType.EXACT_INPUT,
            new List<(MixedRouteSDK<BaseCurrency, BaseCurrency>, CurrencyAmount<BaseCurrency>)> { (Mixed(new object[] { pair_weth_0, pool_weth_1 }, token0, token1), RB(token0, 100)) });
        Assert.True(trade.ExecutionPrice.BaseCurrency.Equals(token0));
        Assert.True(trade.ExecutionPrice.QuoteCurrency.Equals(token1));
        Assert.Equal((BigInteger)300, trade.ExecutionPrice.Denominator);
        Assert.Equal(trade.OutputAmount.Quotient, trade.ExecutionPrice.Numerator);
    }

    private static void AssertPath(BaseCurrency[] expected, List<BaseCurrency> actual)
    {
        Assert.Equal(expected.Length, actual.Count);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.True(actual[i].Equals(expected[i]), $"path[{i}] expected {expected[i].Symbol}");
        }
    }
}
