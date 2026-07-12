using System.Numerics;
using UniswapSharp.Core;
using UniswapSharp.UniversalRouter.Types;
using UniswapSharp.V4.Utils;

namespace UniswapSharp.UniversalRouter.Utils;

/// <summary>
/// Result of <see cref="ComputeEncodeSwapsAmounts.Compute"/>: gross (pre-fee) and net (post-fee) amounts.
/// </summary>
public sealed record EncodeSwapsAmounts(
    BigInteger ExactOrMaxAmountIn,
    BigInteger GrossMinOrExactAmountOut,
    BigInteger NetMinOrExactAmountOut);

/// <summary>Port of universal-router-sdk <c>utils/computeEncodeSwapsAmounts.ts</c>.</summary>
public static class ComputeEncodeSwapsAmounts
{
    private static readonly BigInteger E18 = BigInteger.Pow(10, 18);

    /// <summary>Applies slippage (and fee, where applicable) to the routing amounts.</summary>
    public static EncodeSwapsAmounts Compute(NormalizedSwapSpecification spec)
    {
        BigInteger routingAmount = spec.Routing.Amount.Quotient;
        BigInteger routingQuote = spec.Routing.Quote.Quotient;
        BigInteger slippageNumerator = spec.SlippageTolerance.Numerator;
        BigInteger slippageDenominator = spec.SlippageTolerance.Denominator;

        if (spec.TradeType == TradeType.EXACT_INPUT)
        {
            BigInteger grossMin = routingQuote * (slippageDenominator - slippageNumerator) / slippageDenominator;

            if (spec.Fee is PortionFee pf)
            {
                BigInteger feeAmount = Constants.IsAtLeastV2_1_1(spec.UrVersion)
                    ? grossMin * pf.Fee.Multiply(E18).Quotient / E18
                    : grossMin * pf.Fee.Multiply((BigInteger)10_000).Quotient / 10_000;

                return new EncodeSwapsAmounts(routingAmount, grossMin, grossMin - feeAmount);
            }

            return new EncodeSwapsAmounts(routingAmount, grossMin, grossMin);
        }

        BigInteger exactOrMaxAmountIn = routingQuote * (slippageDenominator + slippageNumerator) / slippageDenominator;
        BigInteger grossOut = routingAmount;
        BigInteger netOut = spec.Fee is FlatFee ff
            ? grossOut - AbiParamEncoder.ToBigInteger(ff.Amount)
            : grossOut;

        return new EncodeSwapsAmounts(exactOrMaxAmountIn, grossOut, netOut);
    }
}
