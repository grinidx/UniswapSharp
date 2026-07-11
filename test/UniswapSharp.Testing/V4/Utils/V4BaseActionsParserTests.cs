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

// Ported 1:1 from sdks/v4-sdk/src/utils/v4BaseActionsParser.test.ts.
// Route/Trade are the open-generic V4 types (UniswapSharp.V4.Entities), which cannot be aliased,
// so the V3 Entities namespace is deliberately not imported; Pool and Tick are aliased instead.
//
// The C# ABI decoder returns addresses lower-cased (upstream ethers returns them checksummed), so
// expected addresses are lower-cased here — the same convention already used by V4PlannerTests.
public class V4BaseActionsParserTests
{
    private const int MEDIUM = Constants.FEE_AMOUNT_MEDIUM;
    private const int TEN = Constants.TICK_SPACING_TEN;
    private const string ADDRESS_ZERO = Constants.ADDRESS_ZERO;

    private static readonly BigInteger ONE_ETHER = BigInteger.Pow(10, 18);

    // ethers.utils.parseEther('1')
    private static readonly BigInteger Amount = ONE_ETHER;

    private const string AddressOne = "0x0000000000000000000000000000000000000001";
    private const string AddressTwo = "0x0000000000000000000000000000000000000002";

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

    // ---- helpers for building ABI-ordered input values ----

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

    // ---- helpers for expected (decoded) values ----

    private static PoolKey LowerPoolKey(PoolKey pk) =>
        new(pk.Currency0.ToLowerInvariant(), pk.Currency1.ToLowerInvariant(), pk.Fee, pk.TickSpacing, pk.Hooks.ToLowerInvariant());

    // The two-hop DAI -> USDC -> WETH path, decoded (addresses lower-cased, hookData empty).
    private static List<PathKey> ExpectedDaiUsdcWethPath() => new()
    {
        new(USDC.Address.ToLowerInvariant(), MEDIUM, TEN, ADDRESS_ZERO, "0x"),
        new(WETH.Address.ToLowerInvariant(), MEDIUM, TEN, ADDRESS_ZERO, "0x"),
    };

    // ---- deep-equality assertions mirroring chai's `deep.equal` ----

    private static void AssertCall(V4RouterCall expected, V4RouterCall actual)
    {
        Assert.Equal(expected.Actions.Count, actual.Actions.Count);
        for (int i = 0; i < expected.Actions.Count; i++)
        {
            V4RouterAction e = expected.Actions[i];
            V4RouterAction a = actual.Actions[i];
            Assert.Equal(e.ActionName, a.ActionName);
            Assert.Equal(e.ActionType, a.ActionType);
            Assert.Equal(e.Params.Count, a.Params.Count);
            for (int j = 0; j < e.Params.Count; j++)
            {
                Assert.Equal(e.Params[j].Name, a.Params[j].Name);
                AssertValue(e.Params[j].Value, a.Params[j].Value);
            }
        }
    }

    private static void AssertValue(object? expected, object? actual)
    {
        switch (expected)
        {
            case SwapExactIn e:
                {
                    SwapExactIn a = Assert.IsType<SwapExactIn>(actual);
                    Assert.Equal(e.CurrencyIn, a.CurrencyIn);
                    Assert.Equal(e.Path, a.Path);
                    AssertNullableMinHop(e.MinHopPriceX36, a.MinHopPriceX36);
                    Assert.Equal(e.AmountIn, a.AmountIn);
                    Assert.Equal(e.AmountOutMinimum, a.AmountOutMinimum);
                    break;
                }
            case SwapExactOut e:
                {
                    SwapExactOut a = Assert.IsType<SwapExactOut>(actual);
                    Assert.Equal(e.CurrencyOut, a.CurrencyOut);
                    Assert.Equal(e.Path, a.Path);
                    AssertNullableMinHop(e.MinHopPriceX36, a.MinHopPriceX36);
                    Assert.Equal(e.AmountOut, a.AmountOut);
                    Assert.Equal(e.AmountInMaximum, a.AmountInMaximum);
                    break;
                }
            default:
                // Scalars (BigInteger, bool, address string) and list-free records
                // (SwapExactInSingle / SwapExactOutSingle) rely on value/record equality.
                Assert.Equal(expected, actual);
                break;
        }
    }

