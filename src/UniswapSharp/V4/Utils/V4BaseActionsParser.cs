using System.Numerics;
using UniswapSharp.V4.Entities;

namespace UniswapSharp.V4.Utils;

/// <summary>One decoded, named action parameter. Ported from v4-sdk/src/utils/v4BaseActionsParser.ts (<c>Param</c>).</summary>
public sealed record Param(string Name, object? Value);

/// <summary>
/// A single decoded V4 router action: its enum name, its <see cref="Actions"/> opcode, and the ordered,
/// named parameters decoded from calldata. Ported from v4-sdk/src/utils/v4BaseActionsParser.ts (<c>V4RouterAction</c>).
/// </summary>
public sealed record V4RouterAction(string ActionName, Actions ActionType, IReadOnlyList<Param> Params);

/// <summary>
/// The structured result of decoding a V4 router action sequence. Ported from
/// v4-sdk/src/utils/v4BaseActionsParser.ts (<c>V4RouterCall</c>).
/// </summary>
public sealed record V4RouterCall(IReadOnlyList<V4RouterAction> Actions);

/// <summary>
/// Decoded <c>SWAP_EXACT_IN_SINGLE</c> swap struct. <see cref="MinHopPriceX36"/> is only present for UR
/// 2.1.1+. Ported from v4-sdk/src/utils/v4BaseActionsParser.ts (<c>SwapExactInSingle</c>).
/// </summary>
public sealed record SwapExactInSingle(
    PoolKey PoolKey,
    bool ZeroForOne,
    BigInteger AmountIn,
    BigInteger AmountOutMinimum,
    BigInteger? MinHopPriceX36,
    string HookData);

/// <summary>
/// Decoded <c>SWAP_EXACT_IN</c> swap struct. <see cref="MinHopPriceX36"/> is only present for UR 2.1.1+.
/// Ported from v4-sdk/src/utils/v4BaseActionsParser.ts (<c>SwapExactIn</c>).
/// </summary>
public sealed record SwapExactIn(
    string CurrencyIn,
    IReadOnlyList<PathKey> Path,
    IReadOnlyList<BigInteger>? MinHopPriceX36,
    BigInteger AmountIn,
    BigInteger AmountOutMinimum);

/// <summary>
/// Decoded <c>SWAP_EXACT_OUT_SINGLE</c> swap struct. <see cref="MinHopPriceX36"/> is only present for UR
/// 2.1.1+. Ported from v4-sdk/src/utils/v4BaseActionsParser.ts (<c>SwapExactOutSingle</c>).
/// </summary>
public sealed record SwapExactOutSingle(
    PoolKey PoolKey,
    bool ZeroForOne,
    BigInteger AmountOut,
    BigInteger AmountInMaximum,
    BigInteger? MinHopPriceX36,
    string HookData);

/// <summary>
/// Decoded <c>SWAP_EXACT_OUT</c> swap struct. <see cref="MinHopPriceX36"/> is only present for UR 2.1.1+.
/// Ported from v4-sdk/src/utils/v4BaseActionsParser.ts (<c>SwapExactOut</c>).
/// </summary>
public sealed record SwapExactOut(
    string CurrencyOut,
    IReadOnlyList<PathKey> Path,
    IReadOnlyList<BigInteger>? MinHopPriceX36,
    BigInteger AmountOut,
    BigInteger AmountInMaximum);

