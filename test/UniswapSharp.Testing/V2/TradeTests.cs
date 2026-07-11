using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.V2.Entities;

namespace UniswapSharp.Testing.V2;

public class TradeTests
{
    private static readonly Ether ETHER = Ether.OnChain(1);
    private static readonly Token token0 = new(1, "0x0000000000000000000000000000000000000001", 18, "t0");
    private static readonly Token token1 = new(1, "0x0000000000000000000000000000000000000002", 18, "t1");
    private static readonly Token token2 = new(1, "0x0000000000000000000000000000000000000003", 18, "t2");
    private static readonly Token token3 = new(1, "0x0000000000000000000000000000000000000004", 18, "t3");
    private static readonly Token weth = Weth9.Tokens[1];

    private static CurrencyAmount<Token> CA(Token t, long v) => CurrencyAmount<Token>.FromRawAmount(t, v);
    private static CurrencyAmount<Ether> CAE(long v) => CurrencyAmount<Ether>.FromRawAmount(ETHER, v);

    private static readonly Pair pair_0_1 = new(CA(token0, 1000), CA(token1, 1000));
    private static readonly Pair pair_0_2 = new(CA(token0, 1000), CA(token2, 1100));
    private static readonly Pair pair_0_3 = new(CA(token0, 1000), CA(token3, 900));
    private static readonly Pair pair_1_2 = new(CA(token1, 1200), CA(token2, 1000));
    private static readonly Pair pair_1_3 = new(CA(token1, 1200), CA(token3, 1300));
    private static readonly Pair pair_weth_0 = new(CA(weth, 1000), CA(token0, 1000));
    private static readonly Pair empty_pair_0_1 = new(CA(token0, 0), CA(token1, 0));

    // ---- construction with ETHER ----

    [Fact]
    public void CanBeConstructedWithEtherAsInput()
    {
        var trade = Trade<Ether, Token>.ExactIn(new Route<Ether, Token>(new List<Pair> { pair_weth_0 }, ETHER, token0), CAE(100));
        Assert.True(trade.InputAmount.Currency.Equals(ETHER));
        Assert.True(trade.OutputAmount.Currency.Equals(token0));
    }

    [Fact]
    public void CanBeConstructedWithEtherAsInputForExactOutput()
    {
        var trade = Trade<Ether, Token>.ExactOut(new Route<Ether, Token>(new List<Pair> { pair_weth_0 }, ETHER, token0), CA(token0, 100));
        Assert.True(trade.InputAmount.Currency.Equals(ETHER));
        Assert.True(trade.OutputAmount.Currency.Equals(token0));
    }

    [Fact]
    public void CanBeConstructedWithEtherAsOutput()
    {
        var trade = Trade<Token, Ether>.ExactOut(new Route<Token, Ether>(new List<Pair> { pair_weth_0 }, token0, ETHER), CAE(100));
        Assert.True(trade.InputAmount.Currency.Equals(token0));
        Assert.True(trade.OutputAmount.Currency.Equals(ETHER));
    }

    [Fact]
    public void CanBeConstructedWithEtherAsOutputForExactInput()
    {
        var trade = Trade<Token, Ether>.ExactIn(new Route<Token, Ether>(new List<Pair> { pair_weth_0 }, token0, ETHER), CA(token0, 100));
        Assert.True(trade.InputAmount.Currency.Equals(token0));
        Assert.True(trade.OutputAmount.Currency.Equals(ETHER));
    }

    // ---- bestTradeExactIn ----

    [Fact]
    public void BestTradeExactIn_ThrowsWithEmptyPairs()
    {
        var ex = Assert.Throws<ArgumentException>(() => Trade<Token, Token>.BestTradeExactIn(new List<Pair>(), CA(token0, 100), token2));
        Assert.Equal("PAIRS", ex.Message);
    }