    private static void AssertNullableMinHop(IReadOnlyList<BigInteger>? expected, IReadOnlyList<BigInteger>? actual)
    {
        if (expected is null)
        {
            Assert.Null(actual);
            return;
        }
        Assert.NotNull(actual);
        Assert.Equal(expected, actual);
    }

    private static void RunCase(V4Planner input, V4RouterCall expected)
    {
        string calldata = input.Finalize();
        V4RouterCall result = V4BaseActionsParser.ParseCalldata(calldata);
        AssertCall(expected, result);
    }

    // ================= Command Parser (deep-equal) =================

    [Fact]
    public void ParsesSweep()
    {
        var input = new V4Planner().AddAction(Actions.SWEEP, new object?[] { AddressOne, AddressTwo });
        RunCase(input, new V4RouterCall(new[]
        {
            new V4RouterAction("SWEEP", Actions.SWEEP, new[]
            {
                new Param("currency", AddressOne),
                new Param("recipient", AddressTwo),
            }),
        }));
    }

    [Fact]
    public void ParsesCloseCurrency()
    {
        var input = new V4Planner().AddAction(Actions.CLOSE_CURRENCY, new object?[] { AddressOne });
        RunCase(input, new V4RouterCall(new[]
        {
            new V4RouterAction("CLOSE_CURRENCY", Actions.CLOSE_CURRENCY, new[]
            {
                new Param("currency", AddressOne),
            }),
        }));
    }

    [Fact]
    public void ParsesTakePair()
    {
        var input = new V4Planner().AddAction(Actions.TAKE_PAIR, new object?[] { AddressOne, AddressTwo, AddressOne });
        RunCase(input, new V4RouterCall(new[]
        {
            new V4RouterAction("TAKE_PAIR", Actions.TAKE_PAIR, new[]
            {
                new Param("currency0", AddressOne),
                new Param("currency1", AddressTwo),
                new Param("recipient", AddressOne),
            }),
        }));
    }

    [Fact]
    public void ParsesTakePortion()
    {
        var input = new V4Planner().AddAction(Actions.TAKE_PORTION, new object?[] { AddressOne, AddressTwo, Amount });
        RunCase(input, new V4RouterCall(new[]
        {
            new V4RouterAction("TAKE_PORTION", Actions.TAKE_PORTION, new[]
            {
                new Param("currency", AddressOne),
                new Param("recipient", AddressTwo),
                new Param("bips", Amount),
            }),
        }));
    }

    [Fact]
    public void ParsesTakeAll()
    {
        var input = new V4Planner().AddAction(Actions.TAKE_ALL, new object?[] { AddressOne, Amount });
        RunCase(input, new V4RouterCall(new[]
        {
            new V4RouterAction("TAKE_ALL", Actions.TAKE_ALL, new[]
            {
                new Param("currency", AddressOne),
                new Param("minAmount", Amount),
            }),
        }));
    }

    [Fact]
    public void ParsesTake()
    {
        var input = new V4Planner().AddAction(Actions.TAKE, new object?[] { AddressOne, AddressTwo, Amount });
        RunCase(input, new V4RouterCall(new[]
        {
            new V4RouterAction("TAKE", Actions.TAKE, new[]
            {
                new Param("currency", AddressOne),
                new Param("recipient", AddressTwo),
                new Param("amount", Amount),
            }),
        }));
    }

    [Fact]
    public void ParsesSettlePair()
    {
        var input = new V4Planner().AddAction(Actions.SETTLE_PAIR, new object?[] { AddressOne, AddressTwo });
        RunCase(input, new V4RouterCall(new[]
        {
            new V4RouterAction("SETTLE_PAIR", Actions.SETTLE_PAIR, new[]
            {
                new Param("currency0", AddressOne),
                new Param("currency1", AddressTwo),
            }),
        }));
    }

