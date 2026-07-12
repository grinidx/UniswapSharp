using System.Numerics;
using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.UniversalRouter.Entities.Actions;
using UniswapSharp.UniversalRouter.Types;
using UniswapSharp.V4.Utils;

namespace UniswapSharp.UniversalRouter.Utils;

/// <summary>Port of universal-router-sdk <c>utils/validateEncodeSwaps.ts</c>.</summary>
public static class ValidateEncodeSwaps
{
    private static void Invariant(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    // V3 path: 20-byte address + N × (3-byte fee + 20-byte address); minimum is 43 bytes (single hop). Returns N or null.
    private static int? GetV3HopCount(string path)
    {
        if (!path.StartsWith("0x"))
        {
            return null;
        }
        int byteLength = (path.Length - 2) / 2;
        if (byteLength < 43)
        {
            return null;
        }
        int variableSegmentLength = byteLength - 20;
        if (variableSegmentLength < 23 || variableSegmentLength % 23 != 0)
        {
            return null;
        }
        return variableSegmentLength / 23;
    }

    private static bool HasV4MinHopPriceX36(V4Action action) => action switch
    {
        V4SwapExactIn a => a.MinHopPriceX36 is not null,
        V4SwapExactOut a => a.MinHopPriceX36 is not null,
        V4SwapExactInSingle a => a.MinHopPriceX36 is not null,
        V4SwapExactOutSingle a => a.MinHopPriceX36 is not null,
        _ => false,
    };

    private static void ValidateV4HopCounts(IEnumerable<V4Action> actions)
    {
        foreach (var action in actions)
        {
            switch (action)
            {
                case V4SwapExactIn a:
                    Invariant(a.MinHopPriceX36 is null || a.MinHopPriceX36.Count == a.Path.Count, "V4_MIN_HOP_PRICE_X36_LENGTH_MISMATCH");
                    break;
                case V4SwapExactOut a:
                    Invariant(a.MinHopPriceX36 is null || a.MinHopPriceX36.Count == a.Path.Count, "V4_MIN_HOP_PRICE_X36_LENGTH_MISMATCH");
                    break;
            }
        }
    }

    private static void AssertRouterRecipient(string recipient) =>
        Invariant(recipient == Constants.ROUTER_AS_RECIPIENT, "STEP_RECIPIENT_MUST_BE_ROUTER");

    private static void AssertRouterActionRecipient(string recipient) =>
        Invariant(recipient == Constants.ROUTER_AS_RECIPIENT, "V4_ACTION_RECIPIENT_MUST_BE_ROUTER");

    // V4 actions that take a recipient must use router custody so the SDK's settlement sweeps see the funds
    private static void ValidateV4Recipients(IEnumerable<V4Action> actions)
    {
        foreach (var action in actions)
        {
            switch (action)
            {
                case V4Take a:
                    AssertRouterActionRecipient(a.Recipient);
                    break;
                case V4TakePortion a:
                    AssertRouterActionRecipient(a.Recipient);
                    break;
            }
        }
    }

    /// <summary>Validates a normalized spec and its router-owned swap steps.</summary>
    public static void Validate(NormalizedSwapSpecification spec, IReadOnlyList<SwapStep> swapSteps)
    {
        Invariant(swapSteps.Count > 0, "EMPTY_SWAP_STEPS");

        BaseCurrency amountCurrency = spec.Routing.Amount.Currency.Wrapped();
        BaseCurrency quoteCurrency = spec.Routing.Quote.Currency.Wrapped();
        BaseCurrency inputCurrency = spec.Routing.InputToken.Wrapped();
        BaseCurrency outputCurrency = spec.Routing.OutputToken.Wrapped();

        Invariant(!spec.SlippageTolerance.LessThan(BigInteger.Zero), "SLIPPAGE_TOLERANCE");
        Invariant(spec.Recipient != Constants.ZERO_ADDRESS, "RECIPIENT_CANNOT_BE_ZERO");
        Invariant(spec.Recipient != Constants.ROUTER_AS_RECIPIENT, "RECIPIENT_CANNOT_BE_ROUTER");

        // routing.amount is on the exact side of the trade; routing.quote is on the slippage side
        if (spec.TradeType == TradeType.EXACT_INPUT)
        {
            Invariant(amountCurrency.Equals(inputCurrency), "INVALID_ROUTING_AMOUNT_CURRENCY");
            Invariant(quoteCurrency.Equals(outputCurrency), "INVALID_ROUTING_QUOTE_CURRENCY");
        }
        else
        {
            Invariant(amountCurrency.Equals(outputCurrency), "INVALID_ROUTING_AMOUNT_CURRENCY");
            Invariant(quoteCurrency.Equals(inputCurrency), "INVALID_ROUTING_QUOTE_CURRENCY");
        }

        // ApproveProxy ingress lives upstream in the proxy contract
        if (spec.TokenTransferMode == TokenTransferMode.ApproveProxy)
        {
            Invariant(spec.ChainId.HasValue, "PROXY_MISSING_CHAIN_ID");
            Invariant(!spec.Routing.InputToken.IsNative, "PROXY_NATIVE_INPUT");
            Invariant(spec.Permit is null, "PROXY_PERMIT_CONFLICT");
            Invariant(spec.Recipient != Constants.SENDER_AS_RECIPIENT, "PROXY_EXPLICIT_RECIPIENT_REQUIRED");
        }
        // permit2 is ERC20-only; native input pays via msg.value
        Invariant(!(spec.Routing.InputToken.IsNative && spec.Permit is not null), "NATIVE_INPUT_PERMIT");

        // native-ERC20 gas-token input (e.g. Arc USDC): funded via msg.value, never via Permit2
        if (spec.NativeErc20Input == true)
        {
            Invariant(!spec.Routing.InputToken.IsNative, "NATIVE_ERC20_INPUT_NATIVE_TOKEN");
            Invariant(spec.Permit is null, "NATIVE_ERC20_INPUT_PERMIT_CONFLICT");
            Invariant(spec.TokenTransferMode != TokenTransferMode.ApproveProxy, "NATIVE_ERC20_INPUT_PROXY_CONFLICT");
            Invariant(spec.Routing.InputToken.Decimals <= 18, "NATIVE_ERC20_INPUT_DECIMALS");
        }

        // portion fees pair with exact-input; flat fees pair with exact-output
        Invariant(!(spec.Fee is PortionFee && spec.TradeType != TradeType.EXACT_INPUT), "INVALID_PORTION_FEE_TRADE_TYPE");
        Invariant(!(spec.Fee is FlatFee && spec.TradeType != TradeType.EXACT_OUTPUT), "INVALID_FLAT_FEE_TRADE_TYPE");
        Invariant(!(spec.Fee is FlatFee ff &&
            AbiParamEncoder.ToBigInteger(ff.Amount) > spec.Routing.Amount.Quotient), "FLAT_FEE_GT_AMOUNT");

        // v2.0 PAY_PORTION takes whole bps; fractional bps need >=v2.1.1's PAY_PORTION_FULL_PRECISION
        Invariant(!(spec.Fee is PortionFee pf &&
            spec.UrVersion == UniversalRouterVersion.V2_0 &&
            !pf.Fee.Multiply((BigInteger)10_000).Remainder().Numerator.IsZero),
            "FRACTIONAL_BPS_PORTION_FEE_UNSUPPORTED_ON_V2_0");

        // per-step: capability-gate by UR version, recipients must be router custody, per-hop arrays must match hop counts
        foreach (var step in swapSteps)
        {
            if (spec.UrVersion == UniversalRouterVersion.V2_0)
            {
                Invariant(!StepHasMinHop(step), "MIN_HOP_PRICE_X36_UNSUPPORTED_ON_V2_0");
                Invariant(!(step is V4Swap v4s && v4s.V4Actions.Any(HasV4MinHopPriceX36)), "MIN_HOP_PRICE_X36_UNSUPPORTED_ON_V2_0");
            }

            switch (step)
            {
                case V2SwapExactIn s:
                    AssertRouterRecipient(s.Recipient);
                    Invariant(s.MinHopPriceX36 is null || s.MinHopPriceX36.Count == s.Path.Count - 1, "V2_MIN_HOP_PRICE_X36_LENGTH_MISMATCH");
                    break;
                case V2SwapExactOut s:
                    AssertRouterRecipient(s.Recipient);
                    Invariant(s.MinHopPriceX36 is null || s.MinHopPriceX36.Count == s.Path.Count - 1, "V2_MIN_HOP_PRICE_X36_LENGTH_MISMATCH");
                    break;
                case V3SwapExactIn s:
                    {
                        AssertRouterRecipient(s.Recipient);
                        int? hopCount = GetV3HopCount(s.Path);
                        Invariant(hopCount is null || s.MinHopPriceX36 is null || s.MinHopPriceX36.Count == hopCount, "V3_MIN_HOP_PRICE_X36_LENGTH_MISMATCH");
                        break;
                    }
                case V3SwapExactOut s:
                    {
                        AssertRouterRecipient(s.Recipient);
                        int? hopCount = GetV3HopCount(s.Path);
                        Invariant(hopCount is null || s.MinHopPriceX36 is null || s.MinHopPriceX36.Count == hopCount, "V3_MIN_HOP_PRICE_X36_LENGTH_MISMATCH");
                        break;
                    }
                case WrapEth s:
                    AssertRouterRecipient(s.Recipient);
                    break;
                case UnwrapWeth s:
                    AssertRouterRecipient(s.Recipient);
                    break;
                case V4Swap s:
                    ValidateV4HopCounts(s.V4Actions);
                    ValidateV4Recipients(s.V4Actions);
                    break;
            }
        }
    }

    private static bool StepHasMinHop(SwapStep step) => step switch
    {
        V2SwapExactIn s => s.MinHopPriceX36 is not null,
        V2SwapExactOut s => s.MinHopPriceX36 is not null,
        V3SwapExactIn s => s.MinHopPriceX36 is not null,
        V3SwapExactOut s => s.MinHopPriceX36 is not null,
        _ => false,
    };
}
