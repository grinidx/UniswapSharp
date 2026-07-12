using System.Numerics;
using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Router.Entities;
using UniswapSharp.UniversalRouter.Utils;
using UniswapSharp.V3.Utils;

namespace UniswapSharp.Testing.UniversalRouter;

// Ported from the RouterTradeAdapter blocks of sdks/universal-router-sdk/test/uniswapTrades.test.ts.
// (Upstream's success-path assertion `compareUniswapTrades` is a no-op; here the structural result of
// fromClassicQuote is checked instead. Synthetic static pool states replace the fork-fetched pools.)
public class RouterTradeAdapterTests
{
    private static readonly Token DAI = UniswapData.DAI;
    private static readonly Token USDC = UniswapData.USDC;
    private static readonly Token WETH = UniswapData.WETH;
    private const string ETH_ADDRESS = "0x0000000000000000000000000000000000000000";
    private static readonly string SQRT_1_1 = EncodeSqrtRatioX96.Encode(1, 1).ToString();

    private static TokenInRoute Tir(Token t) => new(t.Address, 1, t.Symbol!, t.Decimals.ToString());

    private static V2PoolInRoute MockV2(Token tokenIn, Token tokenOut, string amountIn, string amountOut)
    {
        var token0 = tokenIn.SortsBefore(tokenOut) ? tokenIn : tokenOut;
        var token1 = tokenIn.SortsBefore(tokenOut) ? tokenOut : tokenIn;
        return new V2PoolInRoute
        {
            TokenIn = Tir(tokenIn),
            TokenOut = Tir(tokenOut),
            Reserve0 = new V2Reserve(Tir(token0), "1000000000000000000000000"),
            Reserve1 = new V2Reserve(Tir(token1), "1000000000000"),
            AmountIn = amountIn,
            AmountOut = amountOut,
        };
    }

    private static V3PoolInRoute MockV3(Token tokenIn, Token tokenOut, string amountIn, string amountOut) => new()
    {
        TokenIn = Tir(tokenIn),
        TokenOut = Tir(tokenOut),
        SqrtRatioX96 = SQRT_1_1,
        Liquidity = "1000000000000000000000000",
        TickCurrent = "0",
        Fee = "3000",
        AmountIn = amountIn,
        AmountOut = amountOut,
    };

    private static V4PoolInRoute MockV4(BaseCurrency tokenIn, BaseCurrency tokenOut, string amountIn, string amountOut) => new()
    {
        TokenIn = new TokenInRoute(tokenIn.IsNative ? ETH_ADDRESS : tokenIn.Wrapped().Address, 1, tokenIn.Symbol!, tokenIn.Decimals.ToString()),
        TokenOut = new TokenInRoute(tokenOut.IsNative ? ETH_ADDRESS : tokenOut.Wrapped().Address, 1, tokenOut.Symbol!, tokenOut.Decimals.ToString()),
        Fee = "3000",
        TickSpacing = "60",
        Hooks = ETH_ADDRESS,
        SqrtRatioX96 = SQRT_1_1,
        Liquidity = "1000000000000000000000000",
        TickCurrent = "0",
        AmountIn = amountIn,
        AmountOut = amountOut,
    };

    [Fact]
    public void V2_Erc20ToErc20()
    {
        var quote = new PartialClassicQuote(DAI.Address, USDC.Address, TradeType.EXACT_INPUT,
            new List<List<PoolInRoute>> { new() { MockV2(DAI, USDC, "1000000000000000000000", "1000000000") } });
        var trade = RouterTradeAdapter.FromClassicQuote(quote);
        Assert.Single(trade.Routes);
        Assert.Equal(Protocol.V2, trade.Routes[0].Protocol);
        Assert.True(trade.InputAmount.Currency.Equals(DAI));
        Assert.True(trade.OutputAmount.Currency.Equals(USDC));
    }

    [Fact]
    public void V3_Erc20ToErc20()
    {
        var quote = new PartialClassicQuote(DAI.Address, USDC.Address, TradeType.EXACT_INPUT,
            new List<List<PoolInRoute>> { new() { MockV3(DAI, USDC, "1000000000000000000000", "1000000000") } });
        var trade = RouterTradeAdapter.FromClassicQuote(quote);
        Assert.Equal(Protocol.V3, trade.Routes[0].Protocol);
        Assert.True(trade.InputAmount.Currency.Equals(DAI));
    }

