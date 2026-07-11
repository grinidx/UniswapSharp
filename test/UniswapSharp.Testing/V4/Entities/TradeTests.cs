using System.Numerics;
using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.Core.Utils;
using UniswapSharp.V3.Utils;
using UniswapSharp.V4.Entities;
using Constants = UniswapSharp.V4.Constants;
using Pool = UniswapSharp.V4.Entities.Pool;
using Tick = UniswapSharp.V3.Entities.Tick;

namespace UniswapSharp.Testing.V4.Entities;

// Ported 1:1 from sdks/v4-sdk/src/entities/trade.test.ts.
// Route/Trade are the open-generic V4 types (UniswapSharp.V4.Entities), which cannot be aliased,
// so the V3 Entities namespace is deliberately not imported; Pool and Tick are aliased instead.
public class TradeTests
{
    private const int MEDIUM = Constants.FEE_AMOUNT_MEDIUM;
    private const int SIXTY = Constants.TICK_SPACING_SIXTY;
    private const string ADDRESS_ZERO = Constants.ADDRESS_ZERO;

    private static readonly Ether ETHER = Ether.OnChain(1);
    private static readonly Token weth = Weth9.Tokens[1];
    private static readonly Token token0 = new(1, "0x0000000000000000000000000000000000000001", 18, "t0", "token0");
    private static readonly Token token1 = new(1, "0x0000000000000000000000000000000000000002", 18, "t1", "token1");
    private static readonly Token token2 = new(1, "0x0000000000000000000000000000000000000003", 18, "t2", "token2");
    private static readonly Token token3 = new(1, "0x0000000000000000000000000000000000000004", 18, "t3", "token3");

    private static CurrencyAmount<BaseCurrency> Amt(BaseCurrency currency, BigInteger raw) =>
        CurrencyAmount<BaseCurrency>.FromRawAmount(currency, raw);

    private static CurrencyAmount<Token> TAmt(Token currency, BigInteger raw) =>
        CurrencyAmount<Token>.FromRawAmount(currency, raw);

    private static CurrencyAmount<Ether> EAmt(BigInteger raw) =>
        CurrencyAmount<Ether>.FromRawAmount(ETHER, raw);

    private static Pool V2StylePool(CurrencyAmount<BaseCurrency> reserve0, CurrencyAmount<BaseCurrency> reserve1, int feeAmount = MEDIUM)
    {
        var sqrtRatioX96 = EncodeSqrtRatioX96.Encode(reserve1.Quotient, reserve0.Quotient);
        var liquidity = (reserve0.Quotient * reserve1.Quotient).Sqrt();
        return new Pool(
            reserve0.Currency,
            reserve1.Currency,
            feeAmount,
            SIXTY,
            ADDRESS_ZERO,
            sqrtRatioX96,
            liquidity,
            TickMath.GetTickAtSqrtRatio(sqrtRatioX96),
            new List<Tick>
            {
                new(NearestUsableTick.Find(TickMath.MIN_TICK, SIXTY), liquidity, liquidity),
                new(NearestUsableTick.Find(TickMath.MAX_TICK, SIXTY), -liquidity, liquidity),
            });
    }

    private static readonly Pool pool_0_1 = V2StylePool(Amt(token0, 100000), Amt(token1, 100000));
    private static readonly Pool pool_0_2 = V2StylePool(Amt(token0, 100000), Amt(token2, 110000));
    private static readonly Pool pool_0_3 = V2StylePool(Amt(token0, 100000), Amt(token3, 90000));
    private static readonly Pool pool_1_2 = V2StylePool(Amt(token1, 120000), Amt(token2, 100000));
    private static readonly Pool pool_1_3 = V2StylePool(Amt(token1, 120000), Amt(token3, 130000));
    private static readonly Pool pool_eth_0 = V2StylePool(Amt(ETHER, 100000), Amt(token0, 100000));
    private static readonly Pool pool_eth_1 = V2StylePool(Amt(ETHER, 100000), Amt(token1, 100000));
    private static readonly Pool pool_eth_2 = V2StylePool(Amt(ETHER, 100000), Amt(token2, 100000));
    private static readonly Pool pool_weth_0 = V2StylePool(Amt(weth, 100000), Amt(token0, 100000));
    private static readonly Pool pool_weth_eth = V2StylePool(Amt(ETHER, 100000), Amt(weth, 100000));

    private static List<Pool> P(params Pool[] pools) => pools.ToList();

    private static void AssertCurrency(BaseCurrency expected, BaseCurrency actual) => Assert.True(actual.Equals(expected));

