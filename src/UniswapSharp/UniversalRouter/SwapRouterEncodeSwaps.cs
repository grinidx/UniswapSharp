using System.Numerics;
using UniswapSharp.Core;
using UniswapSharp.UniversalRouter.Entities.Actions;
using UniswapSharp.UniversalRouter.Types;
using UniswapSharp.UniversalRouter.Utils;
using UniswapSharp.V4.Utils;
using Constants = UniswapSharp.UniversalRouter.Utils.Constants;

namespace UniswapSharp.UniversalRouter;

public abstract partial class SwapRouter
{
    /// <summary>
    /// Encodes router-provided swap steps inside the SDK safety envelope. Port of <c>SwapRouter.encodeSwaps</c>.
    /// </summary>
    public static MethodParameters EncodeSwaps(SwapSpecification spec, IReadOnlyList<SwapStep> swapSteps)
    {
        var normalizedSpec = NormalizeEncodeSwapsSpec.Normalize(spec);
        var planner = new RoutePlanner();

        ValidateEncodeSwaps.Validate(normalizedSpec, swapSteps);

        var (exactOrMaxAmountIn, _, netMinOrExactAmountOut) = ComputeEncodeSwapsAmounts.Compute(normalizedSpec);
        var inputToken = normalizedSpec.Routing.InputToken;
        var outputToken = normalizedSpec.Routing.OutputToken;

        // Ingress: pull funds into the router. Native input pays as msg.value at the bottom.
        if (normalizedSpec.TokenTransferMode == TokenTransferMode.Permit2)
        {
            if (normalizedSpec.Permit is not null)
            {
                InputTokens.EncodePermit(planner, normalizedSpec.Permit);
            }

            if (!inputToken.IsNative && normalizedSpec.NativeErc20Input != true)
            {
                planner.AddCommand(
                    CommandType.PERMIT2_TRANSFER_FROM,
                    new object?[] { CurrencyAddress.GetCurrencyAddress(inputToken), Constants.ROUTER_AS_RECIPIENT, exactOrMaxAmountIn },
                    false,
                    normalizedSpec.UrVersion);
            }
        }

        foreach (var step in swapSteps)
        {
            EncodeSwapStep.Encode(planner, step, normalizedSpec.UrVersion);
        }

        // Fee deducted from gross output before final settlement.
        if (normalizedSpec.Fee is PortionFee pf)
        {
            CommandType feeCommandType = Constants.IsAtLeastV2_1_1(normalizedSpec.UrVersion)
                ? CommandType.PAY_PORTION_FULL_PRECISION
                : CommandType.PAY_PORTION;
            string encodedFee = Constants.IsAtLeastV2_1_1(normalizedSpec.UrVersion)
                ? Numbers.EncodeFee1e18(pf.Fee)
                : Numbers.EncodeFeeBips(pf.Fee);

            planner.AddCommand(feeCommandType,
                new object?[] { CurrencyAddress.GetCurrencyAddress(outputToken), pf.Recipient, encodedFee },
                false, normalizedSpec.UrVersion);
        }
        else if (normalizedSpec.Fee is FlatFee ff)
        {
            planner.AddCommand(CommandType.TRANSFER,
                new object?[] { CurrencyAddress.GetCurrencyAddress(outputToken), ff.Recipient, ff.Amount },
                false, normalizedSpec.UrVersion);
        }

        // Assumes routers already normalized final gross output into routing.outputToken.
        planner.AddCommand(CommandType.SWEEP,
            new object?[] { CurrencyAddress.GetCurrencyAddress(outputToken), normalizedSpec.Recipient, netMinOrExactAmountOut },
            false, normalizedSpec.UrVersion);

        // Exact-output uses max input, so refund unused slippage padding to the recipient.
        if (normalizedSpec.TradeType == TradeType.EXACT_OUTPUT)
        {
            planner.AddCommand(CommandType.SWEEP,
                new object?[]
                {
                    normalizedSpec.NativeErc20Input == true ? Constants.ETH_ADDRESS : CurrencyAddress.GetCurrencyAddress(inputToken),
                    normalizedSpec.Recipient,
                    BigInteger.Zero,
                },
                false, normalizedSpec.UrVersion);
        }

        // safeMode: zero-min ETH sweep recovers any native funds left on the router.
        if (normalizedSpec.SafeMode)
        {
            planner.AddCommand(CommandType.SWEEP,
                new object?[] { Constants.ETH_ADDRESS, normalizedSpec.Recipient, BigInteger.Zero },
                false, normalizedSpec.UrVersion);
        }

        // ApproveProxy wraps the inner UR plan in an outer proxy.execute() that handles ingress upstream.
        if (normalizedSpec.TokenTransferMode == TokenTransferMode.ApproveProxy)
        {
            return EncodeProxyCall(
                planner,
                CurrencyAddress.GetCurrencyAddress(inputToken),
                exactOrMaxAmountIn,
                normalizedSpec.ChainId!.Value,
                normalizedSpec.UrVersion,
                normalizedSpec.Deadline);
        }

        // Native input pays via msg.value; ERC20 input is already in the router via Permit2.
        BigInteger nativeCurrencyValue;
        if (inputToken.IsNative)
        {
            nativeCurrencyValue = exactOrMaxAmountIn;
        }
        else if (normalizedSpec.NativeErc20Input == true)
        {
            nativeCurrencyValue = exactOrMaxAmountIn * BigInteger.Pow(10, 18 - inputToken.Decimals);
        }
        else
        {
            nativeCurrencyValue = BigInteger.Zero;
        }

        object? deadline = IsTruthyDeadline(normalizedSpec.Deadline)
            ? AbiParamEncoder.ToBigInteger(normalizedSpec.Deadline!)
            : null;

        return EncodePlan(planner, nativeCurrencyValue, deadline);
    }

    // JS truthiness: primitive 0/null are falsy; a BigNumber (object) — even zero — is truthy.
    private static bool IsTruthyDeadline(object? deadline) => deadline switch
    {
        null => false,
        bool b => b,
        int i => i != 0,
        long l => l != 0,
        BigInteger => true,
        string s => s.Length != 0,
        _ => true,
    };
}