    [Fact]
    public void V4_Erc20ToErc20()
    {
        var quote = new PartialClassicQuote(DAI.Address, USDC.Address, TradeType.EXACT_INPUT,
            new List<List<PoolInRoute>> { new() { MockV4(DAI, USDC, "1000000000000000000000", "1000000000") } });
        var trade = RouterTradeAdapter.FromClassicQuote(quote);
        Assert.Equal(Protocol.V4, trade.Routes[0].Protocol);
    }

    [Fact]
    public void Mixed_V3ThenV2()
    {
        var quote = new PartialClassicQuote(WETH.Address, DAI.Address, TradeType.EXACT_INPUT,
            new List<List<PoolInRoute>>
            {
                new() { MockV3(WETH, USDC, "1000000000000000000", "1000000000"), MockV2(USDC, DAI, "1000000000", "1000000000000000000000") },
            });
        var trade = RouterTradeAdapter.FromClassicQuote(quote);
        Assert.Equal(Protocol.MIXED, trade.Routes[0].Protocol);
    }

    // ---- malformed ----

    [Fact]
    public void Throws_OnMissingRoute()
    {
        var quote = new PartialClassicQuote(WETH.Address, USDC.Address, TradeType.EXACT_INPUT, null);
        Assert.Contains("Expected route to be present",
            Assert.Throws<InvalidOperationException>(() => RouterTradeAdapter.FromClassicQuote(quote)).Message);
    }

    [Fact]
    public void Throws_OnNoRoute()
    {
        var quote = new PartialClassicQuote(WETH.Address, USDC.Address, TradeType.EXACT_INPUT, new List<List<PoolInRoute>>());
        Assert.Contains("Expected there to be at least one route",
            Assert.Throws<InvalidOperationException>(() => RouterTradeAdapter.FromClassicQuote(quote)).Message);
    }

    [Fact]
    public void Throws_OnRouteWithNoPools()
    {
        var quote = new PartialClassicQuote(WETH.Address, USDC.Address, TradeType.EXACT_INPUT,
            new List<List<PoolInRoute>> { new() });
        Assert.Contains("Expected all routes to have at least one pool",
            Assert.Throws<InvalidOperationException>(() => RouterTradeAdapter.FromClassicQuote(quote)).Message);
    }

    [Fact]
    public void Throws_OnMissingTokenInOut()
    {
        var pool = MockV2(DAI, USDC, "1000", "1000") with { TokenIn = null };
        var quote = new PartialClassicQuote(WETH.Address, USDC.Address, TradeType.EXACT_INPUT,
            new List<List<PoolInRoute>> { new() { pool } });
        Assert.Contains("Expected both tokenIn and tokenOut to be present",
            Assert.Throws<InvalidOperationException>(() => RouterTradeAdapter.FromClassicQuote(quote)).Message);
    }

    [Fact]
    public void Throws_OnMismatchedChainId()
    {
        var pool = MockV2(DAI, USDC, "1000", "1000") with { TokenIn = new TokenInRoute(DAI.Address, 2, DAI.Symbol!, DAI.Decimals.ToString()) };
        var quote = new PartialClassicQuote(DAI.Address, USDC.Address, TradeType.EXACT_INPUT,
            new List<List<PoolInRoute>> { new() { pool } });
        Assert.Contains("Expected tokenIn and tokenOut to be have same chainId",
            Assert.Throws<InvalidOperationException>(() => RouterTradeAdapter.FromClassicQuote(quote)).Message);
    }

    [Fact]
    public void Throws_OnMissingAmountInOut()
    {
        var pool = MockV2(DAI, USDC, "1000", "1000") with { AmountIn = null };
        var quote = new PartialClassicQuote(WETH.Address, USDC.Address, TradeType.EXACT_INPUT,
            new List<List<PoolInRoute>> { new() { pool } });
        Assert.Contains("Expected both raw amountIn and raw amountOut to be present",
            Assert.Throws<InvalidOperationException>(() => RouterTradeAdapter.FromClassicQuote(quote)).Message);
    }
}
