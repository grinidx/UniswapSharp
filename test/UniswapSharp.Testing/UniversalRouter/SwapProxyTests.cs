using System.Numerics;
using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.UniversalRouter;
using UniswapSharp.UniversalRouter.Entities.Actions;
using UniswapSharp.UniversalRouter.Utils;
using UniswapSharp.V4.Utils;
using Constants = UniswapSharp.UniversalRouter.Utils.Constants;
using RouterTrade = UniswapSharp.Router.Entities.Trade<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;
using V3Pool = UniswapSharp.V3.Entities.Pool;
using V3Route = UniswapSharp.V3.Entities.Route<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;
using V3Trade = UniswapSharp.V3.Entities.Trade<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;
using V4Pool = UniswapSharp.V4.Entities.Pool;
using V4Route = UniswapSharp.V4.Entities.Route<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;
using V4Trade = UniswapSharp.V4.Entities.Trade<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;

namespace UniswapSharp.Testing.UniversalRouter;

// Ported from sdks/universal-router-sdk/test/unit/swapProxy.test.ts
public class SwapProxyTests
{
    private const string TEST_RECIPIENT = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static readonly BaseCurrency ETHER = UniswapData.ETHER;
    private static readonly Token WETH = UniswapData.WETH;
    private static readonly Token USDC = UniswapData.USDC;

    private static readonly V4Pool WETH_USDC_V4 = UniswapData.MakeV4Pool(WETH, USDC);
    private static readonly V3Pool WETH_USDC_V3 = UniswapData.MakeV3Pool(WETH, USDC);

    private static RouterTrade BuildV4Trade(V4Pool pool, BaseCurrency inCur, BaseCurrency outCur, string inAmt, string outAmt, TradeType type = TradeType.EXACT_INPUT)
    {
        var trade = V4Trade.CreateUncheckedTrade(new UniswapSharp.V4.Entities.RouteInput<BaseCurrency, BaseCurrency>
        {
            Route = new V4Route(new List<V4Pool> { pool }, inCur, outCur),
            InputAmount = CurrencyAmount<BaseCurrency>.FromRawAmount(inCur, BigInteger.Parse(inAmt)),
            OutputAmount = CurrencyAmount<BaseCurrency>.FromRawAmount(outCur, BigInteger.Parse(outAmt)),
        }, type);
        return UniswapData.BuildTrade(new object[] { trade });
    }

    private static RouterTrade BuildV3Trade(V3Pool pool, BaseCurrency inCur, BaseCurrency outCur, string inAmt, string outAmt, TradeType type = TradeType.EXACT_INPUT)
    {
        var trade = V3Trade.CreateUncheckedTrade(new UniswapSharp.V3.Entities.RouteInput<BaseCurrency, BaseCurrency>
        {
            Route = new V3Route(new List<V3Pool> { pool }, inCur, outCur),
            InputAmount = CurrencyAmount<BaseCurrency>.FromRawAmount(inCur, BigInteger.Parse(inAmt)),
            OutputAmount = CurrencyAmount<BaseCurrency>.FromRawAmount(outCur, BigInteger.Parse(outAmt)),
        }, type);
        return UniswapData.BuildTrade(new object[] { trade });
    }

    private static SwapOptions ProxyOptions(string? recipient = TEST_RECIPIENT, int? chainId = 1, Permit2Permit? permit = null, Percent? slippage = null) =>
        new()
        {
            SlippageTolerance = slippage ?? new Percent(5, 100),
            Recipient = recipient,
            TokenTransferMode = TokenTransferMode.ApproveProxy,
            ChainId = chainId,
            InputTokenPermit = permit,
            DeadlineOrPreviousBlockhash = 1_700_001_800,
        };

    private static List<object?> DecodeProxy(string calldata)
    {
        string body = "0x" + calldata[10..];
        return AbiParamDecoder.Decode(new[] { "address", "address", "uint256", "bytes", "bytes[]", "uint256" }, body);
    }

    [Fact]
    public void UniswapTradeApproveProxy_ForcesPayerIsUserFalse()
    {
        var trade = BuildV4Trade(WETH_USDC_V4, USDC, WETH, "1000000", "500000000000000000");
        var uniTrade = new UniswapTrade(trade, ProxyOptions());
        Assert.False(uniTrade.PayerIsUser);
    }

    [Fact]
    public void UniswapTradeApproveProxy_ThrowsWhenRecipientNotProvided()
    {
        var trade = BuildV4Trade(WETH_USDC_V4, USDC, WETH, "1000000", "500000000000000000");
        var ex = Assert.Throws<InvalidOperationException>(() => new UniswapTrade(trade, ProxyOptions(recipient: null)));
        Assert.Contains("Explicit recipient address required", ex.Message);
    }

    [Fact]
    public void UniswapTradeApproveProxy_ThrowsWhenRecipientIsSender()
    {
        var trade = BuildV4Trade(WETH_USDC_V4, USDC, WETH, "1000000", "500000000000000000");
        var ex = Assert.Throws<InvalidOperationException>(() => new UniswapTrade(trade, ProxyOptions(recipient: Constants.SENDER_AS_RECIPIENT)));
        Assert.Contains("Explicit recipient address required", ex.Message);
    }