    [Fact]
    public void ParsesSettle()
    {
        var input = new V4Planner().AddAction(Actions.SETTLE, new object?[] { AddressOne, Amount, true });
        RunCase(input, new V4RouterCall(new[]
        {
            new V4RouterAction("SETTLE", Actions.SETTLE, new[]
            {
                new Param("currency", AddressOne),
                new Param("amount", Amount),
                new Param("payerIsUser", true),
            }),
        }));
    }

    [Fact]
    public void ParsesSwapExactInSingle()
    {
        var input = new V4Planner().AddAction(Actions.SWAP_EXACT_IN_SINGLE, new object?[]
        {
            new object?[]
            {
                PoolKeyTuple(USDC_WETH.PoolKey),
                true,
                Amount,
                Amount,
                "0x",
            },
        });
        RunCase(input, new V4RouterCall(new[]
        {
            new V4RouterAction("SWAP_EXACT_IN_SINGLE", Actions.SWAP_EXACT_IN_SINGLE, new[]
            {
                new Param("swap", new SwapExactInSingle(
                    LowerPoolKey(USDC_WETH.PoolKey), true, Amount, Amount, null, "0x")),
            }),
        }));
    }

    [Fact]
    public void ParsesSwapExactOutSingle()
    {
        var input = new V4Planner().AddAction(Actions.SWAP_EXACT_OUT_SINGLE, new object?[]
        {
            new object?[]
            {
                PoolKeyTuple(USDC_WETH.PoolKey),
                true,
                Amount,
                Amount,
                "0x",
            },
        });
        RunCase(input, new V4RouterCall(new[]
        {
            new V4RouterAction("SWAP_EXACT_OUT_SINGLE", Actions.SWAP_EXACT_OUT_SINGLE, new[]
            {
                new Param("swap", new SwapExactOutSingle(
                    LowerPoolKey(USDC_WETH.PoolKey), true, Amount, Amount, null, "0x")),
            }),
        }));
    }

    // V2.0: SWAP_EXACT_IN without minHopPriceX36
    [Fact]
    public void ParsesSwapExactInV20()
    {
        var route = new Route<Token, Token>(new List<Pool> { DAI_USDC, USDC_WETH }, DAI, WETH);
        var input = new V4Planner().AddAction(Actions.SWAP_EXACT_IN, new object?[]
        {
            new object?[]
            {
                DAI.Address,
                PathTuples(EncodeRouteToPath.Encode(route)),
                Amount,
                Amount,
            },
        });
        RunCase(input, new V4RouterCall(new[]
        {
            new V4RouterAction("SWAP_EXACT_IN", Actions.SWAP_EXACT_IN, new[]
            {
                new Param("swap", new SwapExactIn(
                    DAI.Address.ToLowerInvariant(), ExpectedDaiUsdcWethPath(), null, Amount, Amount)),
            }),
        }));
    }

    // V2.0: SWAP_EXACT_OUT without minHopPriceX36
    [Fact]
    public void ParsesSwapExactOutV20()
    {
        var route = new Route<Token, Token>(new List<Pool> { DAI_USDC, USDC_WETH }, DAI, WETH);
        var input = new V4Planner().AddAction(Actions.SWAP_EXACT_OUT, new object?[]
        {
            new object?[]
            {
                DAI.Address,
                PathTuples(EncodeRouteToPath.Encode(route)),
                Amount,
                Amount,
            },
        });
        RunCase(input, new V4RouterCall(new[]
        {
            new V4RouterAction("SWAP_EXACT_OUT", Actions.SWAP_EXACT_OUT, new[]
            {
                new Param("swap", new SwapExactOut(
                    DAI.Address.ToLowerInvariant(), ExpectedDaiUsdcWethPath(), null, Amount, Amount)),
            }),
        }));
    }

    // ================= Version-aware Parser =================

    // ---- V2.0 parsing (default) ----

