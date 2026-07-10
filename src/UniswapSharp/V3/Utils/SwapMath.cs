using System.Numerics;
using static UniswapSharp.V3.Utils.V3Swap;

namespace UniswapSharp.V3.Utils;

public abstract class SwapMath
{
    private static readonly BigInteger MAX_FEE = BigInteger.Pow(10, 6);

    private SwapMath() { }


    public class ReturnValues
    {
        public BigInteger SqrtRatioNextX96 { get; set; } = BigInteger.Zero;
        public BigInteger AmountIn { get; set; } = BigInteger.Zero;
        public BigInteger AmountOut { get; set; } = BigInteger.Zero;
        public BigInteger FeeAmount { get; set; } = BigInteger.Zero;
    }

    public static (BigInteger, BigInteger, BigInteger, BigInteger) ComputeSwapStep(
        BigInteger sqrtRatioCurrentX96,
        BigInteger sqrtRatioTargetX96,
        BigInteger liquidity,
        BigInteger amountRemaining,
        BigInteger feePips)
    {


        //Console.WriteLine("{0}, {1}, {2}, {3}, {4}",sqrtRatioCurrentX96, sqrtRatioTargetX96, liquidity, amountRemaining, feePips);

        var returnValues = new ReturnValues();

        bool zeroForOne = sqrtRatioCurrentX96 >= sqrtRatioTargetX96;
        bool exactIn = amountRemaining >= Constants.ZERO;

        if (exactIn)
        {
            BigInteger amountRemainingLessFee = BigInteger.Divide(
                BigInteger.Multiply(amountRemaining, BigInteger.Subtract(MAX_FEE, feePips)),
                MAX_FEE
            );
            returnValues.AmountIn = zeroForOne
                ? SqrtPriceMath.GetAmount0Delta(sqrtRatioTargetX96, sqrtRatioCurrentX96, liquidity, true)
                : SqrtPriceMath.GetAmount1Delta(sqrtRatioCurrentX96, sqrtRatioTargetX96, liquidity, true);


            if (amountRemainingLessFee >= returnValues.AmountIn)
            {
                returnValues.SqrtRatioNextX96 = sqrtRatioTargetX96;
            }
            else
            {
                returnValues.SqrtRatioNextX96 = SqrtPriceMath.GetNextSqrtPriceFromInput(
                    sqrtRatioCurrentX96,
                    liquidity,
                    amountRemainingLessFee,
                    zeroForOne
                );

                //Console.WriteLine("{0}, {1}", zeroForOne, returnValues.SqrtRatioNextX96);
            }



        }
        else
        {
            returnValues.AmountOut = zeroForOne
                ? SqrtPriceMath.GetAmount1Delta(sqrtRatioTargetX96, sqrtRatioCurrentX96, liquidity, false)
                : SqrtPriceMath.GetAmount0Delta(sqrtRatioCurrentX96, sqrtRatioTargetX96, liquidity, false);



            if ((BigInteger.Multiply(amountRemaining, Constants.NEGATIVE_ONE) >= returnValues.AmountOut))
            {
                returnValues.SqrtRatioNextX96 = sqrtRatioTargetX96;
            }
            else
            {
                returnValues.SqrtRatioNextX96 = SqrtPriceMath.GetNextSqrtPriceFromOutput(
                    sqrtRatioCurrentX96,
                    liquidity,
                    BigInteger.Multiply(amountRemaining, Constants.NEGATIVE_ONE),
                    zeroForOne
                );
            }
        }

        bool max = sqrtRatioTargetX96 == returnValues.SqrtRatioNextX96;

        if (zeroForOne)
        {
            returnValues.AmountIn = max && exactIn
                ? returnValues.AmountIn
                : SqrtPriceMath.GetAmount0Delta(returnValues.SqrtRatioNextX96, sqrtRatioCurrentX96, liquidity, true);
            returnValues.AmountOut = max && !exactIn
                ? returnValues.AmountOut
                : SqrtPriceMath.GetAmount1Delta(returnValues.SqrtRatioNextX96, sqrtRatioCurrentX96, liquidity, false);


            //Console.WriteLine("{0}, {1}, {2}", max, exactIn, returnValues.AmountOut);

        }
        else
        {
            returnValues.AmountIn = max && exactIn
                ? returnValues.AmountIn
                : SqrtPriceMath.GetAmount1Delta(sqrtRatioCurrentX96, returnValues.SqrtRatioNextX96, liquidity, true);
            returnValues.AmountOut = max && !exactIn
                ? returnValues.AmountOut
                : SqrtPriceMath.GetAmount0Delta(sqrtRatioCurrentX96, returnValues.SqrtRatioNextX96, liquidity, false);
        }

        if (!exactIn && returnValues.AmountOut > BigInteger.Multiply(amountRemaining, Constants.NEGATIVE_ONE))
        {
            returnValues.AmountOut = BigInteger.Multiply(amountRemaining, Constants.NEGATIVE_ONE);
        }

        if (exactIn && returnValues.SqrtRatioNextX96 != sqrtRatioTargetX96)
        {
            returnValues.FeeAmount = BigInteger.Subtract(amountRemaining, returnValues.AmountIn);
        }
        else
        {
            returnValues.FeeAmount = FullMath.MulDivRoundingUp(
                returnValues.AmountIn,
                feePips,
                BigInteger.Subtract(MAX_FEE, feePips)
            );
        }


        return (returnValues.SqrtRatioNextX96, returnValues.AmountIn, returnValues.AmountOut, returnValues.FeeAmount);
    }
}
