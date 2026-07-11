using System.Numerics;
using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.V3.Utils;
using UniswapSharp.V4.Entities;
using UniswapSharp.V4.Utils;
using Constants = UniswapSharp.V4.Constants;
using EncodeRouteToPath = UniswapSharp.V4.Utils.EncodeRouteToPath;
using Pool = UniswapSharp.V4.Entities.Pool;
using Tick = UniswapSharp.V3.Entities.Tick;

namespace UniswapSharp.Testing.V4.Utils;

// Ported 1:1 from sdks/v4-sdk/src/utils/v4Planner.test.ts.
// Route/Trade are the open-generic V4 types (UniswapSharp.V4.Entities), which cannot be aliased,
// so the V3 Entities namespace is deliberately not imported; Pool and Tick are aliased instead.
public class V4PlannerTests
{
    private const int MEDIUM = Constants.FEE_AMOUNT_MEDIUM;
    private const int TEN = Constants.TICK_SPACING_TEN;
    private const string ADDRESS_ZERO = Constants.ADDRESS_ZERO;

    private static readonly BigInteger ONE_ETHER = BigInteger.Pow(10, 18);

    private static readonly Ether ETHER = Ether.OnChain(1);
    private static readonly Token WETH = Weth9.Tokens[1];
    private static readonly Token USDC = new(1, "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48", 6, "USDC", "USD Coin");
    private static readonly Token DAI = new(1, "0x6B175474E89094C44Da98b954EedeAC495271d0F", 18, "DAI", "DAI Stablecoin");

    private static List<Tick> Ticklist() => new()
    {
        new Tick(NearestUsableTick.Find(TickMath.MIN_TICK, TEN), ONE_ETHER, ONE_ETHER),
        new Tick(NearestUsableTick.Find(TickMath.MAX_TICK, TEN), -ONE_ETHER, ONE_ETHER),
    };

    private static readonly Pool USDC_WETH =
        new(USDC, WETH, MEDIUM, TEN, ADDRESS_ZERO, EncodeSqrtRatioX96.Encode(1, 1), 0, 0, Ticklist());

    private static readonly Pool DAI_USDC =
        new(USDC, DAI, MEDIUM, TEN, ADDRESS_ZERO, EncodeSqrtRatioX96.Encode(1, 1), 0, 0, Ticklist());

    private static readonly Pool DAI_WETH =
        new(WETH, DAI, MEDIUM, TEN, ADDRESS_ZERO, EncodeSqrtRatioX96.Encode(1, 1), 0, 0, Ticklist());

    // ---- helpers for building/decoding ABI-ordered values ----

    private static object?[] PoolKeyTuple(PoolKey pk) =>
        new object?[] { pk.Currency0, pk.Currency1, pk.Fee, pk.TickSpacing, pk.Hooks };

    private static object?[] PathTuples(IEnumerable<PathKey> path) =>
        path.Select(pk => (object?)new object?[]
        {
            pk.IntermediateCurrency,
            (BigInteger)pk.Fee,
            pk.TickSpacing,
            pk.Hooks,
            pk.HookData,
        }).ToArray();

    private static object?[] MinHop(IEnumerable<BigInteger> values) =>
        values.Select(v => (object?)v).ToArray();

    private static List<object?> DecodeSwap(Actions action, IReadOnlyDictionary<Actions, IReadOnlyList<ParamType>> abi, string encoded)
    {
        var decoded = AbiParamDecoder.Decode(abi[action].Select(v => v.Type).ToArray(), encoded);
        return (List<object?>)decoded[0]!;
    }

    private static string Addr(object? decoded) => (string)decoded!;

    // ================= addAction =================

