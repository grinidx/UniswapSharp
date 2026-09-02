using System.Numerics;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.UniversalRouter;
using UniswapSharp.UniversalRouter.Utils;
using UniswapSharp.V4.Utils;
using Constants = UniswapSharp.UniversalRouter.Utils.Constants;
using TradeType = UniswapSharp.Core.TradeType;
using V4Pool = UniswapSharp.V4.Entities.Pool;
using V4Route = UniswapSharp.V4.Entities.Route<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;
using V4RouteInput = UniswapSharp.V4.Entities.RouteInput<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;
using V4TradeT = UniswapSharp.V4.Entities.Trade<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;

namespace UniswapSharp.Testing.UniversalRouter;

// Ported 1:1 from sdks/universal-router-sdk/test/unit/v4ExactOutputTake.test.ts (upstream #640, ROUTE-1394)
public class V4ExactOutputTakeTests
{
    private static readonly V4Pool WETH_USDC_V4 = UniswapData.MakeV4Pool(UniswapData.WETH, UniswapData.USDC);

    private static readonly BigInteger ONE_ETH = BigInteger.Pow(10, 18);
    private static readonly BigInteger ONE_USDC = BigInteger.Pow(10, 6);

    private static CurrencyAmount<BaseCurrency> Amt(BaseCurrency c, BigInteger v) =>
        CurrencyAmount<BaseCurrency>.FromRawAmount(c, v);

    private static V4TradeT V4Trade(BaseCurrency input, BigInteger inAmt, BaseCurrency output, BigInteger outAmt, TradeType tradeType) =>
        V4TradeT.CreateUncheckedTrade(new V4RouteInput
        {
            Route = new V4Route(new List<V4Pool> { WETH_USDC_V4 }, input, output),
            InputAmount = Amt(input, inAmt),
            OutputAmount = Amt(output, outAmt),
        }, tradeType);

    private static (List<int> commandTypes, List<string> inputs) ParseCommands(string calldata)
    {
        string body = "0x" + calldata[10..];
        var d = calldata[..10] == SwapRouter.GetSighash("execute(bytes,bytes[],uint256)")
            ? AbiParamDecoder.Decode(new[] { "bytes", "bytes[]", "uint256" }, body)
            : AbiParamDecoder.Decode(new[] { "bytes", "bytes[]" }, body);

        var commands = (string)d[0]!;
        var inputs = ((List<object?>)d[1]!).Select(x => (string)x!).ToList();
        var types = new List<int>();
        for (int i = 2; i < commands.Length; i += 2)
        {
            types.Add(Convert.ToInt32(commands.Substring(i, 2), 16) & 0x3f);
        }
        return (types, inputs);
    }

    private static IReadOnlyList<V4RouterAction> GetV4Actions(string calldata)
    {
        var (types, inputs) = ParseCommands(calldata);
        int v4Idx = types.IndexOf((int)CommandType.V4_SWAP);
        Assert.NotEqual(-1, v4Idx);
        return V4BaseActionsParser.ParseCalldata(inputs[v4Idx]).Actions;
    }

    private static object? GetParam(V4RouterAction action, string name) =>
        action.Params.Single(p => p.Name == name).Value;

    private static void ExpectTake(IReadOnlyList<V4RouterAction> actions, string currency, string recipient, BigInteger amount)
    {
        var take = actions.Single(a => a.ActionName == "TAKE");
        Assert.Equal(currency.ToLowerInvariant(), ((string)GetParam(take, "currency")!).ToLowerInvariant());
        Assert.Equal(recipient.ToLowerInvariant(), ((string)GetParam(take, "recipient")!).ToLowerInvariant());
        Assert.Equal(amount, (BigInteger)GetParam(take, "amount")!);
    }

    [Fact]
    public void SingleRouteV4ExactOutput_TakeAmountEqualsEncodedAmountOut()
    {
        var trade = V4Trade(UniswapData.WETH, ONE_ETH, UniswapData.USDC, ONE_USDC, TradeType.EXACT_OUTPUT);
        var routerTrade = UniswapData.BuildTrade(new object[] { trade });

        var mp = SwapRouter.SwapCallParameters(routerTrade,
            UniswapData.SwapOptions(recipient: UniswapData.TEST_RECIPIENT_ADDRESS));

        ExpectTake(GetV4Actions(mp.Calldata), UniswapData.USDC.Address, UniswapData.TEST_RECIPIENT_ADDRESS, ONE_USDC);
    }

    [Fact]
    public void ExactInputV4Encoding_StillUsesOpenDeltaTakeAmountZero()
    {
        var trade = V4Trade(UniswapData.WETH, ONE_ETH, UniswapData.USDC, ONE_USDC, TradeType.EXACT_INPUT);
        var routerTrade = UniswapData.BuildTrade(new object[] { trade });

        var mp = SwapRouter.SwapCallParameters(routerTrade,
            UniswapData.SwapOptions(recipient: UniswapData.TEST_RECIPIENT_ADDRESS));

        // OPEN_DELTA is the zero sentinel; exact-input encoding must not change.
        ExpectTake(GetV4Actions(mp.Calldata), UniswapData.USDC.Address, UniswapData.TEST_RECIPIENT_ADDRESS, BigInteger.Zero);
    }

    [Fact]
    public void ExactOutputWithFee_FloorsTakeToRouter()
    {
        var trade = V4Trade(UniswapData.WETH, ONE_ETH, UniswapData.USDC, ONE_USDC, TradeType.EXACT_OUTPUT);
        var routerTrade = UniswapData.BuildTrade(new object[] { trade });

        var mp = SwapRouter.SwapCallParameters(routerTrade, UniswapData.SwapOptions(
            fee: new UniswapSharp.V3.Payments.FeeOptions
            {
                Fee = new Percent(5, 100),
                Recipient = UniswapData.TEST_FEE_RECIPIENT_ADDRESS,
            },
            recipient: UniswapData.TEST_RECIPIENT_ADDRESS));

        // The router must custody to pay the fee, so TAKE floors to the router, not the recipient.
        ExpectTake(GetV4Actions(mp.Calldata), UniswapData.USDC.Address, Constants.ROUTER_AS_RECIPIENT, ONE_USDC);
    }

    [Fact]
    public void RejectsZeroExactOutputAmount_BeforeEncodingZeroSentinelTake()
    {
        var trade = V4Trade(UniswapData.WETH, ONE_ETH, UniswapData.USDC, BigInteger.Zero, TradeType.EXACT_OUTPUT);
        var routerTrade = UniswapData.BuildTrade(new object[] { trade });

        var ex = Assert.Throws<ArgumentException>(() => SwapRouter.SwapCallParameters(routerTrade,
            UniswapData.SwapOptions(recipient: UniswapData.TEST_RECIPIENT_ADDRESS)));

        Assert.Contains("ZERO_EXACT_OUTPUT_AMOUNT", ex.Message);
    }
}