/// <summary>
/// Decodes V4 router calldata back into a structured <see cref="V4RouterCall"/> — the inverse of
/// <see cref="V4Planner"/>. Ported from v4-sdk/src/utils/v4BaseActionsParser.ts (<c>V4BaseActionsParser</c>).
/// </summary>
public static class V4BaseActionsParser
{
    /// <summary>
    /// Decodes the outer <c>(bytes actions, bytes[] params)</c> calldata, then for each action byte looks up
    /// its ABI definition and decodes the matching params entry, applying the sub-parser transforms that turn
    /// raw decoded tuples into friendly <c>poolKey</c> / path-key / swap structures.
    /// </summary>
    public static V4RouterCall ParseCalldata(string calldata, URVersion urVersion = URVersion.V2_0)
    {
        var decoded = AbiParamDecoder.Decode(new[] { "bytes", "bytes[]" }, calldata);
        var actions = (string)decoded[0]!;
        var inputs = (List<object?>)decoded[1]!;

        var actionTypes = GetActions(actions);

        var parsedActions = new List<V4RouterAction>();
        for (int i = 0; i < actionTypes.Count; i++)
        {
            Actions actionType = actionTypes[i];

            // Use the V2.1.1 ABI for swap actions when parsing V2.1.1+, otherwise the base (V2.0) ABI.
            IReadOnlyList<ParamType> abiDef =
                V4Planner.IsAtLeastV2_1_1(urVersion) && V4Planner.V4_SWAP_ACTIONS_V2_1_1.ContainsKey(actionType)
                    ? V4Planner.V4_SWAP_ACTIONS_V2_1_1[actionType]
                    : V4Planner.V4_BASE_ACTIONS_ABI_DEFINITION[actionType];

            var rawParams = AbiParamDecoder.Decode(abiDef.Select(command => command.Type).ToArray(), (string)inputs[i]!);

            var parameters = new List<Param>();
            for (int j = 0; j < rawParams.Count; j++)
            {
                object? value = abiDef[j].Subparser switch
                {
                    Subparser.V4SwapExactInSingle => ParseV4ExactInSingle((List<object?>)rawParams[j]!, urVersion),
                    Subparser.V4SwapExactIn => ParseV4ExactIn((List<object?>)rawParams[j]!, urVersion),
                    Subparser.V4SwapExactOutSingle => ParseV4ExactOutSingle((List<object?>)rawParams[j]!, urVersion),
                    Subparser.V4SwapExactOut => ParseV4ExactOut((List<object?>)rawParams[j]!, urVersion),
                    Subparser.PoolKey => ParsePoolKey((List<object?>)rawParams[j]!),
                    _ => rawParams[j],
                };
                parameters.Add(new Param(abiDef[j].Name, value));
            }

            parsedActions.Add(new V4RouterAction(actionType.ToString(), actionType, parameters));
        }

        return new V4RouterCall(parsedActions);
    }

    /// <summary>Parses the action opcodes out of the concatenated <c>0x</c>-prefixed action byte string.</summary>
    private static List<Actions> GetActions(string actions)
    {
        var actionTypes = new List<Actions>();
        for (int i = 2; i < actions.Length; i += 2)
        {
            string b = actions.Substring(i, 2);
            actionTypes.Add((Actions)Convert.ToInt32(b, 16));
        }
        return actionTypes;
    }

    private static PoolKey ParsePoolKey(List<object?> data)
    {
        string currency0 = (string)data[0]!;
        string currency1 = (string)data[1]!;
        int fee = (int)(BigInteger)data[2]!;
        int tickSpacing = (int)(BigInteger)data[3]!;
        string hooks = (string)data[4]!;
        return new PoolKey(currency0, currency1, fee, tickSpacing, hooks);
    }

    private static PathKey ParsePathKey(List<object?> data)
    {
        string intermediateCurrency = (string)data[0]!;
        int fee = (int)(BigInteger)data[1]!;
        int tickSpacing = (int)(BigInteger)data[2]!;
        string hooks = (string)data[3]!;
        string hookData = (string)data[4]!;
        return new PathKey(intermediateCurrency, fee, tickSpacing, hooks, hookData);
    }