    [Fact]
    public void EncodesAV4ExactInSingleSwap()
    {
        var planner = new V4Planner();
        planner.AddAction(Actions.SWAP_EXACT_IN_SINGLE, new object?[]
        {
            new object?[]
            {
                PoolKeyTuple(USDC_WETH.PoolKey),
                true,
                ONE_ETHER,
                ONE_ETHER / 2,
                "0x",
            },
        });

        Assert.Equal("0x06", planner.Actions);
        Assert.Equal(
            "0x0000000000000000000000000000000000000000000000000000000000000020000000000000000000000000a0b86991c6218b36c1d19d4a2e9eb0ce3606eb48000000000000000000000000c02aaa39b223fe8d0a0e5c4f27ead9083c756cc20000000000000000000000000000000000000000000000000000000000000bb8000000000000000000000000000000000000000000000000000000000000000a000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000010000000000000000000000000000000000000000000000000de0b6b3a764000000000000000000000000000000000000000000000000000006f05b59d3b2000000000000000000000000000000000000000000000000000000000000000001200000000000000000000000000000000000000000000000000000000000000000",
            planner.Params[0]);
    }

    [Fact]
    public void EncodesAV4ExactInSwapWithV21IncludesMinHopPriceX36()
    {
        var planner = new V4Planner();
        var route = new Route<Token, Token>(new List<Pool> { DAI_USDC, USDC_WETH }, DAI, WETH);
        var minHopPriceX36 = new List<BigInteger> { 10000, 20000 };

        planner.AddAction(Actions.SWAP_EXACT_IN, new object?[]
        {
            new object?[]
            {
                DAI.Address,
                PathTuples(EncodeRouteToPath.Encode(route)),
                MinHop(minHopPriceX36),
                ONE_ETHER,
                BigInteger.Zero,
            },
        }, URVersion.V2_1_1);

        Assert.Equal("0x07", planner.Actions);

        var swap = DecodeSwap(Actions.SWAP_EXACT_IN, V4Planner.V4_SWAP_ACTIONS_V2_1_1, planner.Params[0]);
        Assert.Equal(DAI.Address.ToLowerInvariant(), Addr(swap[0]));
        var decodedMinHop = (List<object?>)swap[2]!;
        Assert.Equal(2, decodedMinHop.Count);
        Assert.Equal((BigInteger)10000, (BigInteger)decodedMinHop[0]!);
        Assert.Equal((BigInteger)20000, (BigInteger)decodedMinHop[1]!);
        Assert.Equal(ONE_ETHER, (BigInteger)swap[3]!);
    }

    [Fact]
    public void EncodesAV4ExactOutSwapWithV21IncludesMinHopPriceX36()
    {
        var planner = new V4Planner();
        var route = new Route<Token, Token>(new List<Pool> { DAI_USDC, USDC_WETH }, DAI, WETH);
        var minHopPriceX36 = new List<BigInteger> { 15000, 25000 };

        planner.AddAction(Actions.SWAP_EXACT_OUT, new object?[]
        {
            new object?[]
            {
                WETH.Address,
                PathTuples(EncodeRouteToPath.Encode(route, true)),
                MinHop(minHopPriceX36),
                ONE_ETHER,
                ONE_ETHER * 2,
            },
        }, URVersion.V2_1_1);

        Assert.Equal("0x09", planner.Actions);

        var swap = DecodeSwap(Actions.SWAP_EXACT_OUT, V4Planner.V4_SWAP_ACTIONS_V2_1_1, planner.Params[0]);
        Assert.Equal(WETH.Address.ToLowerInvariant(), Addr(swap[0]));
        var decodedMinHop = (List<object?>)swap[2]!;
        Assert.Equal(2, decodedMinHop.Count);
        Assert.Equal((BigInteger)15000, (BigInteger)decodedMinHop[0]!);
        Assert.Equal((BigInteger)25000, (BigInteger)decodedMinHop[1]!);
        Assert.Equal(ONE_ETHER, (BigInteger)swap[3]!);
    }

    // ================= addTrade =================

    [Fact]
    public async Task CompletesAV4ExactIn2HopSwapWithSameResultsAsSameAddAction()
    {
        var route = new Route<Token, Token>(new List<Pool> { DAI_USDC, USDC_WETH }, DAI, WETH);

        // encode with addAction (uses V2.0 ABI without minHopPriceX36)
        var planner = new V4Planner();
        planner.AddAction(Actions.SWAP_EXACT_IN, new object?[]
        {
            new object?[]
            {
                DAI.Address,
                PathTuples(EncodeRouteToPath.Encode(route)),
                ONE_ETHER,
                BigInteger.Zero,
            },
        });

        // encode with addTrade using default V2.0 to match addAction
        var tradePlanner = new V4Planner();
        var trade = await Trade<Token, Token>.FromRoute(
            route, CurrencyAmount<Token>.FromRawAmount(DAI, ONE_ETHER), TradeType.EXACT_INPUT);
        tradePlanner.AddTrade(trade);

        Assert.Equal("0x07", planner.Actions);
        Assert.Equal(planner.Actions, tradePlanner.Actions);
        Assert.Equal(planner.Params[0], tradePlanner.Params[0]);
    }

