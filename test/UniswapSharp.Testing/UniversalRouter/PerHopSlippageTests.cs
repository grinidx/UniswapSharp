using System.Numerics;
using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.Router.Entities;
using UniswapSharp.Router.Entities.MixedRoute;
using UniswapSharp.UniversalRouter;
using UniswapSharp.UniversalRouter.Entities.Actions;
using UniswapSharp.UniversalRouter.Utils;
using UniswapSharp.V4.Utils;
using CB = UniswapSharp.Core.Entities.Fractions.CurrencyAmount<UniswapSharp.Core.Entities.BaseCurrency>;
using MixedSDK = UniswapSharp.Router.Entities.MixedRoute.MixedRouteSDK<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;
using RouterTrade = UniswapSharp.Router.Entities.Trade<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;
using V2Pair = UniswapSharp.V2.Entities.Pair;
using V2Route = UniswapSharp.V2.Entities.Route<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;
using V3Pool = UniswapSharp.V3.Entities.Pool;
using V3Route = UniswapSharp.V3.Entities.Route<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;
using V4Pool = UniswapSharp.V4.Entities.Pool;
using V4Route = UniswapSharp.V4.Entities.Route<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;
using V4Trade = UniswapSharp.V4.Entities.Trade<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;

namespace UniswapSharp.Testing.UniversalRouter;

// Ported from sdks/universal-router-sdk/test/unit/perHopSlippage.test.ts
public class PerHopSlippageTests
{
    private static readonly Token WETH = UniswapData.WETH;
    private static readonly Token USDC = UniswapData.USDC;
    private static readonly Token DAI = UniswapData.DAI;

    private static readonly V3Pool WETH_USDC_V3 = UniswapData.MakeV3Pool(WETH, USDC);
    private static readonly V3Pool USDC_DAI_V3 = UniswapData.MakeV3Pool(USDC, DAI);
    private static readonly V4Pool WETH_USDC_V4 = UniswapData.MakeV4Pool(WETH, USDC);
    private static readonly V4Pool USDC_DAI_V4 = UniswapData.MakeV4Pool(USDC, DAI);

    private static readonly V2Pair WETH_USDC_V2 = new(
        CurrencyAmount<Token>.FromRawAmount(WETH, BigInteger.Parse("1000000000000000000")),
        CurrencyAmount<Token>.FromRawAmount(USDC, BigInteger.Parse("1000000000000")));
    private static readonly V2Pair USDC_DAI_V2 = new(
        CurrencyAmount<Token>.FromRawAmount(USDC, BigInteger.Parse("1000000000000")),
        CurrencyAmount<Token>.FromRawAmount(DAI, BigInteger.Parse("1000000000000000000000000")));

    private const string ONE_ETH = "1000000000000000000";
    private const string ONE_USDC = "1000000";
    private const string RECIPIENT = "0x0000000000000000000000000000000000000001";

    private static CB Amt(BaseCurrency c, string raw) => CB.FromRawAmount(c, BigInteger.Parse(raw));

