using System.Numerics;
using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.V2.Entities;

namespace UniswapSharp.Testing.V2;

// Port of entities.test.ts. Upstream parametrizes every case over the decimal permutations
// [0,0,0], [0,9,18] and [18,18,18]; we drive the same permutations via [Theory] InlineData.
public class EntitiesTests
{
    private const int CHAIN_ID = 3;
    private static readonly string[] ADDRESSES =
    {
        "0x0000000000000000000000000000000000000001",
        "0x0000000000000000000000000000000000000002",
        "0x0000000000000000000000000000000000000003"
    };
    private static readonly Token WETH9 = Weth9.Tokens[3];

    private static BigInteger Decimalize(long amount, int decimals) => new BigInteger(amount) * BigInteger.Pow(10, decimals);

    private static Token[] BuildTokens(int d0, int d1, int d2)
    {
        return new[]
        {
            new Token(CHAIN_ID, ADDRESSES[0], d0),
            new Token(CHAIN_ID, ADDRESSES[1], d1),
            new Token(CHAIN_ID, ADDRESSES[2], d2)
        };
    }

    private static List<Pair> BuildPairs(Token[] tokens)
    {
        return new List<Pair>
        {
            new(CurrencyAmount<Token>.FromRawAmount(tokens[0], Decimalize(1, tokens[0].Decimals)),
                CurrencyAmount<Token>.FromRawAmount(tokens[1], Decimalize(1, tokens[1].Decimals))),
            new(CurrencyAmount<Token>.FromRawAmount(tokens[1], Decimalize(1, tokens[1].Decimals)),
                CurrencyAmount<Token>.FromRawAmount(tokens[2], Decimalize(1, tokens[2].Decimals))),
            new(CurrencyAmount<Token>.FromRawAmount(tokens[2], Decimalize(1, tokens[2].Decimals)),
                CurrencyAmount<Token>.FromRawAmount(WETH9, Decimalize(1234, WETH9.Decimals)))
        };
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0, 9, 18)]
    [InlineData(18, 18, 18)]
    public void Route(int d0, int d1, int d2)
    {
        var tokens = BuildTokens(d0, d1, d2);
        var pairs = BuildPairs(tokens);
        var route = new Route<Token, Token>(pairs, tokens[0], WETH9);
        Assert.Equal(pairs, route.Pairs);
        Assert.Equal(tokens.Concat(new[] { WETH9 }).ToList(), route.Path);
        Assert.Equal(tokens[0], route.Input);
        Assert.Equal(WETH9, route.Output);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0, 9, 18)]
    [InlineData(18, 18, 18)]
    public void MidPrice(int d0, int d1, int d2)
    {
        var tokens = BuildTokens(d0, d1, d2);
        var route = new Route<Token, Token>(BuildPairs(tokens), tokens[0], WETH9);

        Assert.Equal(
            CurrencyAmount<Token>.FromRawAmount(WETH9, Decimalize(1234, WETH9.Decimals)).ToExact(),
            route.MidPrice.Quote(CurrencyAmount<Token>.FromRawAmount(route.Input, Decimalize(1, route.Input.Decimals))).ToExact());
        Assert.Equal(
            CurrencyAmount<Token>.FromRawAmount(route.Input, Decimalize(1, route.Input.Decimals)).ToExact(),
            route.MidPrice.Invert().Quote(CurrencyAmount<Token>.FromRawAmount(route.Output, Decimalize(1234, route.Output.Decimals))).ToExact());

        Assert.Equal("0.00081037", route.MidPrice.Invert().ToSignificant(5));
        Assert.Equal("1234.00", route.MidPrice.ToFixed(2));
        Assert.Equal("0.00081037", route.MidPrice.Invert().ToFixed(8));
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0, 9, 18)]
    [InlineData(18, 18, 18)]
    public void TradeExactInput(int d0, int d1, int d2)
    {
        var tokens = BuildTokens(d0, d1, d2);
        var route = new Route<Token, Token>(
            new List<Pair>
            {
                new(CurrencyAmount<Token>.FromRawAmount(tokens[1], Decimalize(5, tokens[1].Decimals)),
                    CurrencyAmount<Token>.FromRawAmount(WETH9, Decimalize(10, WETH9.Decimals)))
            },
            tokens[1], WETH9);
        var inputAmount = CurrencyAmount<Token>.FromRawAmount(tokens[1], Decimalize(1, tokens[1].Decimals));
        var expectedOutputAmount = CurrencyAmount<Token>.FromRawAmount(WETH9, BigInteger.Parse("1662497915624478906"));
        var trade = Trade<Token, Token>.ExactIn(route, inputAmount);

        Assert.Same(route, trade.Route);
        Assert.Equal(TradeType.EXACT_INPUT, trade.TradeType);
        AssertAmount(inputAmount, trade.InputAmount);
        AssertAmount(expectedOutputAmount, trade.OutputAmount);

        Assert.Equal("1.66249791562447891", trade.ExecutionPrice.ToSignificant(18));
        Assert.Equal("0.601504513540621866", trade.ExecutionPrice.Invert().ToSignificant(18));
        Assert.Equal(expectedOutputAmount.Quotient, trade.ExecutionPrice.Quote(inputAmount).Quotient);
        Assert.Equal(inputAmount.Quotient, trade.ExecutionPrice.Invert().Quote(expectedOutputAmount).Quotient);

        Assert.Equal("16.8751042187760547", trade.PriceImpact.ToSignificant(18));
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0, 9, 18)]
    [InlineData(18, 18, 18)]
    public void TradeExactOutput(int d0, int d1, int d2)
    {
        var tokens = BuildTokens(d0, d1, d2);
        var route = new Route<Token, Token>(
            new List<Pair>
            {
                new(CurrencyAmount<Token>.FromRawAmount(tokens[1], Decimalize(5, tokens[1].Decimals)),
                    CurrencyAmount<Token>.FromRawAmount(WETH9, Decimalize(10, WETH9.Decimals)))
            },
            tokens[1], WETH9);
        var outputAmount = CurrencyAmount<Token>.FromRawAmount(WETH9, BigInteger.Parse("1662497915624478906"));
        var expectedInputAmount = CurrencyAmount<Token>.FromRawAmount(tokens[1], Decimalize(1, tokens[1].Decimals));
        var trade = Trade<Token, Token>.ExactOut(route, outputAmount);

        Assert.Same(route, trade.Route);
        Assert.Equal(TradeType.EXACT_OUTPUT, trade.TradeType);
        AssertAmount(outputAmount, trade.OutputAmount);
        AssertAmount(expectedInputAmount, trade.InputAmount);

        Assert.Equal("1.66249791562447891", trade.ExecutionPrice.ToSignificant(18));
        Assert.Equal("0.601504513540621866", trade.ExecutionPrice.Invert().ToSignificant(18));
        Assert.Equal(outputAmount.Quotient, trade.ExecutionPrice.Quote(expectedInputAmount).Quotient);
        Assert.Equal(expectedInputAmount.Quotient, trade.ExecutionPrice.Invert().Quote(outputAmount).Quotient);

        Assert.Equal("16.8751042187760547", trade.PriceImpact.ToSignificant(18));
    }

    // minimum TradeType.EXACT_INPUT runs only for decimals 9 and 18 upstream.
    [Theory]
    [InlineData(9, "30090280812437312", "0.300000099400899902")]
    [InlineData(18, "30090270812437322", "0.3000000000000001")]
    public void MinimumExactInput(int d1, string wethExtra, string expectedPriceImpact)
    {
        var token1 = new Token(CHAIN_ID, ADDRESSES[1], d1);
        var route = new Route<Token, Token>(
            new List<Pair>
            {
                new(CurrencyAmount<Token>.FromRawAmount(token1, Decimalize(1, token1.Decimals)),
                    CurrencyAmount<Token>.FromRawAmount(WETH9, Decimalize(10, WETH9.Decimals) + BigInteger.Parse(wethExtra)))
            },
            token1, WETH9);
        var amount = CurrencyAmount<Token>.FromRawAmount(token1, 1);
        var trade = Trade<Token, Token>.ExactIn(route, amount);

        Assert.Equal(expectedPriceImpact, trade.PriceImpact.ToSignificant(18));
    }

    private static void AssertAmount(CurrencyAmount<Token> expected, CurrencyAmount<Token> actual)
    {
        Assert.True(expected.Currency.Equals(actual.Currency));
        Assert.True(expected.Equals(actual));
    }
}
