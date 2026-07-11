using System.Numerics;
using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.V4.Entities;
using Act = UniswapSharp.V4.Utils.Actions;

namespace UniswapSharp.V4.Utils;

/// <summary>
/// UniversalRouter contract version. Ported from v4-sdk/src/utils/v4Planner.ts (<c>URVersion</c>).
/// </summary>
public enum URVersion
{
    /// <summary>2.0 — swap structs without <c>minHopPriceX36</c>.</summary>
    V2_0,

    /// <summary>2.1.1 — swap structs include per-hop <c>minHopPriceX36</c>.</summary>
    V2_1_1,

    /// <summary>2.2.0.</summary>
    V2_2_0,
}

/// <summary>
/// Constants that define which action to perform. Ported from v4-sdk/src/utils/v4Planner.ts
/// (<c>Actions</c>). The opcode values are significant: they are concatenated (one byte each) into
/// the planner's <c>actions</c> byte string. Not all actions are supported yet.
/// </summary>
public enum Actions
{
    // Liquidity actions
    INCREASE_LIQUIDITY = 0x00,
    DECREASE_LIQUIDITY = 0x01,
    MINT_POSITION = 0x02,
    BURN_POSITION = 0x03,

    // Swapping
    SWAP_EXACT_IN_SINGLE = 0x06,
    SWAP_EXACT_IN = 0x07,
    SWAP_EXACT_OUT_SINGLE = 0x08,
    SWAP_EXACT_OUT = 0x09,

    // Settling
    SETTLE = 0x0b,
    SETTLE_ALL = 0x0c,
    SETTLE_PAIR = 0x0d,

    // Taking
    TAKE = 0x0e,
    TAKE_ALL = 0x0f,
    TAKE_PORTION = 0x10,
    TAKE_PAIR = 0x11,

    CLOSE_CURRENCY = 0x12,
    SWEEP = 0x14,

    // Wrapping / unwrapping native
    UNWRAP = 0x16,
}

/// <summary>
/// Identifies how a parameter should be expanded by the parser phase. Ported from
/// v4-sdk/src/utils/v4Planner.ts (<c>Subparser</c>).
/// </summary>
public enum Subparser
{
    V4SwapExactInSingle,
    V4SwapExactIn,
    V4SwapExactOutSingle,
    V4SwapExactOut,
    PoolKey,
}

/// <summary>
/// One named ABI parameter of an action, optionally annotated with the <see cref="Subparser"/> used
/// to expand it. Ported from v4-sdk/src/utils/v4Planner.ts (<c>ParamType</c>).
/// </summary>
public sealed record ParamType(string Name, string Type, Subparser? Subparser = null);

/// <summary>
/// Builds the <c>actions</c>/<c>params</c> pair consumed by the V4 UniversalRouter, encoding each
/// action's inputs with the ABI shapes below. Ported from v4-sdk/src/utils/v4Planner.ts.
/// </summary>
public class V4Planner
{
    // Struct type strings, identical to upstream v4Planner.ts.
    private const string POOL_KEY_STRUCT =
        "(address currency0,address currency1,uint24 fee,int24 tickSpacing,address hooks)";

    private const string PATH_KEY_STRUCT =
        "(address intermediateCurrency,uint256 fee,int24 tickSpacing,address hooks,bytes hookData)";

    // UR 2.0 swap structs (without minHopPriceX36)
    private const string SWAP_EXACT_IN_SINGLE_STRUCT_V2_0 =
        "(" + POOL_KEY_STRUCT + " poolKey,bool zeroForOne,uint128 amountIn,uint128 amountOutMinimum,bytes hookData)";

    private const string SWAP_EXACT_IN_STRUCT_V2_0 =
        "(address currencyIn," + PATH_KEY_STRUCT + "[] path,uint128 amountIn,uint128 amountOutMinimum)";

    private const string SWAP_EXACT_OUT_SINGLE_STRUCT_V2_0 =
        "(" + POOL_KEY_STRUCT + " poolKey,bool zeroForOne,uint128 amountOut,uint128 amountInMaximum,bytes hookData)";

    private const string SWAP_EXACT_OUT_STRUCT_V2_0 =
        "(address currencyOut," + PATH_KEY_STRUCT + "[] path,uint128 amountOut,uint128 amountInMaximum)";

    // UR 2.1.1 swap structs (with minHopPriceX36)
    private const string SWAP_EXACT_IN_SINGLE_STRUCT_V2_1_1 =
        "(" + POOL_KEY_STRUCT +
        " poolKey,bool zeroForOne,uint128 amountIn,uint128 amountOutMinimum,uint256 minHopPriceX36,bytes hookData)";