    [Fact]
    public async Task CompletesAV4ExactIn2HopSwapWithV20NoMinHopPriceX36()
    {
        var route = new Route<Token, Token>(new List<Pool> { DAI_USDC, USDC_WETH }, DAI, WETH);
        var trade = await Trade<Token, Token>.FromRoute(
            route, CurrencyAmount<Token>.FromRawAmount(DAI, ONE_ETHER), TradeType.EXACT_INPUT);

        var planner = new V4Planner();
        planner.AddTrade(trade); // default V2.0

        Assert.Equal("0x07", planner.Actions);

        var swap = DecodeSwap(Actions.SWAP_EXACT_IN, V4Planner.V4_BASE_ACTIONS_ABI_DEFINITION, planner.Params[0]);
        Assert.Equal(DAI.Address.ToLowerInvariant(), Addr(swap[0]));
        // V2.0 struct does not have a minHopPriceX36 field (currencyIn, path, amountIn, amountOutMinimum).
        Assert.Equal(4, swap.Count);
        Assert.Equal(ONE_ETHER, (BigInteger)swap[2]!);
    }

    [Fact]
    public async Task CompletesAV4ExactOut2HopSwap()
    {
        var route = new Route<Token, Token>(new List<Pool> { DAI_USDC, USDC_WETH }, DAI, WETH);
        var slippageTolerance = new Percent(5);
        var trade = await Trade<Token, Token>.FromRoute(
            route, CurrencyAmount<Token>.FromRawAmount(WETH, ONE_ETHER), TradeType.EXACT_OUTPUT);

        var planner = new V4Planner();
        planner.AddTrade(trade, slippageTolerance);

        Assert.Equal("0x09", planner.Actions);

        var swap = DecodeSwap(Actions.SWAP_EXACT_OUT, V4Planner.V4_BASE_ACTIONS_ABI_DEFINITION, planner.Params[0]);
        Assert.Equal(WETH.Address.ToLowerInvariant(), Addr(swap[0]));
        Assert.Equal(2, ((List<object?>)swap[1]!).Count);
        Assert.Equal(ONE_ETHER, (BigInteger)swap[2]!);
    }

    [Fact]
    public async Task CompletesAV4ExactOut2HopSwapWherePathOutputDiffersFromOutput()
    {
        var route = new Route<Token, Ether>(new List<Pool> { DAI_USDC, USDC_WETH }, DAI, ETHER);
        var slippageTolerance = new Percent(5);
        var trade = await Trade<Token, Ether>.FromRoute(
            route, CurrencyAmount<Ether>.FromRawAmount(ETHER, ONE_ETHER), TradeType.EXACT_OUTPUT);

        var planner = new V4Planner();
        planner.AddTrade(trade, slippageTolerance);

        Assert.Equal("0x09", planner.Actions);

        var swap = DecodeSwap(Actions.SWAP_EXACT_OUT, V4Planner.V4_BASE_ACTIONS_ABI_DEFINITION, planner.Params[0]);
        // route.pathOutput is WETH, different from route.output which is ETHER
        Assert.Equal(WETH.Address.ToLowerInvariant(), Addr(swap[0]));
        Assert.Equal(2, ((List<object?>)swap[1]!).Count);
        Assert.Equal(ONE_ETHER, (BigInteger)swap[2]!);
    }

