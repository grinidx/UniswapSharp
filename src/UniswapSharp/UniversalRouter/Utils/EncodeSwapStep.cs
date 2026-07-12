using UniswapSharp.UniversalRouter.Types;
using UniswapSharp.V4.Utils;

namespace UniswapSharp.UniversalRouter.Utils;

/// <summary>Port of universal-router-sdk <c>utils/encodeSwapStep.ts</c>.</summary>
public static class EncodeSwapStep
{
    // V2/V3 swap params hardcode payerIsUser=false; encodeSwaps pulls funds into the router first via
    // PERMIT2_TRANSFER_FROM.
    /// <summary>Encodes one router-owned <see cref="SwapStep"/> onto the planner.</summary>
    public static void Encode(RoutePlanner planner, SwapStep step, UniversalRouterVersion? urVersion = null)
    {
        bool useV2_1_1 = Constants.IsAtLeastV2_1_1(urVersion);

        switch (step)
        {
            case V2SwapExactIn s:
                {
                    var parms = new List<object?> { s.Recipient, s.AmountIn, s.AmountOutMin, s.Path.Cast<object?>().ToArray(), false };
                    if (useV2_1_1)
                    {
                        parms.Add(MinHop(s.MinHopPriceX36));
                    }
                    planner.AddCommand(CommandType.V2_SWAP_EXACT_IN, parms.ToArray(), false, urVersion);
                    return;
                }
            case V2SwapExactOut s:
                {
                    var parms = new List<object?> { s.Recipient, s.AmountOut, s.AmountInMax, s.Path.Cast<object?>().ToArray(), false };
                    if (useV2_1_1)
                    {
                        parms.Add(MinHop(s.MinHopPriceX36));
                    }
                    planner.AddCommand(CommandType.V2_SWAP_EXACT_OUT, parms.ToArray(), false, urVersion);
                    return;
                }
            case V3SwapExactIn s:
                {
                    var parms = new List<object?> { s.Recipient, s.AmountIn, s.AmountOutMin, s.Path, false };
                    if (useV2_1_1)
                    {
                        parms.Add(MinHop(s.MinHopPriceX36));
                    }
                    planner.AddCommand(CommandType.V3_SWAP_EXACT_IN, parms.ToArray(), false, urVersion);
                    return;
                }
            case V3SwapExactOut s:
                {
                    var parms = new List<object?> { s.Recipient, s.AmountOut, s.AmountInMax, s.Path, false };
                    if (useV2_1_1)
                    {
                        parms.Add(MinHop(s.MinHopPriceX36));
                    }
                    planner.AddCommand(CommandType.V3_SWAP_EXACT_OUT, parms.ToArray(), false, urVersion);
                    return;
                }
            case V4Swap s:
                {
                    var v4Planner = new V4Planner();
                    foreach (var v4Action in s.V4Actions)
                    {
                        var (action, actionParams) = EncodeV4Action.Encode(v4Action, urVersion);
                        v4Planner.AddAction(action, actionParams, V4URVersion.ToV4URVersion(urVersion));
                    }
                    planner.AddCommand(CommandType.V4_SWAP, new object?[] { v4Planner.Finalize() }, false, urVersion);
                    return;
                }
            case WrapEth s:
                planner.AddCommand(CommandType.WRAP_ETH, new object?[] { s.Recipient, s.Amount }, false, urVersion);
                return;
            case UnwrapWeth s:
                planner.AddCommand(CommandType.UNWRAP_WETH, new object?[] { s.Recipient, s.AmountMin }, false, urVersion);
                return;
            default:
                throw new InvalidOperationException($"Unknown swap step type: {step.GetType().Name}");
        }
    }

    private static object?[] MinHop(IReadOnlyList<object>? minHop) => (minHop ?? Array.Empty<object>()).ToArray();
}
