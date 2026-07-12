using System.Numerics;
using System.Text;
using UniswapSharp.Core.Entities;
using UniswapSharp.UniversalRouter;
using UniswapSharp.UniversalRouter.Utils;
using UniswapSharp.V3.Utils;
using UniswapSharp.V4.Utils;
using Param = UniswapSharp.UniversalRouter.Utils.Param;
using Pool = UniswapSharp.V4.Entities.Pool;
using PoolKey = UniswapSharp.V4.Entities.PoolKey;
using Tick = UniswapSharp.V3.Entities.Tick;

namespace UniswapSharp.Testing.UniversalRouter.Utils;

// Ported from sdks/universal-router-sdk/test/utils/commandParser.test.ts (table cases; round-trips through
// RoutePlanner -> execute calldata -> CommandParser).
public class CommandParserTests
{
    private const string AddressOne = "0x0000000000000000000000000000000000000001";
    private const string AddressTwo = "0x0000000000000000000000000000000000000002";
    private static readonly BigInteger Amount = BigInteger.Pow(10, 18);

    private static readonly Token USDC = new(1, "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48", 6, "USDC", "USD Coin");
    private static readonly Token WETH = Weth9.Tokens[1];

    private static List<Tick> Ticklist() => new()
    {
        new Tick(NearestUsableTick.Find(TickMath.MIN_TICK, 10), Amount, Amount),
        new Tick(NearestUsableTick.Find(TickMath.MAX_TICK, 10), -Amount, Amount),
    };

    private static readonly Pool USDC_WETH =
        new(USDC, WETH, 3000, 10, Constants.ZERO_ADDRESS, EncodeSqrtRatioX96.Encode(1, 1), 0, 0, Ticklist());

    private static object?[] PoolKeyTuple(PoolKey pk) =>
        new object?[] { pk.Currency0, pk.Currency1, (BigInteger)pk.Fee, pk.TickSpacing, pk.Hooks };

    private static string EncodePath(string[] path, int fee)
    {
        var sb = new StringBuilder("0x");
        for (int i = 0; i < path.Length - 1; i++)
        {
            sb.Append(path[i][2..]);
            sb.Append(fee.ToString("x6"));
        }
        sb.Append(path[^1][2..]);
        return sb.ToString().ToLowerInvariant();
    }

    private static string EncodePathExactInput(string[] tokens, int fee) => EncodePath(tokens, fee);
    private static string EncodePathExactOutput(string[] tokens, int fee) => EncodePath(tokens.Reverse().ToArray(), fee);

    private sealed record Case(RoutePlanner Input, UniversalRouterCall Result, UniversalRouterVersion? Version = null);

    private static Case C(RoutePlanner input, UniversalRouterCommand command, UniversalRouterVersion? version = null) =>
        new(input, new UniversalRouterCall(new List<UniversalRouterCommand> { command }), version);

    private static UniversalRouterCommand Cmd(CommandType type, params Param[] parms) =>
        new(type.ToString(), type, parms);