    [Fact]
    public async Task ParsesSwapExactInWithoutMinHopPriceX36V20()
    {
        var route = new Route<Token, Token>(new List<Pool> { DAI_USDC, USDC_WETH }, DAI, WETH);
        var trade = await Trade<Token, Token>.FromRoute(
            route, CurrencyAmount<Token>.FromRawAmount(DAI, ONE_ETHER), TradeType.EXACT_INPUT);

        var planner = new V4Planner();
        planner.AddTrade(trade); // Default is V2.0

        var result = V4BaseActionsParser.ParseCalldata(planner.Finalize()); // Default is V2.0

        Assert.Single(result.Actions);
        Assert.Equal("SWAP_EXACT_IN", result.Actions[0].ActionName);

        var swap = Assert.IsType<SwapExactIn>(result.Actions[0].Params[0].Value);
        Assert.Equal(DAI.Address.ToLowerInvariant(), swap.CurrencyIn);
        Assert.Equal(2, swap.Path.Count);
        Assert.Null(swap.MinHopPriceX36); // V2.0 does not have minHopPriceX36
    }

    [Fact]
    public async Task ParsesSwapExactOutWithoutMinHopPriceX36V20()
    {
        var route = new Route<Token, Token>(new List<Pool> { DAI_USDC, USDC_WETH }, DAI, WETH);
        var trade = await Trade<Token, Token>.FromRoute(
            route, CurrencyAmount<Token>.FromRawAmount(WETH, ONE_ETHER), TradeType.EXACT_OUTPUT);

        var planner = new V4Planner();
        planner.AddTrade(trade, new Percent(5)); // Default is V2.0

        var result = V4BaseActionsParser.ParseCalldata(planner.Finalize()); // Default is V2.0

        Assert.Single(result.Actions);
        Assert.Equal("SWAP_EXACT_OUT", result.Actions[0].ActionName);

        var swap = Assert.IsType<SwapExactOut>(result.Actions[0].Params[0].Value);
        Assert.Equal(WETH.Address.ToLowerInvariant(), swap.CurrencyOut);
        Assert.Equal(2, swap.Path.Count);
        Assert.Null(swap.MinHopPriceX36); // V2.0 does not have minHopPriceX36
    }

    // ---- V2.1 parsing ----

    [Fact]
    public async Task ParsesSwapExactInWithMinHopPriceX36V21()
    {
        var route = new Route<Token, Token>(new List<Pool> { DAI_USDC, USDC_WETH }, DAI, WETH);
        var trade = await Trade<Token, Token>.FromRoute(
            route, CurrencyAmount<Token>.FromRawAmount(DAI, ONE_ETHER), TradeType.EXACT_INPUT);

        var minHopPriceX36 = new List<BigInteger> { 10000, 20000 };

        var planner = new V4Planner();
        planner.AddTrade(trade, null, minHopPriceX36, URVersion.V2_1_1);

        var result = V4BaseActionsParser.ParseCalldata(planner.Finalize(), URVersion.V2_1_1);

        Assert.Single(result.Actions);
        Assert.Equal("SWAP_EXACT_IN", result.Actions[0].ActionName);

        var swap = Assert.IsType<SwapExactIn>(result.Actions[0].Params[0].Value);
        Assert.Equal(DAI.Address.ToLowerInvariant(), swap.CurrencyIn);
        Assert.Equal(2, swap.Path.Count);
        Assert.NotNull(swap.MinHopPriceX36);
        Assert.Equal(2, swap.MinHopPriceX36!.Count);
        Assert.Equal((BigInteger)10000, swap.MinHopPriceX36[0]);
        Assert.Equal((BigInteger)20000, swap.MinHopPriceX36[1]);
    }