    private const string SWAP_EXACT_IN_STRUCT_V2_1_1 =
        "(address currencyIn," + PATH_KEY_STRUCT +
        "[] path,uint256[] minHopPriceX36,uint128 amountIn,uint128 amountOutMinimum)";

    private const string SWAP_EXACT_OUT_SINGLE_STRUCT_V2_1_1 =
        "(" + POOL_KEY_STRUCT +
        " poolKey,bool zeroForOne,uint128 amountOut,uint128 amountInMaximum,uint256 minHopPriceX36,bytes hookData)";

    private const string SWAP_EXACT_OUT_STRUCT_V2_1_1 =
        "(address currencyOut," + PATH_KEY_STRUCT +
        "[] path,uint256[] minHopPriceX36,uint128 amountOut,uint128 amountInMaximum)";

    // V4_BASE_ACTIONS_ABI_DEFINITION uses V2.0 structs (default, without minHopPriceX36).
    private const string SWAP_EXACT_IN_SINGLE_STRUCT = SWAP_EXACT_IN_SINGLE_STRUCT_V2_0;
    private const string SWAP_EXACT_IN_STRUCT = SWAP_EXACT_IN_STRUCT_V2_0;
    private const string SWAP_EXACT_OUT_SINGLE_STRUCT = SWAP_EXACT_OUT_SINGLE_STRUCT_V2_0;
    private const string SWAP_EXACT_OUT_STRUCT = SWAP_EXACT_OUT_STRUCT_V2_0;

    private static readonly BigInteger FullDeltaAmount = BigInteger.Zero;

    /// <summary>The per-action ABI parameter lists (V2.0 defaults). Ported from upstream.</summary>
    public static readonly IReadOnlyDictionary<Actions, IReadOnlyList<ParamType>> V4_BASE_ACTIONS_ABI_DEFINITION =
        new Dictionary<Actions, IReadOnlyList<ParamType>>
        {
            // Liquidity commands
            [Act.INCREASE_LIQUIDITY] = new ParamType[]
            {
                new("tokenId", "uint256"),
                new("liquidity", "uint256"),
                new("amount0Max", "uint128"),
                new("amount1Max", "uint128"),
                new("hookData", "bytes"),
            },
            [Act.DECREASE_LIQUIDITY] = new ParamType[]
            {
                new("tokenId", "uint256"),
                new("liquidity", "uint256"),
                new("amount0Min", "uint128"),
                new("amount1Min", "uint128"),
                new("hookData", "bytes"),
            },
            [Act.MINT_POSITION] = new ParamType[]
            {
                new("poolKey", POOL_KEY_STRUCT, Subparser.PoolKey),
                new("tickLower", "int24"),
                new("tickUpper", "int24"),
                new("liquidity", "uint256"),
                new("amount0Max", "uint128"),
                new("amount1Max", "uint128"),
                new("owner", "address"),
                new("hookData", "bytes"),
            },
            [Act.BURN_POSITION] = new ParamType[]
            {
                new("tokenId", "uint256"),
                new("amount0Min", "uint128"),
                new("amount1Min", "uint128"),
                new("hookData", "bytes"),
            },

            // Swapping commands
            [Act.SWAP_EXACT_IN_SINGLE] = new ParamType[]
            {
                new("swap", SWAP_EXACT_IN_SINGLE_STRUCT, Subparser.V4SwapExactInSingle),
            },
            [Act.SWAP_EXACT_IN] = new ParamType[]
            {
                new("swap", SWAP_EXACT_IN_STRUCT, Subparser.V4SwapExactIn),
            },
            [Act.SWAP_EXACT_OUT_SINGLE] = new ParamType[]
            {
                new("swap", SWAP_EXACT_OUT_SINGLE_STRUCT, Subparser.V4SwapExactOutSingle),
            },
            [Act.SWAP_EXACT_OUT] = new ParamType[]
            {
                new("swap", SWAP_EXACT_OUT_STRUCT, Subparser.V4SwapExactOut),
            },

            // Payments commands
            [Act.SETTLE] = new ParamType[]
            {
                new("currency", "address"),
                new("amount", "uint256"),
                new("payerIsUser", "bool"),
            },
            [Act.SETTLE_ALL] = new ParamType[]
            {
                new("currency", "address"),
                new("maxAmount", "uint256"),
            },
            [Act.SETTLE_PAIR] = new ParamType[]
            {
                new("currency0", "address"),
                new("currency1", "address"),
            },
            [Act.TAKE] = new ParamType[]
            {
                new("currency", "address"),
                new("recipient", "address"),
                new("amount", "uint256"),
            },
            [Act.TAKE_ALL] = new ParamType[]
            {
                new("currency", "address"),
                new("minAmount", "uint256"),
            },
            [Act.TAKE_PORTION] = new ParamType[]
            {
                new("currency", "address"),
                new("recipient", "address"),
                new("bips", "uint256"),
            },
            [Act.TAKE_PAIR] = new ParamType[]
            {
                new("currency0", "address"),
                new("currency1", "address"),
                new("recipient", "address"),
            },
            [Act.CLOSE_CURRENCY] = new ParamType[]
            {
                new("currency", "address"),
            },
            [Act.SWEEP] = new ParamType[]
            {
                new("currency", "address"),
                new("recipient", "address"),
            },
            [Act.UNWRAP] = new ParamType[]
            {
                new("amount", "uint256"),
            },
        };