    private static void AssertPath(List<BaseCurrency> expected, IReadOnlyList<BaseCurrency> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.True(actual[i].Equals(expected[i]), $"currency path mismatch at index {i}");
        }
    }

    // ---------------- #fromRoute ----------------

    [Fact]
    public async Task FromRoute_ConstructedWithEtherAsInput()
    {
        var trade = await Trade<Ether, Token>.FromRoute(
            new Route<Ether, Token>(P(pool_eth_0), ETHER, token0), Amt(ETHER, 10000), TradeType.EXACT_INPUT);
        AssertCurrency(ETHER, trade.InputAmount.Currency);
        AssertCurrency(token0, trade.OutputAmount.Currency);
    }

    [Fact]
    public async Task FromRoute_ConstructedWithEtherAsInputOnWethPool()
    {
        var trade = await Trade<Ether, Token>.FromRoute(
            new Route<Ether, Token>(P(pool_weth_0), ETHER, token0), Amt(ETHER, 10000), TradeType.EXACT_INPUT);
        AssertCurrency(ETHER, trade.InputAmount.Currency);
        AssertCurrency(token0, trade.OutputAmount.Currency);
    }

    [Fact]
    public async Task FromRoute_ConstructedWithWethAsInputOnEthPool()
    {
        var trade = await Trade<Token, Token>.FromRoute(
            new Route<Token, Token>(P(pool_eth_0), weth, token0), Amt(weth, 10000), TradeType.EXACT_INPUT);
        AssertCurrency(weth, trade.InputAmount.Currency);
        AssertCurrency(token0, trade.OutputAmount.Currency);
    }

    [Fact]
    public async Task FromRoute_ConstructedWithEtherAsOutputOnWethPool()
    {
        var trade = await Trade<Token, Ether>.FromRoute(
            new Route<Token, Ether>(P(pool_weth_0), token0, ETHER), Amt(token0, 10000), TradeType.EXACT_INPUT);
        AssertCurrency(token0, trade.InputAmount.Currency);
        AssertCurrency(ETHER, trade.OutputAmount.Currency);
    }

    [Fact]
    public async Task FromRoute_ConstructedWithWethAsOutputOnEthPool()
    {
        var trade = await Trade<Token, Token>.FromRoute(
            new Route<Token, Token>(P(pool_eth_0), token0, weth), Amt(token0, 10000), TradeType.EXACT_INPUT);
        AssertCurrency(token0, trade.InputAmount.Currency);
        AssertCurrency(weth, trade.OutputAmount.Currency);
    }

    [Fact]
    public async Task FromRoute_ConstructedWithEtherAsInputForExactOutput()
    {
        var trade = await Trade<Ether, Token>.FromRoute(
            new Route<Ether, Token>(P(pool_eth_0), ETHER, token0), Amt(token0, 10000), TradeType.EXACT_OUTPUT);
        AssertCurrency(ETHER, trade.InputAmount.Currency);
        AssertCurrency(token0, trade.OutputAmount.Currency);
    }

    [Fact]
    public async Task FromRoute_ConstructedWithEtherAsOutput()
    {
        var trade = await Trade<Token, Ether>.FromRoute(
            new Route<Token, Ether>(P(pool_eth_0), token0, ETHER), Amt(ETHER, 10000), TradeType.EXACT_OUTPUT);
        AssertCurrency(token0, trade.InputAmount.Currency);
        AssertCurrency(ETHER, trade.OutputAmount.Currency);
    }

    [Fact]
    public async Task FromRoute_ConstructedWithEtherAsOutputForExactInput()
    {
        var trade = await Trade<Token, Ether>.FromRoute(
            new Route<Token, Ether>(P(pool_eth_0), token0, ETHER), Amt(token0, 10000), TradeType.EXACT_INPUT);
        AssertCurrency(token0, trade.InputAmount.Currency);
        AssertCurrency(ETHER, trade.OutputAmount.Currency);
    }

    [Fact]
    public async Task FromRoute_ConstructedWithEtherAsOutputForExactOutputWithEthWethPool()
    {
        var trade = await Trade<Token, Ether>.FromRoute(
            new Route<Token, Ether>(P(pool_eth_0, pool_weth_eth), token0, ETHER), Amt(ETHER, 10000), TradeType.EXACT_OUTPUT);
        AssertCurrency(token0, trade.InputAmount.Currency);
        AssertCurrency(ETHER, trade.OutputAmount.Currency);
    }

    [Fact]
    public async Task FromRoute_ConstructedWithWethAsOutputForExactOutputWithEthWethPool()
    {
        var trade = await Trade<Token, Token>.FromRoute(
            new Route<Token, Token>(P(pool_weth_0, pool_weth_eth), token0, weth), Amt(weth, 10000), TradeType.EXACT_OUTPUT);
        AssertCurrency(token0, trade.InputAmount.Currency);
        AssertCurrency(weth, trade.OutputAmount.Currency);
    }

    // ---------------- #fromRoutes ----------------

    [Fact]
    public async Task FromRoutes_ConstructedWithEtherAsInputWithMultipleRoutes()
    {
        var trade = await Trade<Ether, Token>.FromRoutes(
            new List<(CurrencyAmount<BaseCurrency>, Route<Ether, Token>)>
            {
                (Amt(ETHER, 10000), new Route<Ether, Token>(P(pool_eth_0), ETHER, token0)),
            },
            TradeType.EXACT_INPUT);
        AssertCurrency(ETHER, trade.InputAmount.Currency);
        AssertCurrency(token0, trade.OutputAmount.Currency);
    }

    [Fact]
    public async Task FromRoutes_ConstructedWithEtherAsInputForExactOutputWithMultipleRoutes()
    {
        var trade = await Trade<Ether, Token>.FromRoutes(
            new List<(CurrencyAmount<BaseCurrency>, Route<Ether, Token>)>
            {
                (Amt(token0, 3000), new Route<Ether, Token>(P(pool_eth_0), ETHER, token0)),
                (Amt(token0, 7000), new Route<Ether, Token>(P(pool_eth_1, pool_0_1), ETHER, token0)),
            },
            TradeType.EXACT_OUTPUT);
        AssertCurrency(ETHER, trade.InputAmount.Currency);
        AssertCurrency(token0, trade.OutputAmount.Currency);
    }

    [Fact]
    public async Task FromRoutes_ConstructedWithEtherAsOutputWithMultipleRoutes()
    {
        var trade = await Trade<Token, Ether>.FromRoutes(
            new List<(CurrencyAmount<BaseCurrency>, Route<Token, Ether>)>
            {
                (Amt(ETHER, 4000), new Route<Token, Ether>(P(pool_eth_0), token0, ETHER)),
                (Amt(ETHER, 6000), new Route<Token, Ether>(P(pool_0_1, pool_eth_1), token0, ETHER)),
            },
            TradeType.EXACT_OUTPUT);
        AssertCurrency(token0, trade.InputAmount.Currency);
        AssertCurrency(ETHER, trade.OutputAmount.Currency);
    }

    [Fact]
    public async Task FromRoutes_ConstructedWithEtherAsOutputForExactInputWithMultipleRoutes()
    {
        var trade = await Trade<Token, Ether>.FromRoutes(
            new List<(CurrencyAmount<BaseCurrency>, Route<Token, Ether>)>
            {
                (Amt(token0, 3000), new Route<Token, Ether>(P(pool_eth_0), token0, ETHER)),
                (Amt(token0, 7000), new Route<Token, Ether>(P(pool_0_1, pool_eth_1), token0, ETHER)),
            },
            TradeType.EXACT_INPUT);
        AssertCurrency(token0, trade.InputAmount.Currency);
        AssertCurrency(ETHER, trade.OutputAmount.Currency);
    }

    [Fact]
    public async Task FromRoutes_ThrowsIfPoolsAreReUsedBetweenRoutes()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Trade<Token, Ether>.FromRoutes(
            new List<(CurrencyAmount<BaseCurrency>, Route<Token, Ether>)>
            {
                (Amt(token0, 4500), new Route<Token, Ether>(P(pool_0_1, pool_eth_1), token0, ETHER)),
                (Amt(token0, 5500), new Route<Token, Ether>(P(pool_0_1, pool_1_2, pool_eth_2), token0, ETHER)),
            },
            TradeType.EXACT_INPUT));
    }

    // ---------------- #createUncheckedTrade ----------------

    [Fact]
    public void CreateUncheckedTrade_ThrowsIfInputCurrencyDoesNotMatchRoute()
    {
        Assert.Throws<ArgumentException>(() => Trade<Token, Token>.CreateUncheckedTrade(
            new RouteInput<Token, Token>
            {
                Route = new Route<Token, Token>(P(pool_0_1), token0, token1),
                InputAmount = TAmt(token2, 10000),
                OutputAmount = TAmt(token1, 10000),
            },
            TradeType.EXACT_INPUT));
    }

    [Fact]
    public void CreateUncheckedTrade_ThrowsIfOutputCurrencyDoesNotMatchRoute()
    {
        Assert.Throws<ArgumentException>(() => Trade<Token, Token>.CreateUncheckedTrade(
            new RouteInput<Token, Token>
            {
                Route = new Route<Token, Token>(P(pool_0_1), token0, token1),
                InputAmount = TAmt(token0, 10000),
                OutputAmount = TAmt(token2, 10000),
            },
            TradeType.EXACT_INPUT));
    }

    [Fact]
    public void CreateUncheckedTrade_CanCreateAnExactInputTradeWithoutSimulating()
    {
        Trade<Token, Token>.CreateUncheckedTrade(
            new RouteInput<Token, Token>
            {
                Route = new Route<Token, Token>(P(pool_0_1), token0, token1),
                InputAmount = TAmt(token0, 10000),
                OutputAmount = TAmt(token1, 100000),
            },
            TradeType.EXACT_INPUT);
    }

    [Fact]
    public void CreateUncheckedTrade_CanCreateAnExactOutputTradeWithoutSimulating()
    {
        Trade<Token, Token>.CreateUncheckedTrade(
            new RouteInput<Token, Token>
            {
                Route = new Route<Token, Token>(P(pool_0_1), token0, token1),
                InputAmount = TAmt(token0, 10000),
                OutputAmount = TAmt(token1, 100000),
            },
            TradeType.EXACT_OUTPUT);
    }

    // ---------------- #createUncheckedTradeWithMultipleRoutes ----------------

    [Fact]
    public void CreateUncheckedTradeWithMultipleRoutes_ThrowsIfInputCurrencyDoesNotMatchRoute()
    {
        Assert.Throws<ArgumentException>(() => Trade<Token, Token>.CreateUncheckedTradeWithMultipleRoutes(
            new List<RouteInput<Token, Token>>
            {
                new()
                {
                    Route = new Route<Token, Token>(P(pool_1_2), token2, token1),
                    InputAmount = TAmt(token2, 2000),
                    OutputAmount = TAmt(token1, 2000),
                },
                new()
                {
                    Route = new Route<Token, Token>(P(pool_0_1), token0, token1),
                    InputAmount = TAmt(token2, 8000),
                    OutputAmount = TAmt(token1, 8000),
                },
            },
            TradeType.EXACT_INPUT));
    }

    [Fact]
    public void CreateUncheckedTradeWithMultipleRoutes_ThrowsIfOutputCurrencyDoesNotMatchRoute()
    {
        Assert.Throws<ArgumentException>(() => Trade<Token, Token>.CreateUncheckedTradeWithMultipleRoutes(
            new List<RouteInput<Token, Token>>
            {
                new()
                {
                    Route = new Route<Token, Token>(P(pool_0_2), token0, token2),
                    InputAmount = TAmt(token0, 10000),
                    OutputAmount = TAmt(token2, 10000),
                },
                new()
                {
                    Route = new Route<Token, Token>(P(pool_0_1), token0, token1),
                    InputAmount = TAmt(token0, 10000),
                    OutputAmount = TAmt(token2, 10000),
                },
            },
            TradeType.EXACT_INPUT));
    }

    [Fact]
    public void CreateUncheckedTradeWithMultipleRoutes_CanCreateAnExactInputTradeWithoutSimulating()
    {
        Trade<Token, Token>.CreateUncheckedTradeWithMultipleRoutes(
            new List<RouteInput<Token, Token>>
            {
                new()
                {
                    Route = new Route<Token, Token>(P(pool_0_1), token0, token1),
                    InputAmount = TAmt(token0, 5000),
                    OutputAmount = TAmt(token1, 50000),
                },
                new()
                {
                    Route = new Route<Token, Token>(P(pool_0_2, pool_1_2), token0, token1),
                    InputAmount = TAmt(token0, 5000),
                    OutputAmount = TAmt(token1, 50000),
                },
            },
            TradeType.EXACT_INPUT);
    }

    [Fact]
    public void CreateUncheckedTradeWithMultipleRoutes_CanCreateAnExactOutputTradeWithoutSimulating()
    {
        Trade<Token, Token>.CreateUncheckedTradeWithMultipleRoutes(
            new List<RouteInput<Token, Token>>
            {
                new()
                {
                    Route = new Route<Token, Token>(P(pool_0_1), token0, token1),
                    InputAmount = TAmt(token0, 5001),
                    OutputAmount = TAmt(token1, 50000),
                },
                new()
                {
                    Route = new Route<Token, Token>(P(pool_0_2, pool_1_2), token0, token1),
                    InputAmount = TAmt(token0, 4999),
                    OutputAmount = TAmt(token1, 50000),
                },
            },
            TradeType.EXACT_OUTPUT);
    }

    // ---------------- #route and #swaps ----------------

    private static Trade<Token, Token> SingleRoute() => Trade<Token, Token>.CreateUncheckedTrade(
        new RouteInput<Token, Token>
        {
            Route = new Route<Token, Token>(P(pool_0_1, pool_1_2), token0, token2),
            InputAmount = TAmt(token0, 100),
            OutputAmount = TAmt(token2, 69),
        },
        TradeType.EXACT_INPUT);

    private static Trade<Token, Token> MultiRoute() => Trade<Token, Token>.CreateUncheckedTradeWithMultipleRoutes(
        new List<RouteInput<Token, Token>>
        {
            new()
            {
                Route = new Route<Token, Token>(P(pool_0_1, pool_1_2), token0, token2),
                InputAmount = TAmt(token0, 50),
                OutputAmount = TAmt(token2, 35),
            },
            new()
            {
                Route = new Route<Token, Token>(P(pool_0_2), token0, token2),
                InputAmount = TAmt(token0, 50),
                OutputAmount = TAmt(token2, 34),
            },
        },
        TradeType.EXACT_INPUT);

    [Fact]
    public void Route_CanAccessRouteForSingleRouteTrade()
    {
        Assert.NotNull(SingleRoute().Route);
    }

    [Fact]
    public void Route_CanAccessRoutesForBothSingleAndMultiRouteTrades()
    {
        Assert.Single(SingleRoute().Swaps);
        Assert.Equal(2, MultiRoute().Swaps.Count);
    }

    [Fact]
    public void Route_ThrowsIfAccessRouteOnMultiRouteTrade()
    {
        var multiRoute = MultiRoute();
        Assert.Throws<InvalidOperationException>(() => multiRoute.Route);
    }

    // ---------------- #worstExecutionPrice ----------------

    private static Trade<Token, Token> WorstExactIn() => Trade<Token, Token>.CreateUncheckedTrade(
        new RouteInput<Token, Token>
        {
            Route = new Route<Token, Token>(P(pool_0_1, pool_1_2), token0, token2),
            InputAmount = TAmt(token0, 100),
            OutputAmount = TAmt(token2, 69),
        },
        TradeType.EXACT_INPUT);

    private static Trade<Token, Token> WorstExactInMultiRoute() => Trade<Token, Token>.CreateUncheckedTradeWithMultipleRoutes(
        new List<RouteInput<Token, Token>>
        {
            new()
            {
                Route = new Route<Token, Token>(P(pool_0_1, pool_1_2), token0, token2),
                InputAmount = TAmt(token0, 50),
                OutputAmount = TAmt(token2, 35),
            },
            new()
            {
                Route = new Route<Token, Token>(P(pool_0_2), token0, token2),
                InputAmount = TAmt(token0, 50),
                OutputAmount = TAmt(token2, 34),
            },
        },
        TradeType.EXACT_INPUT);

    private static Trade<Token, Token> WorstExactOut() => Trade<Token, Token>.CreateUncheckedTrade(
        new RouteInput<Token, Token>
        {
            Route = new Route<Token, Token>(P(pool_0_1, pool_1_2), token0, token2),
            InputAmount = TAmt(token0, 156),
            OutputAmount = TAmt(token2, 100),
        },
        TradeType.EXACT_OUTPUT);

    private static Trade<Token, Token> WorstExactOutMultiRoute() => Trade<Token, Token>.CreateUncheckedTradeWithMultipleRoutes(
        new List<RouteInput<Token, Token>>
        {
            new()
            {
                Route = new Route<Token, Token>(P(pool_0_1, pool_1_2), token0, token2),
                InputAmount = TAmt(token0, 78),
                OutputAmount = TAmt(token2, 50),
            },
            new()
            {
                Route = new Route<Token, Token>(P(pool_0_2), token0, token2),
                InputAmount = TAmt(token0, 78),
                OutputAmount = TAmt(token2, 50),
            },
        },
        TradeType.EXACT_OUTPUT);

    [Fact]
    public void WorstExecutionPrice_ExactInput_ThrowsIfLessThan0()
    {
        Assert.Throws<ArgumentException>(() => WorstExactIn().MinimumAmountOut(new Percent(-1, 100)));
    }

    [Fact]
    public void WorstExecutionPrice_ExactInput_ReturnsExactIf0()
    {
        var exactIn = WorstExactIn();
        Assert.Equal(exactIn.ExecutionPrice, exactIn.WorstExecutionPrice(new Percent(0, 100)));
    }

    [Fact]
    public void WorstExecutionPrice_ExactInput_ReturnsExactIfNonzero()
    {
        var exactIn = WorstExactIn();
        Assert.Equal(new Price<Token, Token>(token0, token2, 100, 69), exactIn.WorstExecutionPrice(new Percent(0, 100)));
        Assert.Equal(new Price<Token, Token>(token0, token2, 100, 65), exactIn.WorstExecutionPrice(new Percent(5, 100)));
        Assert.Equal(new Price<Token, Token>(token0, token2, 100, 0), exactIn.WorstExecutionPrice(new Percent(200, 100)));
    }

    [Fact]
    public void WorstExecutionPrice_ExactInput_ReturnsExactIfNonzeroWithMultipleRoutes()
    {
        var exactInMultiRoute = WorstExactInMultiRoute();
        Assert.Equal(new Price<Token, Token>(token0, token2, 100, 69), exactInMultiRoute.WorstExecutionPrice(new Percent(0, 100)));
        Assert.Equal(new Price<Token, Token>(token0, token2, 100, 65), exactInMultiRoute.WorstExecutionPrice(new Percent(5, 100)));
        Assert.Equal(new Price<Token, Token>(token0, token2, 100, 0), exactInMultiRoute.WorstExecutionPrice(new Percent(200, 100)));
    }

    [Fact]
    public void WorstExecutionPrice_ExactOutput_ThrowsIfLessThan0()
    {
        Assert.Throws<ArgumentException>(() => WorstExactOut().WorstExecutionPrice(new Percent(-1, 100)));
    }

    [Fact]
    public void WorstExecutionPrice_ExactOutput_ReturnsExactIf0()
    {
        var exactOut = WorstExactOut();
        Assert.Equal(exactOut.ExecutionPrice, exactOut.WorstExecutionPrice(new Percent(0, 100)));
    }

    [Fact]
    public void WorstExecutionPrice_ExactOutput_ReturnsSlippageAmountIfNonzero()
    {
        var exactOut = WorstExactOut();
        Assert.True(exactOut.WorstExecutionPrice(new Percent(0, 100)).Equals(new Price<Token, Token>(token0, token2, 156, 100)));
        Assert.True(exactOut.WorstExecutionPrice(new Percent(5, 100)).Equals(new Price<Token, Token>(token0, token2, 163, 100)));
        Assert.True(exactOut.WorstExecutionPrice(new Percent(200, 100)).Equals(new Price<Token, Token>(token0, token2, 468, 100)));
    }

    [Fact]
    public void WorstExecutionPrice_ExactOutput_ReturnsExactIfNonzeroWithMultipleRoutes()
    {
        var exactOutMultiRoute = WorstExactOutMultiRoute();
        Assert.True(exactOutMultiRoute.WorstExecutionPrice(new Percent(0, 100)).Equals(new Price<Token, Token>(token0, token2, 156, 100)));
        Assert.True(exactOutMultiRoute.WorstExecutionPrice(new Percent(5, 100)).Equals(new Price<Token, Token>(token0, token2, 163, 100)));
        Assert.True(exactOutMultiRoute.WorstExecutionPrice(new Percent(200, 100)).Equals(new Price<Token, Token>(token0, token2, 468, 100)));
    }

    // ---------------- #priceImpact ----------------

    [Fact]
    public void PriceImpact_ExactInput_IsCorrect()
    {
        var exactIn = Trade<Token, Token>.CreateUncheckedTradeWithMultipleRoutes(
            new List<RouteInput<Token, Token>>
            {
                new()
                {
                    Route = new Route<Token, Token>(P(pool_0_1, pool_1_2), token0, token2),
                    InputAmount = TAmt(token0, 100),
                    OutputAmount = TAmt(token2, 69),
                },
            },
            TradeType.EXACT_INPUT);
        Assert.Equal("17.2", exactIn.PriceImpact.ToSignificant(3));
    }

    [Fact]
    public void PriceImpact_ExactInput_IsCorrectWithMultipleRoutes()
    {
        var exactInMultipleRoutes = Trade<Token, Token>.CreateUncheckedTradeWithMultipleRoutes(
            new List<RouteInput<Token, Token>>
            {
                new()
                {
                    Route = new Route<Token, Token>(P(pool_0_1, pool_1_2), token0, token2),
                    InputAmount = TAmt(token0, 90),
                    OutputAmount = TAmt(token2, 62),
                },
                new()
                {
                    Route = new Route<Token, Token>(P(pool_0_2), token0, token2),
                    InputAmount = TAmt(token0, 10),
                    OutputAmount = TAmt(token2, 7),
                },
            },
            TradeType.EXACT_INPUT);
        Assert.Equal("19.8", exactInMultipleRoutes.PriceImpact.ToSignificant(3));
    }

    [Fact]
    public void PriceImpact_ExactOutput_IsCorrect()
    {
        var exactOut = Trade<Token, Token>.CreateUncheckedTradeWithMultipleRoutes(
            new List<RouteInput<Token, Token>>
            {
                new()
                {
                    Route = new Route<Token, Token>(P(pool_0_1, pool_1_2), token0, token2),
                    InputAmount = TAmt(token0, 156),
                    OutputAmount = TAmt(token2, 100),
                },
            },
            TradeType.EXACT_OUTPUT);
        Assert.Equal("23.1", exactOut.PriceImpact.ToSignificant(3));
    }

    [Fact]
    public void PriceImpact_ExactOutput_IsCorrectWithMultipleRoutes()
    {
        var exactOutMultipleRoutes = Trade<Token, Token>.CreateUncheckedTradeWithMultipleRoutes(
            new List<RouteInput<Token, Token>>
            {
                new()
                {
                    Route = new Route<Token, Token>(P(pool_0_1, pool_1_2), token0, token2),
                    InputAmount = TAmt(token0, 140),
                    OutputAmount = TAmt(token2, 90),
                },
                new()
                {
                    Route = new Route<Token, Token>(P(pool_0_2), token0, token2),
                    InputAmount = TAmt(token0, 16),
                    OutputAmount = TAmt(token2, 10),
                },
            },
            TradeType.EXACT_OUTPUT);
        Assert.Equal("25.5", exactOutMultipleRoutes.PriceImpact.ToSignificant(3));
    }

    // ---------------- #bestTradeExactIn ----------------

    [Fact]
    public async Task BestTradeExactIn_ThrowsWithEmptyPools()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Trade<Token, Token>.BestTradeExactIn(new List<Pool>(), TAmt(token0, 10000), token2));
    }

    [Fact]
    public async Task BestTradeExactIn_ThrowsWithMaxHopsOf0()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Trade<Token, Token>.BestTradeExactIn(P(pool_0_2), TAmt(token0, 10000), token2,
                new Trade<Token, Token>.BestTradeOptions { MaxHops = 0 }));
    }

    [Fact]
    public async Task BestTradeExactIn_ProvidesBestRoute()
    {
        var result = await Trade<Token, Token>.BestTradeExactIn(P(pool_0_1, pool_0_2, pool_1_2), TAmt(token0, 10000), token2);

        Assert.Equal(2, result.Count);
        Assert.Single(result[0].Swaps[0].Route.Pools);
        AssertPath(new List<BaseCurrency> { token0, token2 }, result[0].Swaps[0].Route.CurrencyPath);
        Assert.True(result[0].InputAmount.Equals(TAmt(token0, 10000)));
        Assert.True(result[0].OutputAmount.Equals(TAmt(token2, 9971)));
        Assert.Equal(2, result[1].Swaps[0].Route.Pools.Count);
        AssertPath(new List<BaseCurrency> { token0, token1, token2 }, result[1].Swaps[0].Route.CurrencyPath);
        Assert.True(result[1].InputAmount.Equals(TAmt(token0, 10000)));
        Assert.True(result[1].OutputAmount.Equals(TAmt(token2, 7004)));
    }

    [Fact]
    public async Task BestTradeExactIn_RespectsMaxHops()
    {
        var result = await Trade<Token, Token>.BestTradeExactIn(P(pool_0_1, pool_0_2, pool_1_2), TAmt(token0, 10), token2,
            new Trade<Token, Token>.BestTradeOptions { MaxHops = 1 });
        Assert.Single(result);
        Assert.Single(result[0].Swaps[0].Route.Pools);
        AssertPath(new List<BaseCurrency> { token0, token2 }, result[0].Swaps[0].Route.CurrencyPath);
    }

    [Fact]
    public async Task BestTradeExactIn_InsufficientInputForOnePool()
    {
        var result = await Trade<Token, Token>.BestTradeExactIn(P(pool_0_1, pool_0_2, pool_1_2), TAmt(token0, 1), token2);
        Assert.Equal(2, result.Count);
        Assert.Single(result[0].Swaps[0].Route.Pools);
        AssertPath(new List<BaseCurrency> { token0, token2 }, result[0].Swaps[0].Route.CurrencyPath);
        Assert.Equal(TAmt(token2, 0), result[0].OutputAmount);
    }

    [Fact]
    public async Task BestTradeExactIn_RespectsN()
    {
        var result = await Trade<Token, Token>.BestTradeExactIn(P(pool_0_1, pool_0_2, pool_1_2), TAmt(token0, 10), token2,
            new Trade<Token, Token>.BestTradeOptions { MaxNumResults = 1 });
        Assert.Single(result);
    }

    [Fact]
    public async Task BestTradeExactIn_NoPath()
    {
        var result = await Trade<Token, Token>.BestTradeExactIn(P(pool_0_1, pool_0_3, pool_1_3), TAmt(token0, 10), token2);
        Assert.Empty(result);
    }

    [Fact]
    public async Task BestTradeExactIn_WorksForEtherCurrencyInput()
    {
        var result = await Trade<Ether, Token>.BestTradeExactIn(P(pool_eth_0, pool_0_1, pool_0_3, pool_1_3), EAmt(100), token3);
        Assert.Equal(2, result.Count);
        AssertCurrency(ETHER, result[0].InputAmount.Currency);
        AssertPath(new List<BaseCurrency> { ETHER, token0, token1, token3 }, result[0].Swaps[0].Route.CurrencyPath);
        AssertCurrency(token3, result[0].OutputAmount.Currency);
        AssertCurrency(ETHER, result[1].InputAmount.Currency);
        AssertPath(new List<BaseCurrency> { ETHER, token0, token3 }, result[1].Swaps[0].Route.CurrencyPath);
        AssertCurrency(token3, result[1].OutputAmount.Currency);
    }

    [Fact]
    public async Task BestTradeExactIn_WorksForEtherCurrencyOutput()
    {
        var result = await Trade<Token, Ether>.BestTradeExactIn(P(pool_eth_0, pool_0_1, pool_0_3, pool_1_3), TAmt(token3, 100), ETHER);
        Assert.Equal(2, result.Count);
        AssertCurrency(token3, result[0].InputAmount.Currency);
        AssertPath(new List<BaseCurrency> { token3, token0, ETHER }, result[0].Swaps[0].Route.CurrencyPath);
        AssertCurrency(ETHER, result[0].OutputAmount.Currency);
        AssertCurrency(token3, result[1].InputAmount.Currency);
        AssertPath(new List<BaseCurrency> { token3, token1, token0, ETHER }, result[1].Swaps[0].Route.CurrencyPath);
        AssertCurrency(ETHER, result[1].OutputAmount.Currency);
    }

    // ---------------- #maximumAmountIn ----------------

    private static Task<Trade<Token, Token>> MaxInExactIn() => Trade<Token, Token>.FromRoute(
        new Route<Token, Token>(P(pool_0_1, pool_1_2), token0, token2), TAmt(token0, 100), TradeType.EXACT_INPUT);

    private static Task<Trade<Token, Token>> MaxInExactOut() => Trade<Token, Token>.FromRoute(
        new Route<Token, Token>(P(pool_0_1, pool_1_2), token0, token2), TAmt(token2, 10000), TradeType.EXACT_OUTPUT);

    [Fact]
    public async Task MaximumAmountIn_ExactInput_ThrowsIfLessThan0()
    {
        var exactIn = await MaxInExactIn();
        Assert.Throws<ArgumentException>(() => exactIn.MaximumAmountIn(new Percent(-1, 100)));
    }

    [Fact]
    public async Task MaximumAmountIn_ExactInput_ReturnsExactIf0()
    {
        var exactIn = await MaxInExactIn();
        Assert.Equal(exactIn.InputAmount, exactIn.MaximumAmountIn(new Percent(0, 100)));
    }

    [Fact]
    public async Task MaximumAmountIn_ExactInput_ReturnsExactIfNonzero()
    {
        var exactIn = await MaxInExactIn();
        Assert.True(exactIn.MaximumAmountIn(new Percent(0, 100)).Equals(TAmt(token0, 100)));
        Assert.True(exactIn.MaximumAmountIn(new Percent(5, 100)).Equals(TAmt(token0, 100)));
        Assert.True(exactIn.MaximumAmountIn(new Percent(200, 100)).Equals(TAmt(token0, 100)));
    }

    [Fact]
    public async Task MaximumAmountIn_ExactOutput_ThrowsIfLessThan0()
    {
        var exactOut = await MaxInExactOut();
        Assert.Throws<ArgumentException>(() => exactOut.MaximumAmountIn(new Percent(-1, 10000)));
    }

    [Fact]
    public async Task MaximumAmountIn_ExactOutput_ReturnsExactIf0()
    {
        var exactOut = await MaxInExactOut();
        Assert.Equal(exactOut.InputAmount, exactOut.MaximumAmountIn(new Percent(0, 10000)));
    }

    [Fact]
    public async Task MaximumAmountIn_ExactOutput_ReturnsSlippageAmountIfNonzero()
    {
        var exactOut = await MaxInExactOut();
        Assert.True(exactOut.MaximumAmountIn(new Percent(0, 100)).Equals(TAmt(token0, 15488)));
        Assert.True(exactOut.MaximumAmountIn(new Percent(5, 100)).Equals(TAmt(token0, 16262)));
        Assert.True(exactOut.MaximumAmountIn(new Percent(200, 100)).Equals(TAmt(token0, 46464)));
    }

    // ---------------- #minimumAmountOut ----------------

    private static Task<Trade<Token, Token>> MinOutExactIn() => Trade<Token, Token>.FromRoute(
        new Route<Token, Token>(P(pool_0_1, pool_1_2), token0, token2), TAmt(token0, 10000), TradeType.EXACT_INPUT);

    private static Task<Trade<Token, Token>> MinOutExactOut() => Trade<Token, Token>.FromRoute(
        new Route<Token, Token>(P(pool_0_1, pool_1_2), token0, token2), TAmt(token2, 100), TradeType.EXACT_OUTPUT);

    [Fact]
    public async Task MinimumAmountOut_ExactInput_ThrowsIfLessThan0()
    {
        var exactIn = await MinOutExactIn();
        Assert.Throws<ArgumentException>(() => exactIn.MinimumAmountOut(new Percent(-1, 100)));
    }

    [Fact]
    public async Task MinimumAmountOut_ExactInput_ReturnsExactIf0()
    {
        var exactIn = await MinOutExactIn();
        Assert.Equal(exactIn.OutputAmount, exactIn.MinimumAmountOut(new Percent(0, 10000)));
    }

    [Fact]
    public async Task MinimumAmountOut_ExactInput_ReturnsExactIfNonzero()
    {
        var exactIn = await MinOutExactIn();
        Assert.Equal(TAmt(token2, 7004), exactIn.MinimumAmountOut(new Percent(0, 100)));
        Assert.Equal(TAmt(token2, 6653), exactIn.MinimumAmountOut(new Percent(5, 100)));
        Assert.Equal(TAmt(token2, 0), exactIn.MinimumAmountOut(new Percent(200, 100)));
    }

    [Fact]
    public async Task MinimumAmountOut_ExactOutput_ThrowsIfLessThan0()
    {
        var exactOut = await MinOutExactOut();
        Assert.Throws<ArgumentException>(() => exactOut.MinimumAmountOut(new Percent(-1, 100)));
    }

    [Fact]
    public async Task MinimumAmountOut_ExactOutput_ReturnsExactIf0()
    {
        var exactOut = await MinOutExactOut();
        Assert.Equal(exactOut.OutputAmount, exactOut.MinimumAmountOut(new Percent(0, 100)));
    }

    [Fact]
    public async Task MinimumAmountOut_ExactOutput_ReturnsSlippageAmountIfNonzero()
    {
        var exactOut = await MinOutExactOut();
        Assert.True(exactOut.MinimumAmountOut(new Percent(0, 100)).Equals(TAmt(token2, 100)));
        Assert.True(exactOut.MinimumAmountOut(new Percent(5, 100)).Equals(TAmt(token2, 100)));
        Assert.True(exactOut.MinimumAmountOut(new Percent(200, 100)).Equals(TAmt(token2, 100)));
    }

    // ---------------- #bestTradeExactOut ----------------

    [Fact]
    public async Task BestTradeExactOut_ThrowsWithEmptyPools()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Trade<Token, Token>.BestTradeExactOut(new List<Pool>(), token0, TAmt(token2, 100)));
    }

    [Fact]
    public async Task BestTradeExactOut_ThrowsWithMaxHopsOf0()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Trade<Token, Token>.BestTradeExactOut(P(pool_0_2), token0, TAmt(token2, 100),
                new Trade<Token, Token>.BestTradeOptions { MaxHops = 0 }));
    }

    [Fact]
    public async Task BestTradeExactOut_ProvidesBestRoute()
    {
        var result = await Trade<Token, Token>.BestTradeExactOut(P(pool_0_1, pool_0_2, pool_1_2), token0, TAmt(token2, 10000));
        Assert.Equal(2, result.Count);
        Assert.Single(result[0].Swaps[0].Route.Pools);
        AssertPath(new List<BaseCurrency> { token0, token2 }, result[0].Swaps[0].Route.CurrencyPath);
        Assert.True(result[0].InputAmount.Equals(TAmt(token0, 10032)));
        Assert.True(result[0].OutputAmount.Equals(TAmt(token2, 10000)));
        Assert.Equal(2, result[1].Swaps[0].Route.Pools.Count);
        AssertPath(new List<BaseCurrency> { token0, token1, token2 }, result[1].Swaps[0].Route.CurrencyPath);
        Assert.True(result[1].InputAmount.Equals(TAmt(token0, 15488)));
        Assert.True(result[1].OutputAmount.Equals(TAmt(token2, 10000)));
    }

    [Fact]
    public async Task BestTradeExactOut_RespectsMaxHops()
    {
        var result = await Trade<Token, Token>.BestTradeExactOut(P(pool_0_1, pool_0_2, pool_1_2), token0, TAmt(token2, 10),
            new Trade<Token, Token>.BestTradeOptions { MaxHops = 1 });
        Assert.Single(result);
        Assert.Single(result[0].Swaps[0].Route.Pools);
        AssertPath(new List<BaseCurrency> { token0, token2 }, result[0].Swaps[0].Route.CurrencyPath);
    }

    // NOTE: the upstream `insufficient liquidity` and `insufficient liquidity in one pool but not the
    // other` cases are `it.skip`ped upstream (never executed) and are omitted here for the same reason.

    [Fact]
    public async Task BestTradeExactOut_RespectsN()
    {
        var result = await Trade<Token, Token>.BestTradeExactOut(P(pool_0_1, pool_0_2, pool_1_2), token0, TAmt(token2, 10),
            new Trade<Token, Token>.BestTradeOptions { MaxNumResults = 1 });
        Assert.Single(result);
    }

    [Fact]
    public async Task BestTradeExactOut_NoPath()
    {
        var result = await Trade<Token, Token>.BestTradeExactOut(P(pool_0_1, pool_0_3, pool_1_3), token0, TAmt(token2, 10));
        Assert.Empty(result);
    }

    [Fact]
    public async Task BestTradeExactOut_WorksForEtherCurrencyInput()
    {
        var result = await Trade<Ether, Token>.BestTradeExactOut(P(pool_eth_0, pool_0_1, pool_0_3, pool_1_3), ETHER, TAmt(token3, 10000));
        Assert.Equal(2, result.Count);
        AssertCurrency(ETHER, result[0].InputAmount.Currency);
        AssertPath(new List<BaseCurrency> { ETHER, token0, token1, token3 }, result[0].Swaps[0].Route.CurrencyPath);
        AssertCurrency(token3, result[0].OutputAmount.Currency);
        AssertCurrency(ETHER, result[1].InputAmount.Currency);
        AssertPath(new List<BaseCurrency> { ETHER, token0, token3 }, result[1].Swaps[0].Route.CurrencyPath);
        AssertCurrency(token3, result[1].OutputAmount.Currency);
    }

    [Fact]
    public async Task BestTradeExactOut_WorksForEtherCurrencyOutput()
    {
        var result = await Trade<Token, Ether>.BestTradeExactOut(P(pool_eth_0, pool_0_1, pool_0_3, pool_1_3), token3, EAmt(100));
        Assert.Equal(2, result.Count);
        AssertCurrency(token3, result[0].InputAmount.Currency);
        AssertPath(new List<BaseCurrency> { token3, token0, ETHER }, result[0].Swaps[0].Route.CurrencyPath);
        AssertCurrency(ETHER, result[0].OutputAmount.Currency);
        AssertCurrency(token3, result[1].InputAmount.Currency);
        AssertPath(new List<BaseCurrency> { token3, token1, token0, ETHER }, result[1].Swaps[0].Route.CurrencyPath);
        AssertCurrency(ETHER, result[1].OutputAmount.Currency);
    }
}
