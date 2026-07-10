using System.Numerics;
using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.V3;
using UniswapSharp.V3.Entities;
using UniswapSharp.V3.Utils;
using static UniswapSharp.V3.Constants;

namespace UniswapSharp.Testing.V3;

// Ported from sdks/v3-sdk/src/quoter.test.ts
public class SwapQuoterTests
{
    private static readonly Token token0 = new(1, "0x0000000000000000000000000000000000000001", 18, "t0", "token0");
    private static readonly Token token1 = new(1, "0x0000000000000000000000000000000000000002", 18, "t1", "token1");
    private static readonly Token WETH = Weth9.Tokens[1];

    private const FeeAmount feeAmount = FeeAmount.MEDIUM;
    private static readonly BigInteger sqrtRatioX96 = EncodeSqrtRatioX96.Encode(1, 1);
    private const int liquidity = 1_000_000;

    private static Pool MakePool(Token a, Token b) => new(
        a, b, feeAmount, sqrtRatioX96, liquidity, TickMath.GetTickAtSqrtRatio(sqrtRatioX96),
        new List<Tick>
        {
            new(NearestUsableTick.Find(TickMath.MIN_TICK, TICK_SPACINGS[feeAmount]), liquidity, liquidity),
            new(NearestUsableTick.Find(TickMath.MAX_TICK, TICK_SPACINGS[feeAmount]), -liquidity, liquidity),
        });

    private static readonly Pool pool_0_1 = MakePool(token0, token1);
    private static readonly Pool pool_1_weth = MakePool(token1, WETH);

    [Fact]
    public async Task SingleHopExactInputV1()
    {
        var trade = await Trade<Token, Token>.FromRoute(new Route<Token, Token>([pool_0_1], token0, token1), CurrencyAmount<Token>.FromRawAmount(token0, 100), TradeType.EXACT_INPUT);
        var result = SwapQuoter.QuoteCallParameters(trade.Swaps[0].Route, trade.InputAmount.AsBaseCurrency()!, trade.TradeType);
        Assert.Equal(
            "0xf7729d43000000000000000000000000000000000000000000000000000000000000000100000000000000000000000000000000000000000000000000000000000000020000000000000000000000000000000000000000000000000000000000000bb800000000000000000000000000000000000000000000000000000000000000640000000000000000000000000000000000000000000000000000000000000000",
            result.Calldata);
        Assert.Equal("0x00", result.Value);
    }

    [Fact]
    public async Task SingleHopExactOutputV1()
    {
        var trade = await Trade<Token, Token>.FromRoute(new Route<Token, Token>([pool_0_1], token0, token1), CurrencyAmount<Token>.FromRawAmount(token1, 100), TradeType.EXACT_OUTPUT);
        var result = SwapQuoter.QuoteCallParameters(trade.Swaps[0].Route, trade.OutputAmount.AsBaseCurrency()!, trade.TradeType);
        Assert.Equal(
            "0x30d07f21000000000000000000000000000000000000000000000000000000000000000100000000000000000000000000000000000000000000000000000000000000020000000000000000000000000000000000000000000000000000000000000bb800000000000000000000000000000000000000000000000000000000000000640000000000000000000000000000000000000000000000000000000000000000",
            result.Calldata);
        Assert.Equal("0x00", result.Value);
    }

    [Fact]
    public async Task MultiHopExactInputV1()
    {
        var trade = await Trade<Token, Token>.FromRoute(new Route<Token, Token>([pool_0_1, pool_1_weth], token0, WETH), CurrencyAmount<Token>.FromRawAmount(token0, 100), TradeType.EXACT_INPUT);
        var result = SwapQuoter.QuoteCallParameters(trade.Route, trade.InputAmount.AsBaseCurrency()!, trade.TradeType);
        Assert.Equal(
            "0xcdca17530000000000000000000000000000000000000000000000000000000000000040000000000000000000000000000000000000000000000000000000000000006400000000000000000000000000000000000000000000000000000000000000420000000000000000000000000000000000000001000bb80000000000000000000000000000000000000002000bb8c02aaa39b223fe8d0a0e5c4f27ead9083c756cc2000000000000000000000000000000000000000000000000000000000000",
            result.Calldata);
        Assert.Equal("0x00", result.Value);
    }