    /// <summary>UR 2.1.1-specific ABI definitions for the swap actions (with minHopPriceX36).</summary>
    public static readonly IReadOnlyDictionary<Actions, IReadOnlyList<ParamType>> V4_SWAP_ACTIONS_V2_1_1 =
        new Dictionary<Actions, IReadOnlyList<ParamType>>
        {
            [Act.SWAP_EXACT_IN_SINGLE] = new ParamType[]
            {
                new("swap", SWAP_EXACT_IN_SINGLE_STRUCT_V2_1_1, Subparser.V4SwapExactInSingle),
            },
            [Act.SWAP_EXACT_IN] = new ParamType[]
            {
                new("swap", SWAP_EXACT_IN_STRUCT_V2_1_1, Subparser.V4SwapExactIn),
            },
            [Act.SWAP_EXACT_OUT_SINGLE] = new ParamType[]
            {
                new("swap", SWAP_EXACT_OUT_SINGLE_STRUCT_V2_1_1, Subparser.V4SwapExactOutSingle),
            },
            [Act.SWAP_EXACT_OUT] = new ParamType[]
            {
                new("swap", SWAP_EXACT_OUT_STRUCT_V2_1_1, Subparser.V4SwapExactOut),
            },
        };

    /// <summary>The concatenated action opcodes, a lower-case <c>0x</c>-prefixed byte string.</summary>
    public string Actions { get; private set; } = Constants.EMPTY_BYTES;

    /// <summary>The ABI-encoded input for each action, aligned with <see cref="Actions"/>.</summary>
    public List<string> Params { get; } = new();

    /// <summary>
    /// True for UR versions at or above 2.1.1 (which carry <c>minHopPriceX36</c> in the swap structs).
    /// Upstream compares the version strings numerically; the enum ordering is equivalent.
    /// </summary>
    public static bool IsAtLeastV2_1_1(URVersion version) => version >= URVersion.V2_1_1;

    /// <summary>Adds an arbitrary action with its ABI-ordered parameter values.</summary>
    public V4Planner AddAction(Actions type, object?[] parameters, URVersion urVersion = URVersion.V2_0)
    {
        var (action, encodedInput) = CreateAction(type, parameters, urVersion);
        Params.Add(encodedInput);
        Actions += ((int)action).ToString("x2");
        return this;
    }

    /// <summary>Adds a swap action derived from a single-swap trade.</summary>
    public V4Planner AddTrade<TInput, TOutput>(
        Trade<TInput, TOutput> trade,
        Percent? slippageTolerance = null,
        IReadOnlyList<BigInteger>? minHopPriceX36 = null,
        URVersion urVersion = URVersion.V2_0)
        where TInput : BaseCurrency
        where TOutput : BaseCurrency
    {
        bool exactOutput = trade.TradeType == TradeType.EXACT_OUTPUT;

        // exactInput sometimes performs aggregated slippage checks, but not with exactOutput.
        if (exactOutput && slippageTolerance is null)
        {
            throw new InvalidOperationException("ExactOut requires slippageTolerance");
        }
        if (trade.Swaps.Count != 1)
        {
            throw new InvalidOperationException(
                "Only accepts Trades with 1 swap (must break swaps into individual trades)");
        }
        if (!(urVersion == URVersion.V2_0
              || minHopPriceX36 is null
              || minHopPriceX36.Count == 0
              || minHopPriceX36.Count == trade.Route.Pools.Count))
        {
            throw new InvalidOperationException(
                $"minHopPriceX36 length ({minHopPriceX36?.Count}) must equal route.pools.length ({trade.Route.Pools.Count})");
        }

        Actions actionType = exactOutput ? Act.SWAP_EXACT_OUT : Act.SWAP_EXACT_IN;

        string currencyIn = CurrencyAddress(trade.Route.PathInput);
        string currencyOut = CurrencyAddress(trade.Route.PathOutput);
        object?[] path = PathKeysToTuples(EncodeRouteToPath.Encode(trade.Route, exactOutput));

        object?[] swapStruct;
        if (exactOutput)
        {
            BigInteger amountOut = trade.OutputAmount.Quotient;
            BigInteger amountInMaximum = trade.MaximumAmountIn(slippageTolerance ?? new Percent(0)).Quotient;
            swapStruct = IsAtLeastV2_1_1(urVersion)
                ? new object?[] { currencyOut, path, MinHopTuples(minHopPriceX36), amountOut, amountInMaximum }
                : new object?[] { currencyOut, path, amountOut, amountInMaximum };
        }
        else
        {
            BigInteger amountIn = trade.InputAmount.Quotient;
            BigInteger amountOutMinimum = slippageTolerance != null
                ? trade.MinimumAmountOut(slippageTolerance).Quotient
                : BigInteger.Zero;
            swapStruct = IsAtLeastV2_1_1(urVersion)
                ? new object?[] { currencyIn, path, MinHopTuples(minHopPriceX36), amountIn, amountOutMinimum }
                : new object?[] { currencyIn, path, amountIn, amountOutMinimum };
        }

        AddSwapAction(actionType, new object?[] { swapStruct }, urVersion);
        return this;
    }

