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

// Ported from sdks/universal-router-sdk/test/unit/nativeErc20Input.test.ts
public class NativeErc20InputTests
{
    private const string TEST_RECIPIENT = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const int V4_ACTION_SETTLE = 0x0b;
    private static readonly BigInteger SCALE_6_TO_18 = BigInteger.Pow(10, 12);

    private static readonly BaseCurrency ETHER = UniswapData.ETHER;
    private static readonly Token WETH = UniswapData.WETH;
    private static readonly Token USDC = UniswapData.USDC;
    private static readonly Token DAI = new(1, "0x6B175474E89094C44Da98b954EedeAC495271d0F", 18, "DAI", "dai");
    private static readonly Token TOKEN_20_DECIMALS = new(1, "0x1111111111111111111111111111111111111111", 20, "T20", "Twenty");

    private static readonly V3Pool USDC_DAI_V3 = UniswapData.MakeV3Pool(USDC, DAI);
    private static readonly V4Pool USDC_DAI_V4 = UniswapData.MakeV4Pool(USDC, DAI);
    private static readonly V3Pool WETH_USDC_V3 = UniswapData.MakeV3Pool(WETH, USDC);

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

    private static SwapOptions Opts(bool? nativeErc20Input = true, TokenTransferMode? mode = null, int? chainId = null, Permit2Permit? permit = null) => new()
    {
        SlippageTolerance = new Percent(5, 100),
        Recipient = TEST_RECIPIENT,
        NativeErc20Input = nativeErc20Input,
        TokenTransferMode = mode,
        ChainId = chainId,
        InputTokenPermit = permit,
    };

    private static (List<int> commandTypes, List<string> inputs) ParseCommands(string calldata)
    {
        string body = "0x" + calldata[10..];
        List<object?> d;
        if (calldata[..10] == SwapRouter.GetSighash("execute(bytes,bytes[],uint256)"))
        {
            d = AbiParamDecoder.Decode(new[] { "bytes", "bytes[]", "uint256" }, body);
        }
        else
        {
            d = AbiParamDecoder.Decode(new[] { "bytes", "bytes[]" }, body);
        }
        var commands = (string)d[0]!;
        var inputs = ((List<object?>)d[1]!).Select(x => (string)x!).ToList();
        var types = new List<int>();
        for (int i = 2; i < commands.Length; i += 2)
        {
            types.Add(Convert.ToInt32(commands.Substring(i, 2), 16) & 0x3f);
        }
        return (types, inputs);
    }

    private static List<object?> DecodeV3Swap(string input) =>
        AbiParamDecoder.Decode(new[] { "address", "uint256", "uint256", "bytes", "bool" }, input);

    private static List<object?> DecodeSweep(string input) =>
        AbiParamDecoder.Decode(new[] { "address", "address", "uint256" }, input);

    private static List<object?> DecodeV4Settle(string input)
    {
        var d = AbiParamDecoder.Decode(new[] { "bytes", "bytes[]" }, input);
        string actionsHex = ((string)d[0]!)[2..];
        var parms = ((List<object?>)d[1]!).Select(x => (string)x!).ToList();
        for (int i = 0; i * 2 < actionsHex.Length; i++)
        {
            if (Convert.ToInt32(actionsHex.Substring(i * 2, 2), 16) == V4_ACTION_SETTLE)
            {
                return AbiParamDecoder.Decode(new[] { "address", "uint256", "bool" }, parms[i]);
            }
        }
        throw new InvalidOperationException("No SETTLE action found in V4_SWAP command");
    }

    private static BigInteger Value(string v) => AbiParamEncoder.ToBigInteger(v);

    // ---- validation ----

    [Fact]
    public void ForcesPayerIsUserFalse()
    {
        var trade = BuildV3Trade(USDC_DAI_V3, USDC, DAI, "1000000", "1000000000000000000");
        Assert.False(new UniswapTrade(trade, Opts()).PayerIsUser);
    }

    [Fact]
    public void ThrowsWhenInputCurrencyIsNative()
    {
        var pool = UniswapData.MakeV4Pool(ETHER, USDC);
        var trade = BuildV4Trade(pool, ETHER, USDC, "1000000000000000000", "1000000");
        Assert.Contains("nativeErc20Input requires an ERC20 input token",
            Assert.Throws<InvalidOperationException>(() => new UniswapTrade(trade, Opts())).Message);
    }

    [Fact]
    public void ThrowsWithApproveProxyMode()
    {
        var trade = BuildV3Trade(USDC_DAI_V3, USDC, DAI, "1000000", "1000000000000000000");
        Assert.Contains("nativeErc20Input is not supported with ApproveProxy",
            Assert.Throws<InvalidOperationException>(() => new UniswapTrade(trade, Opts(mode: TokenTransferMode.ApproveProxy, chainId: 1))).Message);
    }