    [Fact]
    public void SwapCallParameters_EncodesProxyExecuteV4()
    {
        var trade = BuildV4Trade(WETH_USDC_V4, USDC, WETH, "1000000", "500000000000000000");
        var mp = SwapRouter.SwapCallParameters(trade, ProxyOptions());
        Assert.Equal("0x00", mp.Value);

        var decoded = DecodeProxy(mp.Calldata);
        Assert.Equal(Constants.UNIVERSAL_ROUTER_ADDRESS(UniversalRouterVersion.V2_0, 1).ToLowerInvariant(), ((string)decoded[0]!).ToLowerInvariant());
        Assert.Equal(USDC.Address.ToLowerInvariant(), ((string)decoded[1]!).ToLowerInvariant());
        Assert.True(((List<object?>)decoded[4]!).Count > 0);
    }

    [Fact]
    public void SwapCallParameters_EncodesProxyExecuteV3()
    {
        var trade = BuildV3Trade(WETH_USDC_V3, USDC, WETH, "1000000", "500000000000000000");
        var mp = SwapRouter.SwapCallParameters(trade, ProxyOptions());
        Assert.Equal("0x00", mp.Value);

        var decoded = DecodeProxy(mp.Calldata);
        Assert.Equal(Constants.UNIVERSAL_ROUTER_ADDRESS(UniversalRouterVersion.V2_0, 1).ToLowerInvariant(), ((string)decoded[0]!).ToLowerInvariant());
        Assert.Equal(USDC.Address.ToLowerInvariant(), ((string)decoded[1]!).ToLowerInvariant());
    }

    [Fact]
    public void SwapCallParameters_NoPermit2CommandsInProxyCalldata()
    {
        var trade = BuildV4Trade(WETH_USDC_V4, USDC, WETH, "1000000", "500000000000000000");
        var mp = SwapRouter.SwapCallParameters(trade, ProxyOptions());
        var commands = (string)DecodeProxy(mp.Calldata)[3]!;
        Assert.DoesNotContain("02", commands);
        Assert.DoesNotContain("03", commands);
        Assert.DoesNotContain("0a", commands);
        Assert.DoesNotContain("0d", commands);
    }

    [Fact]
    public void SwapCallParameters_ThrowsWhenNativeInput()
    {
        var ethUsdc = UniswapData.MakeV4Pool(ETHER, USDC);
        var trade = BuildV4Trade(ethUsdc, ETHER, USDC, "1000000000000000000", "1000000");
        var ex = Assert.Throws<InvalidOperationException>(() => SwapRouter.SwapCallParameters(trade, ProxyOptions()));
        Assert.Contains("PROXY_NATIVE_INPUT", ex.Message);
    }

    [Fact]
    public void SwapCallParameters_ThrowsWhenChainIdMissing()
    {
        var trade = BuildV4Trade(WETH_USDC_V4, USDC, WETH, "1000000", "500000000000000000");
        var ex = Assert.Throws<InvalidOperationException>(() => SwapRouter.SwapCallParameters(trade, ProxyOptions(chainId: null)));
        Assert.Contains("PROXY_MISSING_CHAIN_ID", ex.Message);
    }

    [Fact]
    public void SwapCallParameters_ThrowsWhenInputTokenPermitProvided()
    {
        var trade = BuildV4Trade(WETH_USDC_V4, USDC, WETH, "1000000", "500000000000000000");
        var permit = new Permit2Permit(new UniswapSharp.Permit2.PermitDetails(USDC.Address, 1000000, 0, 0), "0x0000000000000000000000000000000000000000", 0, "0x");
        var ex = Assert.Throws<InvalidOperationException>(() => SwapRouter.SwapCallParameters(trade, ProxyOptions(permit: permit)));
        Assert.Contains("PROXY_PERMIT_CONFLICT", ex.Message);
    }

    [Fact]
    public void SwapCallParameters_SetsCorrectInputAmountForSlippage()
    {
        var trade = BuildV4Trade(WETH_USDC_V4, USDC, WETH, "1000000", "500000000000000000");
        var opts = ProxyOptions(slippage: new Percent(5, 100));
        var mp = SwapRouter.SwapCallParameters(trade, opts);
        var decoded = DecodeProxy(mp.Calldata);
        var maxAmountIn = trade.MaximumAmountIn(opts.SlippageTolerance);
        Assert.Equal(maxAmountIn.Quotient.ToString(), ((BigInteger)decoded[2]!).ToString());
    }

    [Fact]
    public void SwapCallParameters_ReturnsNormalUrCalldataWhenDefault()
    {
        var trade = BuildV4Trade(WETH_USDC_V4, USDC, WETH, "1000000", "500000000000000000");
        var opts = new SwapOptions
        {
            SlippageTolerance = new Percent(5, 100),
            Recipient = TEST_RECIPIENT,
            DeadlineOrPreviousBlockhash = 1_700_001_800,
        };
        var mp = SwapRouter.SwapCallParameters(trade, opts);
        // decodes as UR execute with deadline (selector), not proxy execute
        Assert.StartsWith(SwapRouter.GetSighash("execute(bytes,bytes[],uint256)"), mp.Calldata);
    }
}