    [Fact]
    public async Task MultiHopExactOutputV1()
    {
        var trade = await Trade<Token, Token>.FromRoute(new Route<Token, Token>([pool_0_1, pool_1_weth], token0, WETH), CurrencyAmount<Token>.FromRawAmount(WETH, 100), TradeType.EXACT_OUTPUT);
        var result = SwapQuoter.QuoteCallParameters(trade.Route, trade.OutputAmount.AsBaseCurrency()!, trade.TradeType);
        Assert.Equal(
            "0x2f80bb1d000000000000000000000000000000000000000000000000000000000000004000000000000000000000000000000000000000000000000000000000000000640000000000000000000000000000000000000000000000000000000000000042c02aaa39b223fe8d0a0e5c4f27ead9083c756cc2000bb80000000000000000000000000000000000000002000bb80000000000000000000000000000000000000001000000000000000000000000000000000000000000000000000000000000",
            result.Calldata);
        Assert.Equal("0x00", result.Value);
    }

    [Fact]
    public async Task SqrtPriceLimit()
    {
        var trade = await Trade<Token, Token>.FromRoute(new Route<Token, Token>([pool_0_1], token0, token1), CurrencyAmount<Token>.FromRawAmount(token0, 100), TradeType.EXACT_INPUT);
        var result = SwapQuoter.QuoteCallParameters(trade.Route, trade.InputAmount.AsBaseCurrency()!, trade.TradeType, new SwapQuoter.QuoteOptions { SqrtPriceLimitX96 = BigInteger.Pow(2, 128) });
        Assert.Equal(
            "0xf7729d43000000000000000000000000000000000000000000000000000000000000000100000000000000000000000000000000000000000000000000000000000000020000000000000000000000000000000000000000000000000000000000000bb800000000000000000000000000000000000000000000000000000000000000640000000000000000000000000000000100000000000000000000000000000000",
            result.Calldata);
        Assert.Equal("0x00", result.Value);
    }

    [Fact]
    public async Task SingleHopExactOutputV2()
    {
        var trade = await Trade<Token, Token>.FromRoute(new Route<Token, Token>([pool_0_1], token0, token1), CurrencyAmount<Token>.FromRawAmount(token1, 100), TradeType.EXACT_OUTPUT);
        var result = SwapQuoter.QuoteCallParameters(trade.Swaps[0].Route, trade.OutputAmount.AsBaseCurrency()!, trade.TradeType, new SwapQuoter.QuoteOptions { UseQuoterV2 = true });
        Assert.Equal(
            "0xbd21704a0000000000000000000000000000000000000000000000000000000000000001000000000000000000000000000000000000000000000000000000000000000200000000000000000000000000000000000000000000000000000000000000640000000000000000000000000000000000000000000000000000000000000bb80000000000000000000000000000000000000000000000000000000000000000",
            result.Calldata);
        Assert.Equal("0x00", result.Value);
    }

    [Fact]
    public async Task SingleHopExactInputV2()
    {
        var trade = await Trade<Token, Token>.FromRoute(new Route<Token, Token>([pool_0_1], token0, token1), CurrencyAmount<Token>.FromRawAmount(token0, 100), TradeType.EXACT_INPUT);
        var result = SwapQuoter.QuoteCallParameters(trade.Swaps[0].Route, trade.InputAmount.AsBaseCurrency()!, trade.TradeType, new SwapQuoter.QuoteOptions { UseQuoterV2 = true });
        Assert.Equal(
            "0xc6a5026a0000000000000000000000000000000000000000000000000000000000000001000000000000000000000000000000000000000000000000000000000000000200000000000000000000000000000000000000000000000000000000000000640000000000000000000000000000000000000000000000000000000000000bb80000000000000000000000000000000000000000000000000000000000000000",
            result.Calldata);
        Assert.Equal("0x00", result.Value);
    }
}