    private static List<Case> Cases()
    {
        var cases = new List<Case>
        {
            C(new RoutePlanner().AddCommand(CommandType.WRAP_ETH, new object?[] { AddressOne, Amount }),
                Cmd(CommandType.WRAP_ETH, new Param("recipient", AddressOne), new Param("amount", Amount))),

            C(new RoutePlanner().AddCommand(CommandType.UNWRAP_WETH, new object?[] { AddressOne, Amount }),
                Cmd(CommandType.UNWRAP_WETH, new Param("recipient", AddressOne), new Param("amountMin", Amount))),

            C(new RoutePlanner().AddCommand(CommandType.SWEEP, new object?[] { AddressOne, AddressTwo, Amount }),
                Cmd(CommandType.SWEEP, new Param("token", AddressOne), new Param("recipient", AddressTwo), new Param("amountMin", Amount))),

            C(new RoutePlanner().AddCommand(CommandType.TRANSFER, new object?[] { AddressOne, AddressTwo, Amount }),
                Cmd(CommandType.TRANSFER, new Param("token", AddressOne), new Param("recipient", AddressTwo), new Param("value", Amount))),

            C(new RoutePlanner().AddCommand(CommandType.PAY_PORTION, new object?[] { AddressOne, AddressTwo, Amount }),
                Cmd(CommandType.PAY_PORTION, new Param("token", AddressOne), new Param("recipient", AddressTwo), new Param("bips", Amount))),

            C(new RoutePlanner().AddCommand(CommandType.PAY_PORTION_FULL_PRECISION, new object?[] { AddressOne, AddressTwo, Amount }),
                Cmd(CommandType.PAY_PORTION_FULL_PRECISION, new Param("token", AddressOne), new Param("recipient", AddressTwo), new Param("portion", Amount))),

            C(new RoutePlanner().AddCommand(CommandType.BALANCE_CHECK_ERC20, new object?[] { AddressOne, AddressTwo, Amount }),
                Cmd(CommandType.BALANCE_CHECK_ERC20, new Param("owner", AddressOne), new Param("token", AddressTwo), new Param("minBalance", Amount))),
        };

        // two-command plan
        cases.Add(new Case(
            new RoutePlanner()
                .AddCommand(CommandType.WRAP_ETH, new object?[] { AddressOne, Amount })
                .AddCommand(CommandType.UNWRAP_WETH, new object?[] { AddressOne, Amount }),
            new UniversalRouterCall(new List<UniversalRouterCommand>
            {
                Cmd(CommandType.WRAP_ETH, new Param("recipient", AddressOne), new Param("amount", Amount)),
                Cmd(CommandType.UNWRAP_WETH, new Param("recipient", AddressOne), new Param("amountMin", Amount)),
            })));

        // V3 exact in
        cases.Add(C(
            new RoutePlanner().AddCommand(CommandType.V3_SWAP_EXACT_IN,
                new object?[] { AddressOne, Amount, Amount, EncodePathExactInput(new[] { AddressOne, AddressTwo }, 123), true }),
            Cmd(CommandType.V3_SWAP_EXACT_IN,
                new Param("recipient", AddressOne),
                new Param("amountIn", Amount),
                new Param("amountOutMin", Amount),
                new Param("path", new List<V3PathItem> { new(AddressOne, AddressTwo, 123) }),
                new Param("payerIsUser", true))));

        // V3 exact out
        cases.Add(C(
            new RoutePlanner().AddCommand(CommandType.V3_SWAP_EXACT_OUT,
                new object?[] { AddressOne, Amount, Amount, EncodePathExactOutput(new[] { AddressOne, AddressTwo }, 123), true }),
            Cmd(CommandType.V3_SWAP_EXACT_OUT,
                new Param("recipient", AddressOne),
                new Param("amountOut", Amount),
                new Param("amountInMax", Amount),
                new Param("path", new List<V3PathItem> { new(AddressOne, AddressTwo, 123) }),
                new Param("payerIsUser", true))));

        // V2 exact in
        cases.Add(C(
            new RoutePlanner().AddCommand(CommandType.V2_SWAP_EXACT_IN,
                new object?[] { AddressOne, Amount, Amount, new object?[] { AddressOne, AddressTwo }, true }),
            Cmd(CommandType.V2_SWAP_EXACT_IN,
                new Param("recipient", AddressOne),
                new Param("amountIn", Amount),
                new Param("amountOutMin", Amount),
                new Param("path", new List<object?> { AddressOne, AddressTwo }),
                new Param("payerIsUser", true))));

        // V2 exact out
        cases.Add(C(
            new RoutePlanner().AddCommand(CommandType.V2_SWAP_EXACT_OUT,
                new object?[] { AddressOne, Amount, Amount, new object?[] { AddressOne, AddressTwo }, true }),
            Cmd(CommandType.V2_SWAP_EXACT_OUT,
                new Param("recipient", AddressOne),
                new Param("amountOut", Amount),
                new Param("amountInMax", Amount),
                new Param("path", new List<object?> { AddressOne, AddressTwo }),
                new Param("payerIsUser", true))));

        // V2.1.1 V3 exact-in with minHopPriceX36
        cases.Add(C(
            new RoutePlanner().AddCommand(CommandType.V3_SWAP_EXACT_IN,
                new object?[] { AddressOne, Amount, Amount, EncodePathExactInput(new[] { AddressOne, AddressTwo }, 123), true, new object?[] { "500", "600" } },
                false, UniversalRouterVersion.V2_1_1),
            Cmd(CommandType.V3_SWAP_EXACT_IN,
                new Param("recipient", AddressOne),
                new Param("amountIn", Amount),
                new Param("amountOutMin", Amount),
                new Param("path", new List<V3PathItem> { new(AddressOne, AddressTwo, 123) }),
                new Param("payerIsUser", true),
                new Param("minHopPriceX36", new List<object?> { new BigInteger(500), new BigInteger(600) })),
            UniversalRouterVersion.V2_1_1));

        // V2.1.1 V3 exact-out
        cases.Add(C(
            new RoutePlanner().AddCommand(CommandType.V3_SWAP_EXACT_OUT,
                new object?[] { AddressOne, Amount, Amount, EncodePathExactOutput(new[] { AddressOne, AddressTwo }, 123), true, new object?[] { "750" } },
                false, UniversalRouterVersion.V2_1_1),
            Cmd(CommandType.V3_SWAP_EXACT_OUT,
                new Param("recipient", AddressOne),
                new Param("amountOut", Amount),
                new Param("amountInMax", Amount),
                new Param("path", new List<V3PathItem> { new(AddressOne, AddressTwo, 123) }),
                new Param("payerIsUser", true),
                new Param("minHopPriceX36", new List<object?> { new BigInteger(750) })),
            UniversalRouterVersion.V2_1_1));

        // V2.1.1 V2 exact-in
        cases.Add(C(
            new RoutePlanner().AddCommand(CommandType.V2_SWAP_EXACT_IN,
                new object?[] { AddressOne, Amount, Amount, new object?[] { AddressOne, AddressTwo }, true, new object?[] { "500", "600" } },
                false, UniversalRouterVersion.V2_1_1),
            Cmd(CommandType.V2_SWAP_EXACT_IN,
                new Param("recipient", AddressOne),
                new Param("amountIn", Amount),
                new Param("amountOutMin", Amount),
                new Param("path", new List<object?> { AddressOne, AddressTwo }),
                new Param("payerIsUser", true),
                new Param("minHopPriceX36", new List<object?> { new BigInteger(500), new BigInteger(600) })),
            UniversalRouterVersion.V2_1_1));

        // V2.1.1 V2 exact-out
        cases.Add(C(
            new RoutePlanner().AddCommand(CommandType.V2_SWAP_EXACT_OUT,
                new object?[] { AddressOne, Amount, Amount, new object?[] { AddressOne, AddressTwo }, true, new object?[] { "250" } },
                false, UniversalRouterVersion.V2_1_1),
            Cmd(CommandType.V2_SWAP_EXACT_OUT,
                new Param("recipient", AddressOne),
                new Param("amountOut", Amount),
                new Param("amountInMax", Amount),
                new Param("path", new List<object?> { AddressOne, AddressTwo }),
                new Param("payerIsUser", true),
                new Param("minHopPriceX36", new List<object?> { new BigInteger(250) })),
            UniversalRouterVersion.V2_1_1));

        // V4_SWAP SWAP_EXACT_IN_SINGLE
        cases.Add(C(
            new RoutePlanner().AddCommand(CommandType.V4_SWAP, new object?[]
            {
                new V4Planner().AddAction(Actions.SWAP_EXACT_IN_SINGLE, new object?[]
                {
                    new object?[] { PoolKeyTuple(USDC_WETH.PoolKey), true, Amount, Amount, "0x" },
                }).Finalize(),
            }),
            Cmd(CommandType.V4_SWAP,
                new Param("SWAP_EXACT_IN_SINGLE", new List<Param>
                {
                    new("swap", new SwapExactInSingle(USDC_WETH.PoolKey, true, Amount, Amount, null, "0x")),
                }))));

        // V4_SWAP TAKE_ALL
        cases.Add(C(
            new RoutePlanner().AddCommand(CommandType.V4_SWAP, new object?[]
            {
                new V4Planner().AddAction(Actions.TAKE_ALL, new object?[] { AddressOne, Amount }).Finalize(),
            }),
            Cmd(CommandType.V4_SWAP,
                new Param("TAKE_ALL", new List<Param>
                {
                    new("currency", AddressOne),
                    new("minAmount", Amount),
                }))));

        // V4_POSITION_MANAGER_CALL MINT_POSITION
        cases.Add(C(
            new RoutePlanner().AddCommand(CommandType.V4_POSITION_MANAGER_CALL, new object?[]
            {
                new V4Planner().AddAction(Actions.MINT_POSITION, new object?[]
                {
                    PoolKeyTuple(USDC_WETH.PoolKey), -60, 60, (BigInteger)5000000, Amount, Amount, AddressOne, "0x",
                }).Finalize(),
            }),
            Cmd(CommandType.V4_POSITION_MANAGER_CALL,
                new Param("MINT_POSITION", new List<Param>
                {
                    new("poolKey", USDC_WETH.PoolKey),
                    new("tickLower", new BigInteger(-60)),
                    new("tickUpper", new BigInteger(60)),
                    new("liquidity", new BigInteger(5000000)),
                    new("amount0Max", Amount),
                    new("amount1Max", Amount),
                    new("owner", AddressOne),
                    new("hookData", "0x"),
                }))));

        return cases;
    }