    [Fact]
    public void BestTradeExactIn_ThrowsWithMaxHopsZero()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Trade<Token, Token>.BestTradeExactIn(new List<Pair> { pair_0_2 }, CA(token0, 100), token2, new BestTradeOptions { MaxHops = 0 }));
        Assert.Equal("MAX_HOPS", ex.Message);
    }

    [Fact]
    public void BestTradeExactIn_ProvidesBestRoute()
    {
        var result = Trade<Token, Token>.BestTradeExactIn(new List<Pair> { pair_0_1, pair_0_2, pair_1_2 }, CA(token0, 100), token2);
        Assert.Equal(2, result.Count);
        Assert.Single(result[0].Route.Pairs); // 0 -> 2 at 10:11
        Assert.Equal(new List<Token> { token0, token2 }, result[0].Route.Path);
        AssertAmount(CA(token0, 100), result[0].InputAmount);
        AssertAmount(CA(token2, 99), result[0].OutputAmount);
        Assert.Equal(2, result[1].Route.Pairs.Count); // 0 -> 1 -> 2 at 12:12:10
        Assert.Equal(new List<Token> { token0, token1, token2 }, result[1].Route.Path);
        AssertAmount(CA(token0, 100), result[1].InputAmount);
        AssertAmount(CA(token2, 69), result[1].OutputAmount);
    }

    [Fact]
    public void BestTradeExactIn_DoesntThrowForZeroLiquidityPairs()
    {
        Assert.Empty(Trade<Token, Token>.BestTradeExactIn(new List<Pair> { empty_pair_0_1 }, CA(token0, 100), token1));
    }

    [Fact]
    public void BestTradeExactIn_RespectsMaxHops()
    {
        var result = Trade<Token, Token>.BestTradeExactIn(
            new List<Pair> { pair_0_1, pair_0_2, pair_1_2 }, CA(token0, 10), token2, new BestTradeOptions { MaxHops = 1 });
        Assert.Single(result);
        Assert.Single(result[0].Route.Pairs); // 0 -> 2 at 10:11
        Assert.Equal(new List<Token> { token0, token2 }, result[0].Route.Path);
    }

    [Fact]
    public void BestTradeExactIn_InsufficientInputForOnePair()
    {
        var result = Trade<Token, Token>.BestTradeExactIn(new List<Pair> { pair_0_1, pair_0_2, pair_1_2 }, CA(token0, 1), token2);
        Assert.Single(result);
        Assert.Single(result[0].Route.Pairs); // 0 -> 2 at 10:11
        Assert.Equal(new List<Token> { token0, token2 }, result[0].Route.Path);
        AssertAmount(CA(token2, 1), result[0].OutputAmount);
    }

    [Fact]
    public void BestTradeExactIn_RespectsN()
    {
        var result = Trade<Token, Token>.BestTradeExactIn(
            new List<Pair> { pair_0_1, pair_0_2, pair_1_2 }, CA(token0, 10), token2, new BestTradeOptions { MaxNumResults = 1 });
        Assert.Single(result);
    }

    [Fact]
    public void BestTradeExactIn_NoPath()
    {
        var result = Trade<Token, Token>.BestTradeExactIn(new List<Pair> { pair_0_1, pair_0_3, pair_1_3 }, CA(token0, 10), token2);
        Assert.Empty(result);
    }

    [Fact]
    public void BestTradeExactIn_WorksForEtherCurrencyInput()
    {
        var result = Trade<Ether, Token>.BestTradeExactIn(
            new List<Pair> { pair_weth_0, pair_0_1, pair_0_3, pair_1_3 }, CAE(100), token3);
        Assert.Equal(2, result.Count);
        Assert.True(result[0].InputAmount.Currency.Equals(ETHER));
        Assert.Equal(new List<Token> { weth, token0, token1, token3 }, result[0].Route.Path);
        Assert.True(result[0].OutputAmount.Currency.Equals(token3));
        Assert.True(result[1].InputAmount.Currency.Equals(ETHER));
        Assert.Equal(new List<Token> { weth, token0, token3 }, result[1].Route.Path);
        Assert.True(result[1].OutputAmount.Currency.Equals(token3));
    }

    [Fact]
    public void BestTradeExactIn_WorksForEtherCurrencyOutput()
    {
        var result = Trade<Token, Ether>.BestTradeExactIn(
            new List<Pair> { pair_weth_0, pair_0_1, pair_0_3, pair_1_3 }, CA(token3, 100), ETHER);
        Assert.Equal(2, result.Count);
        Assert.True(result[0].InputAmount.Currency.Equals(token3));
        Assert.Equal(new List<Token> { token3, token0, weth }, result[0].Route.Path);
        Assert.True(result[0].OutputAmount.Currency.Equals(ETHER));
        Assert.True(result[1].InputAmount.Currency.Equals(token3));
        Assert.Equal(new List<Token> { token3, token1, token0, weth }, result[1].Route.Path);
        Assert.True(result[1].OutputAmount.Currency.Equals(ETHER));
    }

    // ---- maximumAmountIn ----

    private static Trade<Token, Token> ExactInTrade() =>
        Trade<Token, Token>.ExactIn(new Route<Token, Token>(new List<Pair> { pair_0_1, pair_1_2 }, token0, token2), CA(token0, 100));

    private static Trade<Token, Token> ExactOutTrade() =>
        Trade<Token, Token>.ExactOut(new Route<Token, Token>(new List<Pair> { pair_0_1, pair_1_2 }, token0, token2), CA(token2, 100));

    [Fact]
    public void MaximumAmountIn_ExactInput_ThrowsIfLessThanZero()
    {
        Assert.Throws<ArgumentException>(() => ExactInTrade().MaximumAmountIn(new Percent(-1, 100)));
    }

    [Fact]
    public void MaximumAmountIn_ExactInput_ReturnsExactIfZeroAndNonzero()
    {
        var exactIn = ExactInTrade();
        AssertAmount(exactIn.InputAmount, exactIn.MaximumAmountIn(new Percent(0, 100)));
        AssertAmount(CA(token0, 100), exactIn.MaximumAmountIn(new Percent(0, 100)));
        AssertAmount(CA(token0, 100), exactIn.MaximumAmountIn(new Percent(5, 100)));
        AssertAmount(CA(token0, 100), exactIn.MaximumAmountIn(new Percent(200, 100)));
    }

    [Fact]
    public void MaximumAmountIn_ExactOutput_ThrowsIfLessThanZero()
    {
        Assert.Throws<ArgumentException>(() => ExactOutTrade().MaximumAmountIn(new Percent(-1, 100)));
    }

    [Fact]
    public void MaximumAmountIn_ExactOutput_ReturnsSlippageAmounts()
    {
        var exactOut = ExactOutTrade();
        AssertAmount(exactOut.InputAmount, exactOut.MaximumAmountIn(new Percent(0, 100)));
        AssertAmount(CA(token0, 156), exactOut.MaximumAmountIn(new Percent(0, 100)));
        AssertAmount(CA(token0, 163), exactOut.MaximumAmountIn(new Percent(5, 100)));
        AssertAmount(CA(token0, 468), exactOut.MaximumAmountIn(new Percent(200, 100)));
    }

    // ---- minimumAmountOut ----

    [Fact]
    public void MinimumAmountOut_ExactInput_ThrowsIfLessThanZero()
    {
        Assert.Throws<ArgumentException>(() => ExactInTrade().MinimumAmountOut(new Percent(-1, 100)));
    }

    [Fact]
    public void MinimumAmountOut_ExactInput_ReturnsSlippageAmounts()
    {
        var exactIn = ExactInTrade();
        AssertAmount(exactIn.OutputAmount, exactIn.MinimumAmountOut(new Percent(0, 100)));
        AssertAmount(CA(token2, 69), exactIn.MinimumAmountOut(new Percent(0, 100)));
        AssertAmount(CA(token2, 65), exactIn.MinimumAmountOut(new Percent(5, 100)));
        AssertAmount(CA(token2, 0), exactIn.MinimumAmountOut(new Percent(200, 100)));
    }

    [Fact]
    public void MinimumAmountOut_ExactOutput_ThrowsIfLessThanZero()
    {
        Assert.Throws<ArgumentException>(() => ExactOutTrade().MinimumAmountOut(new Percent(-1, 100)));
    }

    [Fact]
    public void MinimumAmountOut_ExactOutput_ReturnsExactRegardlessOfSlippage()
    {
        var exactOut = ExactOutTrade();
        AssertAmount(exactOut.OutputAmount, exactOut.MinimumAmountOut(new Percent(0, 100)));
        AssertAmount(CA(token2, 100), exactOut.MinimumAmountOut(new Percent(0, 100)));
        AssertAmount(CA(token2, 100), exactOut.MinimumAmountOut(new Percent(5, 100)));
        AssertAmount(CA(token2, 100), exactOut.MinimumAmountOut(new Percent(200, 100)));
    }

    // ---- worstExecutionPrice ----

    [Fact]
    public void WorstExecutionPrice_ExactInput()
    {
        var exactIn = ExactInTrade();
        Assert.Throws<ArgumentException>(() => exactIn.MinimumAmountOut(new Percent(-1, 100)));
        Assert.True(exactIn.ExecutionPrice.Equals(exactIn.WorstExecutionPrice(new Percent(0, 100))));
        AssertPrice(token0, token2, 100, 69, exactIn.WorstExecutionPrice(new Percent(0, 100)));
        AssertPrice(token0, token2, 100, 65, exactIn.WorstExecutionPrice(new Percent(5, 100)));
        AssertPrice(token0, token2, 100, 0, exactIn.WorstExecutionPrice(new Percent(200, 100)));
    }

    [Fact]
    public void WorstExecutionPrice_ExactOutput()
    {
        var exactOut = ExactOutTrade();
        Assert.Throws<ArgumentException>(() => exactOut.WorstExecutionPrice(new Percent(-1, 100)));
        Assert.True(exactOut.ExecutionPrice.Equals(exactOut.WorstExecutionPrice(new Percent(0, 100))));
        AssertPrice(token0, token2, 156, 100, exactOut.WorstExecutionPrice(new Percent(0, 100)));
        AssertPrice(token0, token2, 163, 100, exactOut.WorstExecutionPrice(new Percent(5, 100)));
        AssertPrice(token0, token2, 468, 100, exactOut.WorstExecutionPrice(new Percent(200, 100)));
    }

    // ---- bestTradeExactOut ----

    [Fact]
    public void BestTradeExactOut_ThrowsWithEmptyPairs()
    {
        var ex = Assert.Throws<ArgumentException>(() => Trade<Token, Token>.BestTradeExactOut(new List<Pair>(), token0, CA(token2, 100)));
        Assert.Equal("PAIRS", ex.Message);
    }

    [Fact]
    public void BestTradeExactOut_ThrowsWithMaxHopsZero()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Trade<Token, Token>.BestTradeExactOut(new List<Pair> { pair_0_2 }, token0, CA(token2, 100), new BestTradeOptions { MaxHops = 0 }));
        Assert.Equal("MAX_HOPS", ex.Message);
    }

    [Fact]
    public void BestTradeExactOut_ProvidesBestRoute()
    {
        var result = Trade<Token, Token>.BestTradeExactOut(new List<Pair> { pair_0_1, pair_0_2, pair_1_2 }, token0, CA(token2, 100));
        Assert.Equal(2, result.Count);
        Assert.Single(result[0].Route.Pairs); // 0 -> 2 at 10:11
        Assert.Equal(new List<Token> { token0, token2 }, result[0].Route.Path);
        AssertAmount(CA(token0, 101), result[0].InputAmount);
        AssertAmount(CA(token2, 100), result[0].OutputAmount);
        Assert.Equal(2, result[1].Route.Pairs.Count); // 0 -> 1 -> 2 at 12:12:10
        Assert.Equal(new List<Token> { token0, token1, token2 }, result[1].Route.Path);
        AssertAmount(CA(token0, 156), result[1].InputAmount);
        AssertAmount(CA(token2, 100), result[1].OutputAmount);
    }

    [Fact]
    public void BestTradeExactOut_DoesntThrowForZeroLiquidityPairs()
    {
        Assert.Empty(Trade<Token, Token>.BestTradeExactOut(new List<Pair> { empty_pair_0_1 }, token1, CA(token1, 100)));
    }

    [Fact]
    public void BestTradeExactOut_RespectsMaxHops()
    {
        var result = Trade<Token, Token>.BestTradeExactOut(
            new List<Pair> { pair_0_1, pair_0_2, pair_1_2 }, token0, CA(token2, 10), new BestTradeOptions { MaxHops = 1 });
        Assert.Single(result);
        Assert.Single(result[0].Route.Pairs); // 0 -> 2 at 10:11
        Assert.Equal(new List<Token> { token0, token2 }, result[0].Route.Path);
    }

    [Fact]
    public void BestTradeExactOut_InsufficientLiquidity()
    {
        var result = Trade<Token, Token>.BestTradeExactOut(new List<Pair> { pair_0_1, pair_0_2, pair_1_2 }, token0, CA(token2, 1200));
        Assert.Empty(result);
    }

    [Fact]
    public void BestTradeExactOut_InsufficientLiquidityInOnePairButNotTheOther()
    {
        var result = Trade<Token, Token>.BestTradeExactOut(new List<Pair> { pair_0_1, pair_0_2, pair_1_2 }, token0, CA(token2, 1050));
        Assert.Single(result);
    }

    [Fact]
    public void BestTradeExactOut_RespectsN()
    {
        var result = Trade<Token, Token>.BestTradeExactOut(
            new List<Pair> { pair_0_1, pair_0_2, pair_1_2 }, token0, CA(token2, 10), new BestTradeOptions { MaxNumResults = 1 });
        Assert.Single(result);
    }

    [Fact]
    public void BestTradeExactOut_NoPath()
    {
        var result = Trade<Token, Token>.BestTradeExactOut(new List<Pair> { pair_0_1, pair_0_3, pair_1_3 }, token0, CA(token2, 10));
        Assert.Empty(result);
    }

    [Fact]
    public void BestTradeExactOut_WorksForEtherCurrencyInput()
    {
        var result = Trade<Ether, Token>.BestTradeExactOut(
            new List<Pair> { pair_weth_0, pair_0_1, pair_0_3, pair_1_3 }, ETHER, CA(token3, 100));
        Assert.Equal(2, result.Count);
        Assert.True(result[0].InputAmount.Currency.Equals(ETHER));
        Assert.Equal(new List<Token> { weth, token0, token1, token3 }, result[0].Route.Path);
        Assert.True(result[0].OutputAmount.Currency.Equals(token3));
        Assert.True(result[1].InputAmount.Currency.Equals(ETHER));
        Assert.Equal(new List<Token> { weth, token0, token3 }, result[1].Route.Path);
        Assert.True(result[1].OutputAmount.Currency.Equals(token3));
    }

    [Fact]
    public void BestTradeExactOut_WorksForEtherCurrencyOutput()
    {
        var result = Trade<Token, Ether>.BestTradeExactOut(
            new List<Pair> { pair_weth_0, pair_0_1, pair_0_3, pair_1_3 }, token3, CAE(100));
        Assert.Equal(2, result.Count);
        Assert.True(result[0].InputAmount.Currency.Equals(token3));
        Assert.Equal(new List<Token> { token3, token0, weth }, result[0].Route.Path);
        Assert.True(result[0].OutputAmount.Currency.Equals(ETHER));
        Assert.True(result[1].InputAmount.Currency.Equals(token3));
        Assert.Equal(new List<Token> { token3, token1, token0, weth }, result[1].Route.Path);
        Assert.True(result[1].OutputAmount.Currency.Equals(ETHER));
    }

    // ---- helpers ----

    private static void AssertAmount<T>(CurrencyAmount<T> expected, CurrencyAmount<T> actual) where T : BaseCurrency
    {
        Assert.True(expected.Currency.Equals(actual.Currency));
        Assert.True(expected.Equals(actual));
    }

    private static void AssertPrice(Token expBase, Token expQuote, long denominator, long numerator, Price<Token, Token> actual)
    {
        Assert.True(expBase.Equals(actual.BaseCurrency));
        Assert.True(expQuote.Equals(actual.QuoteCurrency));
        Assert.True(new Price<Token, Token>(expBase, expQuote, denominator, numerator).Equals(actual));
    }
}
