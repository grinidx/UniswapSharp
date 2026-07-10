using System.Numerics;

namespace UniswapSharp.V3.Utils;

public static class EncodeSqrtRatioX96
{
    /// <summary>
    /// Returns the sqrt ratio as a Q64.96 corresponding to a given ratio of amount1 and amount0
    /// </summary>
    /// <param name="amount1">The numerator amount i.e., the amount of token1</param>
    /// <param name="amount0">The denominator amount i.e., the amount of token0</param>
    /// <returns>The sqrt ratio</returns>
    public static BigInteger Encode(BigInteger amount1, BigInteger amount0)
    {
        BigInteger numerator = amount1 << 192;
        BigInteger denominator = amount0;
        BigInteger ratioX192 = BigInteger.Divide(numerator, denominator);
        return Sqrt(ratioX192);
    }

    private static BigInteger Sqrt(BigInteger n)
    {
        if (n == 0) return 0;
        if (n > 0)
        {
            int bitLength = Convert.ToInt32(Math.Ceiling(BigInteger.Log(n, 2)));
            BigInteger root = BigInteger.One << (bitLength / 2);

            while (!IsSqrt(n, root))
            {
                root += n / root;
                root /= 2;
            }

            return root;
        }

        throw new ArithmeticException("NaN");
    }

    private static bool IsSqrt(BigInteger n, BigInteger root)
    {
        BigInteger lowerBound = root * root;
        BigInteger upperBound = (root + 1) * (root + 1);

        return n >= lowerBound && n < upperBound;
    }
}