    [Fact]
    public void ParsesAllTableCases()
    {
        foreach (var c in Cases())
        {
            string calldata = SwapRouter.EncodeExecute(c.Input.Commands, c.Input.Inputs);
            var result = CommandParser.ParseCalldata(calldata, c.Version ?? UniversalRouterVersion.V2_0);
            Assert.Equal(Canon(c.Result), Canon(result));
        }
    }

    // Canonical string renderer used for structural comparison (BigNumbers -> decimal strings, addresses lowercased).
    private static string Canon(object? o)
    {
        switch (o)
        {
            case null:
                return "null";
            case bool b:
                return b ? "true" : "false";
            case BigInteger bi:
                return bi.ToString();
            case int i:
                return i.ToString();
            case long l:
                return l.ToString();
            case string s:
                return s.ToLowerInvariant();
            case UniversalRouterCall call:
                return "call[" + string.Join(";", call.Commands.Select(Canon)) + "]";
            case UniversalRouterCommand cmd:
                return cmd.CommandName + "{" + string.Join(",", cmd.Params.Select(Canon)) + "}";
            case Param p:
                return p.Name + "=" + Canon(p.Value);
            case V3PathItem v3:
                return "hop(" + Canon(v3.TokenIn) + "," + Canon(v3.TokenOut) + "," + v3.Fee + ")";
            case PoolKey pk:
                return "pk(" + Canon(pk.Currency0) + "," + Canon(pk.Currency1) + "," + pk.Fee + "," + pk.TickSpacing + "," + Canon(pk.Hooks) + ")";
            case PathKey pathk:
                return "path(" + Canon(pathk.IntermediateCurrency) + "," + pathk.Fee + "," + pathk.TickSpacing + "," + Canon(pathk.Hooks) + "," + Canon(pathk.HookData) + ")";
            case SwapExactInSingle s:
                return "seis(" + Canon(s.PoolKey) + "," + s.ZeroForOne + "," + s.AmountIn + "," + s.AmountOutMinimum + "," + Canon(s.MinHopPriceX36) + "," + Canon(s.HookData) + ")";
            case SwapExactIn s:
                return "sei(" + Canon(s.CurrencyIn) + "," + Canon(s.Path) + "," + Canon(s.MinHopPriceX36) + "," + s.AmountIn + "," + s.AmountOutMinimum + ")";
            case SwapExactOutSingle s:
                return "seos(" + Canon(s.PoolKey) + "," + s.ZeroForOne + "," + s.AmountOut + "," + s.AmountInMaximum + "," + Canon(s.MinHopPriceX36) + "," + Canon(s.HookData) + ")";
            case SwapExactOut s:
                return "seo(" + Canon(s.CurrencyOut) + "," + Canon(s.Path) + "," + Canon(s.MinHopPriceX36) + "," + s.AmountOut + "," + s.AmountInMaximum + ")";
            case System.Collections.IEnumerable e:
                return "[" + string.Join(",", e.Cast<object?>().Select(Canon)) + "]";
            default:
                return o.ToString() ?? "null";
        }
    }
}