    [Fact]
    public async Task ParsesSwapExactOutWithMinHopPriceX36V21()
    {
        var route = new Route<Token, Token>(new List<Pool> { DAI_USDC, USDC_WETH }, DAI, WETH);
        var trade = await Trade<Token, Token>.FromRoute(
            route, CurrencyAmount<Token>.FromRawAmount(WETH, ONE_ETHER), TradeType.EXACT_OUTPUT);

        var minHopPriceX36 = new List<BigInteger> { 15000, 25000 };

        var planner = new V4Planner();
        planner.AddTrade(trade, new Percent(5), minHopPriceX36, URVersion.V2_1_1);

        var result = V4BaseActionsParser.ParseCalldata(planner.Finalize(), URVersion.V2_1_1);

        Assert.Single(result.Actions);
        Assert.Equal("SWAP_EXACT_OUT", result.Actions[0].ActionName);

        var swap = Assert.IsType<SwapExactOut>(result.Actions[0].Params[0].Value);
        Assert.Equal(WETH.Address.ToLowerInvariant(), swap.CurrencyOut);
        Assert.Equal(2, swap.Path.Count);
        Assert.NotNull(swap.MinHopPriceX36);
        Assert.Equal(2, swap.MinHopPriceX36!.Count);
        Assert.Equal((BigInteger)15000, swap.MinHopPriceX36[0]);
        Assert.Equal((BigInteger)25000, swap.MinHopPriceX36[1]);
    }

    [Fact]
    public async Task ParsesSwapExactInWithEmptyMinHopPriceX36ArrayV21()
    {
        var route = new Route<Token, Token>(new List<Pool> { DAI_USDC, USDC_WETH }, DAI, WETH);
        var trade = await Trade<Token, Token>.FromRoute(
            route, CurrencyAmount<Token>.FromRawAmount(DAI, ONE_ETHER), TradeType.EXACT_INPUT);

        var planner = new V4Planner();
        planner.AddTrade(trade, null, null, URVersion.V2_1_1); // No minHopPriceX36 provided, defaults to []

        var result = V4BaseActionsParser.ParseCalldata(planner.Finalize(), URVersion.V2_1_1);

        Assert.Single(result.Actions);
        Assert.Equal("SWAP_EXACT_IN", result.Actions[0].ActionName);

        var swap = Assert.IsType<SwapExactIn>(result.Actions[0].Params[0].Value);
        Assert.NotNull(swap.MinHopPriceX36);
        Assert.Empty(swap.MinHopPriceX36!); // Empty array
    }

    // ---- Round-trip encoding/parsing ----

    [Fact]
    public async Task RoundTripsV20SwapExactIn()
    {
        var route = new Route<Token, Token>(new List<Pool> { DAI_USDC, USDC_WETH }, DAI, WETH);
        var trade = await Trade<Token, Token>.FromRoute(
            route, CurrencyAmount<Token>.FromRawAmount(DAI, ONE_ETHER), TradeType.EXACT_INPUT);

        var planner = new V4Planner();
        planner.AddTrade(trade);

        var result = V4BaseActionsParser.ParseCalldata(planner.Finalize());

        var swap = Assert.IsType<SwapExactIn>(result.Actions[0].Params[0].Value);
        Assert.Equal(DAI.Address.ToLowerInvariant(), swap.CurrencyIn);
        Assert.Equal(ONE_ETHER, swap.AmountIn);
    }

    [Fact]
    public async Task RoundTripsV21SwapExactInWithMinHopPriceX36()
    {
        var route = new Route<Token, Token>(new List<Pool> { DAI_USDC, USDC_WETH }, DAI, WETH);
        var trade = await Trade<Token, Token>.FromRoute(
            route, CurrencyAmount<Token>.FromRawAmount(DAI, ONE_ETHER), TradeType.EXACT_INPUT);

        var minHopPriceX36 = new List<BigInteger> { 12345, 67890 };

        var planner = new V4Planner();
        planner.AddTrade(trade, null, minHopPriceX36, URVersion.V2_1_1);

        var result = V4BaseActionsParser.ParseCalldata(planner.Finalize(), URVersion.V2_1_1);

        var swap = Assert.IsType<SwapExactIn>(result.Actions[0].Params[0].Value);
        Assert.Equal(DAI.Address.ToLowerInvariant(), swap.CurrencyIn);
        Assert.Equal(ONE_ETHER, swap.AmountIn);
        Assert.NotNull(swap.MinHopPriceX36);
        Assert.Equal((BigInteger)12345, swap.MinHopPriceX36![0]);
        Assert.Equal((BigInteger)67890, swap.MinHopPriceX36[1]);
    }
}
