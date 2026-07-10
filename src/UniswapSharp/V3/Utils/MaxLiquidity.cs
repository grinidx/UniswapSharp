using System.Numerics;

namespace UniswapSharp.V3.Utils;

public static class MaxLiquidity
{
    private static readonly BigInteger Q96 = BigInteger.Pow(2, 96);

    /// <summary>
    /// Returns an imprecise maximum amount of liquidity received for a given amount of token 0.
    /// This function is available to accommodate LiquidityAmounts#getLiquidityForAmount0 in the v3 periphery,
    /// which could be more precise by at least 32 bits by dividing by Q64 instead of Q96 in the intermediate step,
    /// and shifting the subtracted ratio left by 32 bits. This imprecise calculation will likely be replaced in a future
    /// v3 router contract.
    /// </summary>
    /// <param name="sqrtRatioAX96">The price at the lower boundary</param>
    /// <param name="sqrtRatioBX96">The price at the upper boundary</param>
    /// <param name="amount0">The token0 amount</param>
    /// <returns>liquidity for amount0, imprecise</returns>
    public static BigInteger MaxLiquidityForAmount0Imprecise(BigInteger sqrtRatioAX96, BigInteger sqrtRatioBX96, BigInteger amount0)
    {
        if (sqrtRatioAX96 > sqrtRatioBX96)
        {
            (sqrtRatioAX96, sqrtRatioBX96) = (sqrtRatioBX96, sqrtRatioAX96);
        }
        BigInteger intermediate = BigInteger.Divide(BigInteger.Multiply(sqrtRatioAX96, sqrtRatioBX96), Q96);
        return BigInteger.Divide(BigInteger.Multiply(amount0, intermediate), BigInteger.Subtract(sqrtRatioBX96, sqrtRatioAX96));
    }

    /// <summary>
    /// Returns a precise maximum amount of liquidity received for a given amount of token 0 by dividing by Q64 instead of Q96 in the intermediate step,
    /// and shifting the subtracted ratio left by 32 bits.
    /// </summary>
    /// <param name="sqrtRatioAX96">The price at the lower boundary</param>
    /// <param name="sqrtRatioBX96">The price at the upper boundary</param>
    /// <param name="amount0">The token0 amount</param>
    /// <returns>liquidity for amount0, precise</returns>
    public static BigInteger MaxLiquidityForAmount0Precise(BigInteger sqrtRatioAX96, BigInteger sqrtRatioBX96, BigInteger amount0)
    {
        if (sqrtRatioAX96 > sqrtRatioBX96)
        {
            (sqrtRatioAX96, sqrtRatioBX96) = (sqrtRatioBX96, sqrtRatioAX96);
        }

        BigInteger numerator = BigInteger.Multiply(BigInteger.Multiply(amount0, sqrtRatioAX96), sqrtRatioBX96);
        BigInteger denominator = BigInteger.Multiply(Q96, BigInteger.Subtract(sqrtRatioBX96, sqrtRatioAX96));

        return BigInteger.Divide(numerator, denominator);
    }

    /// <summary>
    /// Computes the maximum amount of liquidity received for a given amount of token1
    /// </summary>
    /// <param name="sqrtRatioAX96">The price at the lower tick boundary</param>
    /// <param name="sqrtRatioBX96">The price at the upper tick boundary</param>
    /// <param name="amount1">The token1 amount</param>
    /// <returns>liquidity for amount1</returns>
    public static BigInteger MaxLiquidityForAmount1(BigInteger sqrtRatioAX96, BigInteger sqrtRatioBX96, BigInteger amount1)
    {
        if (sqrtRatioAX96 > sqrtRatioBX96)
        {
            (sqrtRatioAX96, sqrtRatioBX96) = (sqrtRatioBX96, sqrtRatioAX96);
        }
        return BigInteger.Divide(BigInteger.Multiply(amount1, Q96), BigInteger.Subtract(sqrtRatioBX96, sqrtRatioAX96));
    }

    /// <summary>
    /// Computes the maximum amount of liquidity received for a given amount of token0, token1,
    /// and the prices at the tick boundaries.
    /// </summary>
    /// <param name="sqrtRatioCurrentX96">the current price</param>
    /// <param name="sqrtRatioAX96">price at lower boundary</param>
    /// <param name="sqrtRatioBX96">price at upper boundary</param>
    /// <param name="amount0">token0 amount</param>
    /// <param name="amount1">token1 amount</param>
    /// <param name="useFullPrecision">if false, liquidity will be maximized according to what the router can calculate,
    /// not what core can theoretically support</param>
    public static BigInteger MaxLiquidityForAmounts(
        BigInteger sqrtRatioCurrentX96,
        BigInteger sqrtRatioAX96,
        BigInteger sqrtRatioBX96,
        BigInteger amount0,
        BigInteger amount1,
        bool useFullPrecision)
    {
        if (sqrtRatioAX96 > sqrtRatioBX96)
        {
            (sqrtRatioAX96, sqrtRatioBX96) = (sqrtRatioBX96, sqrtRatioAX96);
        }

        Func<BigInteger, BigInteger, BigInteger, BigInteger> maxLiquidityForAmount0 = useFullPrecision ? MaxLiquidityForAmount0Precise : MaxLiquidityForAmount0Imprecise;

        if (sqrtRatioCurrentX96 <= sqrtRatioAX96)
        {
            return maxLiquidityForAmount0(sqrtRatioAX96, sqrtRatioBX96, amount0);
        }
        else if (sqrtRatioCurrentX96 < sqrtRatioBX96)
        {
            BigInteger liquidity0 = maxLiquidityForAmount0(sqrtRatioCurrentX96, sqrtRatioBX96, amount0);
            BigInteger liquidity1 = MaxLiquidityForAmount1(sqrtRatioAX96, sqrtRatioCurrentX96, amount1);
            return liquidity0 < liquidity1 ? liquidity0 : liquidity1;
        }
        else
        {
            return MaxLiquidityForAmount1(sqrtRatioAX96, sqrtRatioBX96, amount1);
        }
    }
}