    [Fact]
    public void ThrowsWhenInputTokenPermitProvided()
    {
        var trade = BuildV3Trade(USDC_DAI_V3, USDC, DAI, "1000000", "1000000000000000000");
        var permit = new Permit2Permit(new UniswapSharp.Permit2.PermitDetails(USDC.Address, 1000000, 0, 0), "0x0000000000000000000000000000000000000000", 0, "0x");
        Assert.Contains("nativeErc20Input does not use Permit2",
            Assert.Throws<InvalidOperationException>(() => new UniswapTrade(trade, Opts(permit: permit))).Message);
    }

    [Fact]
    public void ThrowsWhenV4RouteQuotedAgainstNativePathInput()
    {
        var pool = UniswapData.MakeV4Pool(ETHER, USDC);
        var trade = BuildV4Trade(pool, WETH, USDC, "1000000000000000000", "1000000");
        Assert.Contains("nativeErc20Input requires routes quoted against the ERC20 input",
            Assert.Throws<InvalidOperationException>(() => new UniswapTrade(trade, Opts())).Message);
    }

    // ---- swapCallParameters ----

    [Fact]
    public void AttachesScaledMsgValueForExactInV3()
    {
        const string inputAmount = "1000000";
        var trade = BuildV3Trade(USDC_DAI_V3, USDC, DAI, inputAmount, "1000000000000000000");
        var mp = SwapRouter.SwapCallParameters(trade, Opts());

        Assert.Equal((BigInteger.Parse(inputAmount) * SCALE_6_TO_18).ToString(), Value(mp.Value).ToString());

        var (types, inputs) = ParseCommands(mp.Calldata);
        Assert.Equal(new List<int> { (int)CommandType.V3_SWAP_EXACT_IN }, types);
        Assert.Equal(false, DecodeV3Swap(inputs[0])[4]);
    }

    [Fact]
    public void SweepsUnusedInputForExactOutV3()
    {
        var trade = BuildV3Trade(USDC_DAI_V3, USDC, DAI, "1000000", "1000000000000000000", TradeType.EXACT_OUTPUT);
        var mp = SwapRouter.SwapCallParameters(trade, Opts());

        var (types, inputs) = ParseCommands(mp.Calldata);
        Assert.Equal(new List<int> { (int)CommandType.V3_SWAP_EXACT_OUT, (int)CommandType.SWEEP }, types);

        var swap = DecodeV3Swap(inputs[0]);
        var amountInMax = (BigInteger)swap[2]!;
        Assert.Equal((amountInMax * SCALE_6_TO_18).ToString(), Value(mp.Value).ToString());
        Assert.Equal(false, swap[4]);

        var sweep = DecodeSweep(inputs[1]);
        Assert.Equal(Constants.ETH_ADDRESS.ToLowerInvariant(), ((string)sweep[0]!).ToLowerInvariant());
        Assert.Equal(TEST_RECIPIENT, ((string)sweep[1]!).ToLowerInvariant());
    }

    [Fact]
    public void SettlesV4SwapFromRouterBalance()
    {
        const string inputAmount = "1000000";
        var trade = BuildV4Trade(USDC_DAI_V4, USDC, DAI, inputAmount, "1000000000000000000");
        var mp = SwapRouter.SwapCallParameters(trade, Opts());

        Assert.Equal((BigInteger.Parse(inputAmount) * SCALE_6_TO_18).ToString(), Value(mp.Value).ToString());

        var (types, inputs) = ParseCommands(mp.Calldata);
        Assert.Equal(new List<int> { (int)CommandType.V4_SWAP }, types);

        var settle = DecodeV4Settle(inputs[0]);
        Assert.Equal(USDC.Address.ToLowerInvariant(), ((string)settle[0]!).ToLowerInvariant());
        Assert.Equal(false, settle[2]);
    }

    [Fact]
    public void UsesScaleFactorOfOneFor18Decimals()
    {
        const string inputAmount = "1000000000000000000";
        var trade = BuildV3Trade(WETH_USDC_V3, WETH, USDC, inputAmount, "1000000");
        var mp = SwapRouter.SwapCallParameters(trade, Opts());
        Assert.Equal(inputAmount, Value(mp.Value).ToString());
    }

    [Fact]
    public void ThrowsForMoreThan18Decimals()
    {
        var pool = UniswapData.MakeV3Pool(TOKEN_20_DECIMALS, USDC);
        var trade = BuildV3Trade(pool, TOKEN_20_DECIMALS, USDC, "100000000000000000000", "1000000");
        Assert.Contains("NATIVE_ERC20_INPUT_DECIMALS",
            Assert.Throws<InvalidOperationException>(() => SwapRouter.SwapCallParameters(trade, Opts())).Message);
    }

    [Fact]
    public void LeavesBehaviorUnchangedWhenFlagNotSet()
    {
        var trade = BuildV3Trade(USDC_DAI_V3, USDC, DAI, "1000000", "1000000000000000000");
        Assert.True(new UniswapTrade(trade, Opts(nativeErc20Input: null)).PayerIsUser);

        var mp = SwapRouter.SwapCallParameters(trade, Opts(nativeErc20Input: null));
        Assert.Equal("0x00", mp.Value);
        Assert.Equal(true, DecodeV3Swap(ParseCommands(mp.Calldata).inputs[0])[4]);
    }
}