    private static SwapExactInSingle ParseV4ExactInSingle(List<object?> data, URVersion urVersion)
    {
        PoolKey poolKey = ParsePoolKey((List<object?>)data[0]!);
        bool zeroForOne = (bool)data[1]!;
        BigInteger amountIn = (BigInteger)data[2]!;
        BigInteger amountOutMinimum = (BigInteger)data[3]!;

        if (urVersion == URVersion.V2_0)
        {
            // V2.0: [poolKey, zeroForOne, amountIn, amountOutMinimum, hookData]
            string hookData = (string)data[4]!;
            return new SwapExactInSingle(poolKey, zeroForOne, amountIn, amountOutMinimum, null, hookData);
        }

        // V2.1.1: [poolKey, zeroForOne, amountIn, amountOutMinimum, minHopPriceX36, hookData]
        BigInteger minHopPriceX36 = (BigInteger)data[4]!;
        string hookDataV211 = (string)data[5]!;
        return new SwapExactInSingle(poolKey, zeroForOne, amountIn, amountOutMinimum, minHopPriceX36, hookDataV211);
    }

    private static SwapExactIn ParseV4ExactIn(List<object?> data, URVersion urVersion)
    {
        var path = ((List<object?>)data[1]!).Select(pk => ParsePathKey((List<object?>)pk!)).ToList();
        string currencyIn = (string)data[0]!;

        if (urVersion == URVersion.V2_0)
        {
            // V2.0: [currencyIn, path, amountIn, amountOutMinimum]
            BigInteger amountIn = (BigInteger)data[2]!;
            BigInteger amountOutMinimum = (BigInteger)data[3]!;
            return new SwapExactIn(currencyIn, path, null, amountIn, amountOutMinimum);
        }

        // V2.1.1: [currencyIn, path, minHopPriceX36, amountIn, amountOutMinimum]
        var minHopPriceX36 = ((List<object?>)data[2]!).Select(x => (BigInteger)x!).ToList();
        BigInteger amountInV211 = (BigInteger)data[3]!;
        BigInteger amountOutMinimumV211 = (BigInteger)data[4]!;
        return new SwapExactIn(currencyIn, path, minHopPriceX36, amountInV211, amountOutMinimumV211);
    }

    private static SwapExactOutSingle ParseV4ExactOutSingle(List<object?> data, URVersion urVersion)
    {
        PoolKey poolKey = ParsePoolKey((List<object?>)data[0]!);
        bool zeroForOne = (bool)data[1]!;
        BigInteger amountOut = (BigInteger)data[2]!;
        BigInteger amountInMaximum = (BigInteger)data[3]!;

        if (urVersion == URVersion.V2_0)
        {
            // V2.0: [poolKey, zeroForOne, amountOut, amountInMaximum, hookData]
            string hookData = (string)data[4]!;
            return new SwapExactOutSingle(poolKey, zeroForOne, amountOut, amountInMaximum, null, hookData);
        }

        // V2.1.1: [poolKey, zeroForOne, amountOut, amountInMaximum, minHopPriceX36, hookData]
        BigInteger minHopPriceX36 = (BigInteger)data[4]!;
        string hookDataV211 = (string)data[5]!;
        return new SwapExactOutSingle(poolKey, zeroForOne, amountOut, amountInMaximum, minHopPriceX36, hookDataV211);
    }

    private static SwapExactOut ParseV4ExactOut(List<object?> data, URVersion urVersion)
    {
        var path = ((List<object?>)data[1]!).Select(pk => ParsePathKey((List<object?>)pk!)).ToList();
        string currencyOut = (string)data[0]!;

        if (urVersion == URVersion.V2_0)
        {
            // V2.0: [currencyOut, path, amountOut, amountInMaximum]
            BigInteger amountOut = (BigInteger)data[2]!;
            BigInteger amountInMaximum = (BigInteger)data[3]!;
            return new SwapExactOut(currencyOut, path, null, amountOut, amountInMaximum);
        }

        // V2.1.1: [currencyOut, path, minHopPriceX36, amountOut, amountInMaximum]
        var minHopPriceX36 = ((List<object?>)data[2]!).Select(x => (BigInteger)x!).ToList();
        BigInteger amountOutV211 = (BigInteger)data[3]!;
        BigInteger amountInMaximumV211 = (BigInteger)data[4]!;
        return new SwapExactOut(currencyOut, path, minHopPriceX36, amountOutV211, amountInMaximumV211);
    }
}