    /// <summary>Adds a SETTLE action; a null amount settles the full open delta.</summary>
    public V4Planner AddSettle(BaseCurrency currency, bool payerIsUser, BigInteger? amount = null)
    {
        AddAction(Act.SETTLE, new object?[] { CurrencyAddress(currency), amount ?? FullDeltaAmount, payerIsUser });
        return this;
    }

    /// <summary>Adds a TAKE action; a null amount takes the full open delta.</summary>
    public V4Planner AddTake(BaseCurrency currency, string recipient, BigInteger? amount = null)
    {
        BigInteger takeAmount = amount ?? FullDeltaAmount;
        AddAction(Act.TAKE, new object?[] { CurrencyAddress(currency), recipient, takeAmount });
        return this;
    }

    /// <summary>Adds an UNWRAP action for the given amount.</summary>
    public V4Planner AddUnwrap(BigInteger amount)
    {
        AddAction(Act.UNWRAP, new object?[] { amount });
        return this;
    }

    /// <summary>Encodes the accumulated actions/params as <c>encode(['bytes','bytes[]'], [actions, params])</c>.</summary>
    public string Finalize() =>
        AbiParamEncoder.Encode(new[] { "bytes", "bytes[]" }, new object?[] { Actions, Params.Cast<object?>().ToArray() });

    private V4Planner AddSwapAction(Actions type, object?[] parameters, URVersion urVersion)
    {
        // Use the V2.1.1 ABI (with minHopPriceX36) for V2.1.1+, otherwise default to the V2.0 ABI.
        IReadOnlyList<ParamType> abiDef = IsAtLeastV2_1_1(urVersion)
            ? V4_SWAP_ACTIONS_V2_1_1[type]
            : V4_BASE_ACTIONS_ABI_DEFINITION[type];
        string encodedInput = AbiParamEncoder.Encode(abiDef.Select(v => v.Type).ToArray(), parameters);
        Params.Add(encodedInput);
        Actions += ((int)type).ToString("x2");
        return this;
    }

    private static (Actions Action, string EncodedInput) CreateAction(
        Actions action, object?[] parameters, URVersion urVersion)
    {
        IReadOnlyList<ParamType> abiDef =
            IsAtLeastV2_1_1(urVersion) && V4_SWAP_ACTIONS_V2_1_1.ContainsKey(action)
                ? V4_SWAP_ACTIONS_V2_1_1[action]
                : V4_BASE_ACTIONS_ABI_DEFINITION[action];
        string encodedInput = AbiParamEncoder.Encode(abiDef.Select(v => v.Type).ToArray(), parameters);
        return (action, encodedInput);
    }

    private static string CurrencyAddress(BaseCurrency currency) =>
        currency.IsNative ? Constants.ADDRESS_ZERO : currency.Wrapped().Address;

    private static object?[] PathKeysToTuples(IEnumerable<PathKey> path) =>
        path.Select(pk => (object?)new object?[]
        {
            pk.IntermediateCurrency,
            (BigInteger)pk.Fee,
            pk.TickSpacing,
            pk.Hooks,
            pk.HookData,
        }).ToArray();

    private static object?[] MinHopTuples(IReadOnlyList<BigInteger>? minHopPriceX36) =>
        (minHopPriceX36 ?? Array.Empty<BigInteger>()).Select(x => (object?)x).ToArray();
}