    private static (List<int> types, List<string> inputs) ParseCommands(string calldata)
    {
        string body = "0x" + calldata[10..];
        List<object?> d = calldata[..10] == SwapRouter.GetSighash("execute(bytes,bytes[],uint256)")
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

    private static List<string> DecodeMinHop(string[] types, string input, int idx)
    {
        var decoded = AbiParamDecoder.Decode(types, input);
        return ((List<object?>)decoded[idx]!).Select(v => ((BigInteger)v!).ToString()).ToList();
    }

    private static readonly string[] V2Types6 = { "address", "uint256", "uint256", "address[]", "bool", "uint256[]" };
    private static readonly string[] V3Types6 = { "address", "uint256", "uint256", "bytes", "bool", "uint256[]" };

    private static SwapOptions Opts(UniversalRouterVersion? v) => UniswapData.SwapOptions(urVersion: v);

    // ---- V2 routes with V2_1_1 ----

    [Fact]
    public void V2ExactInput_EncodesMinHop()
    {
        var route = new V2Route(new List<V2Pair> { WETH_USDC_V2, USDC_DAI_V2 }, WETH, DAI);
        var trade = new RouterTrade(TradeType.EXACT_INPUT, v2Routes: new[]
        {
            new V2RouteAmounts<BaseCurrency, BaseCurrency>(route, Amt(WETH, ONE_ETH), Amt(DAI, ONE_ETH), new BigInteger[] { 500, 600 }),
        });
        var (types, inputs) = ParseCommands(SwapRouter.SwapCallParameters(trade, Opts(UniversalRouterVersion.V2_1_1)).Calldata);
        int idx = types.IndexOf((int)CommandType.V2_SWAP_EXACT_IN);
        Assert.NotEqual(-1, idx);
        Assert.Equal(new List<string> { "500", "600" }, DecodeMinHop(V2Types6, inputs[idx], 5));
    }

    [Fact]
    public void V2ExactOutput_EncodesMinHop()
    {
        var route = new V2Route(new List<V2Pair> { WETH_USDC_V2 }, WETH, USDC);
        var trade = new RouterTrade(TradeType.EXACT_OUTPUT, v2Routes: new[]
        {
            new V2RouteAmounts<BaseCurrency, BaseCurrency>(route, Amt(WETH, ONE_ETH), Amt(USDC, ONE_USDC), new BigInteger[] { 250 }),
        });
        var (types, inputs) = ParseCommands(SwapRouter.SwapCallParameters(trade, Opts(UniversalRouterVersion.V2_1_1)).Calldata);
        int idx = types.IndexOf((int)CommandType.V2_SWAP_EXACT_OUT);
        Assert.NotEqual(-1, idx);
        Assert.Equal(new List<string> { "250" }, DecodeMinHop(V2Types6, inputs[idx], 5));
    }

    // ---- V3 routes with V2_1_1 ----

    [Fact]
    public void V3ExactInput_EncodesMinHop()
    {
        var route = new V3Route(new List<V3Pool> { WETH_USDC_V3, USDC_DAI_V3 }, WETH, DAI);
        var trade = new RouterTrade(TradeType.EXACT_INPUT, v3Routes: new[]
        {
            new V3RouteAmounts<BaseCurrency, BaseCurrency>(route, Amt(WETH, ONE_ETH), Amt(DAI, ONE_ETH), new BigInteger[] { 300, 400 }),
        });
        var (types, inputs) = ParseCommands(SwapRouter.SwapCallParameters(trade, Opts(UniversalRouterVersion.V2_1_1)).Calldata);
        int idx = types.IndexOf((int)CommandType.V3_SWAP_EXACT_IN);
        Assert.NotEqual(-1, idx);
        Assert.Equal(new List<string> { "300", "400" }, DecodeMinHop(V3Types6, inputs[idx], 5));
    }

    [Fact]
    public void V3ExactOutput_EncodesMinHop()
    {
        var route = new V3Route(new List<V3Pool> { WETH_USDC_V3 }, WETH, USDC);
        var trade = new RouterTrade(TradeType.EXACT_OUTPUT, v3Routes: new[]
        {
            new V3RouteAmounts<BaseCurrency, BaseCurrency>(route, Amt(WETH, ONE_ETH), Amt(USDC, ONE_USDC), new BigInteger[] { 750 }),
        });
        var (types, inputs) = ParseCommands(SwapRouter.SwapCallParameters(trade, Opts(UniversalRouterVersion.V2_1_1)).Calldata);
        int idx = types.IndexOf((int)CommandType.V3_SWAP_EXACT_OUT);
        Assert.NotEqual(-1, idx);
        Assert.Equal(new List<string> { "750" }, DecodeMinHop(V3Types6, inputs[idx], 5));
    }

    // ---- V4 routes with V2_1_1 ----

    private static RouterTrade V4Trade2Pool(string outRaw, TradeType type, BigInteger[]? minHop, params V4Pool[] pools)
    {
        var (inCur, outCur) = (WETH, pools.Length == 2 ? DAI : USDC);
        var route = new V4Route(pools.ToList(), inCur, outCur);
        var v4t = V4Trade.CreateUncheckedTrade(new UniswapSharp.V4.Entities.RouteInput<BaseCurrency, BaseCurrency>
        {
            Route = route,
            InputAmount = Amt(WETH, ONE_ETH),
            OutputAmount = Amt(outCur, outRaw),
        }, type);
        return new RouterTrade(type, v4Routes: new[]
        {
            new V4RouteAmounts<BaseCurrency, BaseCurrency>(v4t.Route, v4t.InputAmount, v4t.OutputAmount, minHop),
        });
    }

    private static (SwapExactIn? sei, SwapExactOut? seo) DecodeV4Swap(string calldata)
    {
        var (types, inputs) = ParseCommands(calldata);
        int idx = types.IndexOf((int)CommandType.V4_SWAP);
        Assert.NotEqual(-1, idx);
        var parsed = V4BaseActionsParser.ParseCalldata(inputs[idx], URVersion.V2_1_1);
        var sei = parsed.Actions.FirstOrDefault(a => a.ActionName == "SWAP_EXACT_IN");
        var seo = parsed.Actions.FirstOrDefault(a => a.ActionName == "SWAP_EXACT_OUT");
        return (sei is null ? null : (SwapExactIn)sei.Params[0].Value!, seo is null ? null : (SwapExactOut)seo.Params[0].Value!);
    }

    [Fact]
    public void V4ExactInput_EncodesMinHop()
    {
        var trade = V4Trade2Pool(ONE_ETH, TradeType.EXACT_INPUT, new BigInteger[] { 1000, 2000 }, WETH_USDC_V4, USDC_DAI_V4);
        var (sei, _) = DecodeV4Swap(SwapRouter.SwapCallParameters(trade, Opts(UniversalRouterVersion.V2_1_1)).Calldata);
        Assert.Equal(new List<string> { "1000", "2000" }, sei!.MinHopPriceX36!.Select(v => v.ToString()).ToList());
    }

    [Fact]
    public void V4ExactOutput_EncodesMinHop()
    {
        var trade = V4Trade2Pool(ONE_USDC, TradeType.EXACT_OUTPUT, new BigInteger[] { 3000 }, WETH_USDC_V4);
        var (_, seo) = DecodeV4Swap(SwapRouter.SwapCallParameters(trade, Opts(UniversalRouterVersion.V2_1_1)).Calldata);
        Assert.Equal(new List<string> { "3000" }, seo!.MinHopPriceX36!.Select(v => v.ToString()).ToList());
    }

    // ---- Mixed routes with V2_1_1 ----

    private static RouterTrade MixedTrade(BigInteger[]? minHop)
    {
        var route = new MixedSDK(new List<object> { WETH_USDC_V3, USDC_DAI_V2 }, WETH, DAI);
        return new RouterTrade(TradeType.EXACT_INPUT, mixedRoutes: new[]
        {
            new MixedRouteAmounts<BaseCurrency, BaseCurrency>(route, Amt(WETH, ONE_ETH), Amt(DAI, ONE_ETH), minHop),
        });
    }

    [Fact]
    public void Mixed_SlicesMinHopAcrossSections()
    {
        var (types, inputs) = ParseCommands(SwapRouter.SwapCallParameters(MixedTrade(new BigInteger[] { 100, 200 }), Opts(UniversalRouterVersion.V2_1_1)).Calldata);
        int v3 = types.IndexOf((int)CommandType.V3_SWAP_EXACT_IN);
        int v2 = types.IndexOf((int)CommandType.V2_SWAP_EXACT_IN);
        Assert.NotEqual(-1, v3);
        Assert.NotEqual(-1, v2);
        Assert.Equal(new List<string> { "100" }, DecodeMinHop(V3Types6, inputs[v3], 5));
        Assert.Equal(new List<string> { "200" }, DecodeMinHop(V2Types6, inputs[v2], 5));
    }

    [Fact]
    public void Mixed_UndefinedMinHopEncodesEmptyArraysPerSection()
    {
        var (types, inputs) = ParseCommands(SwapRouter.SwapCallParameters(MixedTrade(null), Opts(UniversalRouterVersion.V2_1_1)).Calldata);
        int v3 = types.IndexOf((int)CommandType.V3_SWAP_EXACT_IN);
        int v2 = types.IndexOf((int)CommandType.V2_SWAP_EXACT_IN);
        Assert.Empty(DecodeMinHop(V3Types6, inputs[v3], 5));
        Assert.Empty(DecodeMinHop(V2Types6, inputs[v2], 5));
    }

    // ---- Backwards compatibility: V2_0 drops minHop ----

    [Fact]
    public void V2_0_V2Route_DropsMinHop()
    {
        var route = new V2Route(new List<V2Pair> { WETH_USDC_V2, USDC_DAI_V2 }, WETH, DAI);
        var trade = new RouterTrade(TradeType.EXACT_INPUT, v2Routes: new[]
        {
            new V2RouteAmounts<BaseCurrency, BaseCurrency>(route, Amt(WETH, ONE_ETH), Amt(DAI, ONE_ETH), new BigInteger[] { 500, 600 }),
        });
        var (types, inputs) = ParseCommands(SwapRouter.SwapCallParameters(trade, Opts(UniversalRouterVersion.V2_0)).Calldata);
        int idx = types.IndexOf((int)CommandType.V2_SWAP_EXACT_IN);
        var decoded = AbiParamDecoder.Decode(new[] { "address", "uint256", "uint256", "address[]", "bool" }, inputs[idx]);
        Assert.Equal(5, decoded.Count);
    }

    [Fact]
    public void V2_0_V3Route_DropsMinHop()
    {
        var route = new V3Route(new List<V3Pool> { WETH_USDC_V3, USDC_DAI_V3 }, WETH, DAI);
        var trade = new RouterTrade(TradeType.EXACT_INPUT, v3Routes: new[]
        {
            new V3RouteAmounts<BaseCurrency, BaseCurrency>(route, Amt(WETH, ONE_ETH), Amt(DAI, ONE_ETH), new BigInteger[] { 300, 400 }),
        });
        var (types, inputs) = ParseCommands(SwapRouter.SwapCallParameters(trade, Opts(UniversalRouterVersion.V2_0)).Calldata);
        int idx = types.IndexOf((int)CommandType.V3_SWAP_EXACT_IN);
        var decoded = AbiParamDecoder.Decode(new[] { "address", "uint256", "uint256", "bytes", "bool" }, inputs[idx]);
        Assert.Equal(5, decoded.Count);
    }

    [Fact]
    public void Default_NoUrVersion_DropsMinHop()
    {
        var route = new V2Route(new List<V2Pair> { WETH_USDC_V2, USDC_DAI_V2 }, WETH, DAI);
        var trade = new RouterTrade(TradeType.EXACT_INPUT, v2Routes: new[]
        {
            new V2RouteAmounts<BaseCurrency, BaseCurrency>(route, Amt(WETH, ONE_ETH), Amt(DAI, ONE_ETH), new BigInteger[] { 500, 600 }),
        });
        var (types, inputs) = ParseCommands(SwapRouter.SwapCallParameters(trade, UniswapData.SwapOptions()).Calldata);
        int idx = types.IndexOf((int)CommandType.V2_SWAP_EXACT_IN);
        var decoded = AbiParamDecoder.Decode(new[] { "address", "uint256", "uint256", "address[]", "bool" }, inputs[idx]);
        Assert.Equal(5, decoded.Count);
    }

    [Fact]
    public void ExplicitV2_0_IdenticalToDefault()
    {
        var p1 = new RoutePlanner();
        p1.AddCommand(CommandType.V2_SWAP_EXACT_IN, new object?[] { RECIPIENT, BigInteger.Parse(ONE_ETH), BigInteger.Parse(ONE_ETH), new object?[] { WETH.Address, USDC.Address }, true });
        var p2 = new RoutePlanner();
        p2.AddCommand(CommandType.V2_SWAP_EXACT_IN, new object?[] { RECIPIENT, BigInteger.Parse(ONE_ETH), BigInteger.Parse(ONE_ETH), new object?[] { WETH.Address, USDC.Address }, true }, false, UniversalRouterVersion.V2_0);
        Assert.Equal(p2.Inputs[0], p1.Inputs[0]);
        Assert.Equal(p2.Commands, p1.Commands);
    }

    // ---- V2_1_1 without minHop encodes empty array ----

    [Fact]
    public void V2_1_1_V2Route_EmptyMinHop()
    {
        var route = new V2Route(new List<V2Pair> { WETH_USDC_V2, USDC_DAI_V2 }, WETH, DAI);
        var trade = new RouterTrade(TradeType.EXACT_INPUT, v2Routes: new[]
        {
            new V2RouteAmounts<BaseCurrency, BaseCurrency>(route, Amt(WETH, ONE_ETH), Amt(DAI, ONE_ETH)),
        });
        var (types, inputs) = ParseCommands(SwapRouter.SwapCallParameters(trade, Opts(UniversalRouterVersion.V2_1_1)).Calldata);
        int idx = types.IndexOf((int)CommandType.V2_SWAP_EXACT_IN);
        Assert.Empty(DecodeMinHop(V2Types6, inputs[idx], 5));
    }

    [Fact]
    public void V2_1_1_V4Route_EmptyMinHop()
    {
        var trade = V4Trade2Pool(ONE_USDC, TradeType.EXACT_INPUT, null, WETH_USDC_V4);
        var (sei, _) = DecodeV4Swap(SwapRouter.SwapCallParameters(trade, Opts(UniversalRouterVersion.V2_1_1)).Calldata);
        Assert.Empty(sei!.MinHopPriceX36!);
    }

    // ---- Low-level RoutePlanner & createCommand ----

    private static readonly BigInteger Amount = BigInteger.Parse(ONE_ETH);
    private static string V3Path => "0x" + WETH.Address[2..] + "000bb8" + USDC.Address[2..];

    [Fact]
    public void V2_1_1_RoutePlanner_V2SwapExactIn6Params()
    {
        var planner = new RoutePlanner();
        planner.AddCommand(CommandType.V2_SWAP_EXACT_IN,
            new object?[] { RECIPIENT, Amount, Amount, new object?[] { WETH.Address, USDC.Address }, true, new object?[] { "1000", "2000" } },
            false, UniversalRouterVersion.V2_1_1);
        Assert.Single(planner.Inputs);
        Assert.Equal(new List<string> { "1000", "2000" }, DecodeMinHop(V2Types6, planner.Inputs[0], 5));
    }

    [Fact]
    public void V2_1_1_RoutePlanner_V3SwapExactIn6Params()
    {
        var planner = new RoutePlanner();
        planner.AddCommand(CommandType.V3_SWAP_EXACT_IN,
            new object?[] { RECIPIENT, Amount, Amount, V3Path, true, new object?[] { "3000", "4000", "5000" } },
            false, UniversalRouterVersion.V2_1_1);
        Assert.Equal(new List<string> { "3000", "4000", "5000" }, DecodeMinHop(V3Types6, planner.Inputs[0], 5));
    }

    [Fact]
    public void V2_0_RoutePlanner_Only5Params()
    {
        var planner = new RoutePlanner();
        planner.AddCommand(CommandType.V2_SWAP_EXACT_IN, new object?[] { RECIPIENT, Amount, Amount, new object?[] { WETH.Address, USDC.Address }, true });
        var decoded = AbiParamDecoder.Decode(new[] { "address", "uint256", "uint256", "address[]", "bool" }, planner.Inputs[0]);
        Assert.Equal(5, decoded.Count);
    }

    [Fact]
    public void CreateCommand_V2_1_1UsesExtendedAbi()
    {
        var command = RoutePlanner.CreateCommand(CommandType.V2_SWAP_EXACT_IN,
            new object?[] { RECIPIENT, Amount, Amount, new object?[] { WETH.Address, USDC.Address }, true, new object?[] { "1000" } },
            UniversalRouterVersion.V2_1_1);
        Assert.Equal(CommandType.V2_SWAP_EXACT_IN, command.Type);
        Assert.Equal(new List<string> { "1000" }, DecodeMinHop(V2Types6, command.EncodedInput, 5));
    }

    [Fact]
    public void CreateCommand_NoUrVersionUsesBaseAbi()
    {
        var command = RoutePlanner.CreateCommand(CommandType.V2_SWAP_EXACT_IN,
            new object?[] { RECIPIENT, Amount, Amount, new object?[] { WETH.Address, USDC.Address }, true });
        var decoded = AbiParamDecoder.Decode(new[] { "address", "uint256", "uint256", "address[]", "bool" }, command.EncodedInput);
        Assert.Equal(5, decoded.Count);
    }

    [Fact]
    public void CreateCommand_NonSwapUnaffectedByV2_1_1()
    {
        var command = RoutePlanner.CreateCommand(CommandType.WRAP_ETH, new object?[] { RECIPIENT, Amount }, UniversalRouterVersion.V2_1_1);
        var decoded = AbiParamDecoder.Decode(new[] { "address", "uint256" }, command.EncodedInput);
        Assert.Equal(2, decoded.Count);
    }
}
