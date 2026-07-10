using System.Numerics;
// ReSharper disable InconsistentNaming

namespace UniswapSharp.V3.Utils;

public static class FullMath
{
    private static readonly BigInteger ONE = BigInteger.One;
    private static readonly BigInteger ZERO = BigInteger.Zero;

    public static BigInteger MulDivRoundingUp(BigInteger a, BigInteger b, BigInteger denominator)
    {
        BigInteger product = BigInteger.Multiply(a, b);
        BigInteger result = BigInteger.Divide(product, denominator);
        if (BigInteger.Remainder(product, denominator) != ZERO)
        {
            result = BigInteger.Add(result, ONE);
        }
        return result;
    }
}