    [Fact]
    public async Task CompletesAV4ExactIn2HopSwapWherePathInputDiffersFromInput()
    {
        var route = new Route<Ether, Token>(new List<Pool> { USDC_WETH, DAI_USDC }, ETHER, DAI);
        var slippageTolerance = new Percent(5);
        var trade = await Trade<Ether, Token>.FromRoute(
            route, CurrencyAmount<Ether>.FromRawAmount(ETHER, ONE_ETHER), TradeType.EXACT_INPUT);

        var planner = new V4Planner();
        planner.AddTrade(trade, slippageTolerance);

        Assert.Equal("0x07", planner.Actions);

        var swap = DecodeSwap(Actions.SWAP_EXACT_IN, V4Planner.V4_BASE_ACTIONS_ABI_DEFINITION, planner.Params[0]);
        // route.pathInput is WETH, different from route.input which is ETHER
        Assert.Equal(WETH.Address.ToLowerInvariant(), Addr(swap[0]));
        Assert.Equal(2, ((List<object?>)swap[1]!).Count);
        Assert.Equal(ONE_ETHER, (BigInteger)swap[2]!);
    }

    [Fact]
    public async Task ThrowsWhenAddingExactOutTradeWithoutSlippageTolerance()
    {
        var route = new Route<Token, Token>(new List<Pool> { DAI_USDC, USDC_WETH }, DAI, WETH);
        var trade = await Trade<Token, Token>.FromRoute(
            route, CurrencyAmount<Token>.FromRawAmount(WETH, ONE_ETHER), TradeType.EXACT_OUTPUT);

        var planner = new V4Planner();
        var ex = Assert.Throws<InvalidOperationException>(() => planner.AddTrade(trade));
        Assert.Equal("ExactOut requires slippageTolerance", ex.Message);
    }

    [Fact]
    public async Task ThrowsWhenTradeHasMoreThanOneSwap()
    {
        var slippageTolerance = new Percent(5);
        var amount = CurrencyAmount<Token>.FromRawAmount(WETH, ONE_ETHER);
        var route1 = new Route<Token, Token>(new List<Pool> { DAI_USDC, USDC_WETH }, DAI, WETH);
        var route2 = new Route<Token, Token>(new List<Pool> { DAI_WETH }, DAI, WETH);
        var trade = await Trade<Token, Token>.FromRoutes(
            new List<(CurrencyAmount<Token> amount, Route<Token, Token> route)>
            {
                (amount, route1),
                (amount, route2),
            },
            TradeType.EXACT_OUTPUT);

        var planner = new V4Planner();
        var ex = Assert.Throws<InvalidOperationException>(() => planner.AddTrade(trade, slippageTolerance));
        Assert.Equal("Only accepts Trades with 1 swap (must break swaps into individual trades)", ex.Message);
    }

    [Fact]
    public async Task CompletesAV4ExactIn2HopSwapWithPerHopSlippageLimitsV21()
    {
        var route = new Route<Token, Token>(new List<Pool> { DAI_USDC, USDC_WETH }, DAI, WETH);
        var trade = await Trade<Token, Token>.FromRoute(
            route, CurrencyAmount<Token>.FromRawAmount(DAI, ONE_ETHER), TradeType.EXACT_INPUT);

        var minHopPriceX36 = new List<BigInteger> { 10000, 20000 };

        var planner = new V4Planner();
        planner.AddTrade(trade, null, minHopPriceX36, URVersion.V2_1_1);

        Assert.Equal("0x07", planner.Actions);

        var swap = DecodeSwap(Actions.SWAP_EXACT_IN, V4Planner.V4_SWAP_ACTIONS_V2_1_1, planner.Params[0]);
        Assert.Equal(DAI.Address.ToLowerInvariant(), Addr(swap[0]));
        var decodedMinHop = (List<object?>)swap[2]!;
        Assert.Equal(2, decodedMinHop.Count);
        Assert.Equal((BigInteger)10000, (BigInteger)decodedMinHop[0]!);
        Assert.Equal((BigInteger)20000, (BigInteger)decodedMinHop[1]!);
    }

    [Fact]
    public async Task CompletesAV4ExactOut2HopSwapWithPerHopSlippageLimitsV21()
    {
        var route = new Route<Token, Token>(new List<Pool> { DAI_USDC, USDC_WETH }, DAI, WETH);
        var slippageTolerance = new Percent(5);
        var trade = await Trade<Token, Token>.FromRoute(
            route, CurrencyAmount<Token>.FromRawAmount(WETH, ONE_ETHER), TradeType.EXACT_OUTPUT);

        var minHopPriceX36 = new List<BigInteger> { 10000, 20000 };

        var planner = new V4Planner();
        planner.AddTrade(trade, slippageTolerance, minHopPriceX36, URVersion.V2_1_1);

        Assert.Equal("0x09", planner.Actions);

        var swap = DecodeSwap(Actions.SWAP_EXACT_OUT, V4Planner.V4_SWAP_ACTIONS_V2_1_1, planner.Params[0]);
        Assert.Equal(WETH.Address.ToLowerInvariant(), Addr(swap[0]));
        var decodedMinHop = (List<object?>)swap[2]!;
        Assert.Equal(2, decodedMinHop.Count);
        Assert.Equal((BigInteger)10000, (BigInteger)decodedMinHop[0]!);
        Assert.Equal((BigInteger)20000, (BigInteger)decodedMinHop[1]!);
    }

    [Fact]
    public async Task CompletesAV4ExactInSwapWithEmptyMinHopPriceX36WhenNotProvidedV21()
    {
        var route = new Route<Token, Token>(new List<Pool> { DAI_USDC, USDC_WETH }, DAI, WETH);
        var trade = await Trade<Token, Token>.FromRoute(
            route, CurrencyAmount<Token>.FromRawAmount(DAI, ONE_ETHER), TradeType.EXACT_INPUT);

        var planner = new V4Planner();
        planner.AddTrade(trade, null, null, URVersion.V2_1_1);

        Assert.Equal("0x07", planner.Actions);

        var swap = DecodeSwap(Actions.SWAP_EXACT_IN, V4Planner.V4_SWAP_ACTIONS_V2_1_1, planner.Params[0]);
        Assert.Equal(DAI.Address.ToLowerInvariant(), Addr(swap[0]));
        Assert.Empty((List<object?>)swap[2]!);
    }

    // ================= addSettle =================

    [Fact]
    public void CompletesASettleWithoutASpecifiedAmount()
    {
        var planner = new V4Planner();
        planner.AddSettle(DAI, true);

        Assert.Equal("0x0b", planner.Actions);
        Assert.Equal(
            "0x0000000000000000000000006b175474e89094c44da98b954eedeac495271d0f00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001",
            planner.Params[0]);
    }

    [Fact]
    public void CompletesASettleWithASpecifiedAmount()
    {
        var planner = new V4Planner();
        planner.AddSettle(DAI, true, BigInteger.Parse("8"));

        Assert.Equal("0x0b", planner.Actions);
        Assert.Equal(
            "0x0000000000000000000000006b175474e89094c44da98b954eedeac495271d0f00000000000000000000000000000000000000000000000000000000000000080000000000000000000000000000000000000000000000000000000000000001",
            planner.Params[0]);
    }

    [Fact]
    public void CompletesASettleWithPayerIsUserAsFalse()
    {
        var planner = new V4Planner();
        planner.AddSettle(DAI, false, BigInteger.Parse("8"));

        Assert.Equal("0x0b", planner.Actions);
        Assert.Equal(
            "0x0000000000000000000000006b175474e89094c44da98b954eedeac495271d0f00000000000000000000000000000000000000000000000000000000000000080000000000000000000000000000000000000000000000000000000000000000",
            planner.Params[0]);
    }

    // ================= addTake =================

    [Fact]
    public void CompletesATakeWithoutASpecifiedAmount()
    {
        var recipient = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var planner = new V4Planner();
        planner.AddTake(DAI, recipient);

        Assert.Equal("0x0e", planner.Actions);
        Assert.Equal(
            "0x0000000000000000000000006b175474e89094c44da98b954eedeac495271d0f000000000000000000000000aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa0000000000000000000000000000000000000000000000000000000000000000",
            planner.Params[0]);
    }

    [Fact]
    public void CompletesATakeWithASpecifiedAmount()
    {
        var recipient = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var planner = new V4Planner();
        planner.AddTake(DAI, recipient, BigInteger.Parse("8"));

        Assert.Equal("0x0e", planner.Actions);
        Assert.Equal(
            "0x0000000000000000000000006b175474e89094c44da98b954eedeac495271d0f000000000000000000000000aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa0000000000000000000000000000000000000000000000000000000000000008",
            planner.Params[0]);
    }
}
