using System.Numerics;

namespace UniswapSharp.V3.Utils;

public static class LiquidityMath
{
    private static readonly BigInteger NEGATIVE_ONE = BigInteger.MinusOne;
    private static readonly BigInteger ZERO = BigInteger.Zero;

    public static BigInteger AddDelta(BigInteger x, BigInteger y)
    {
        if (y < ZERO)
        {
            return x - (y * NEGATIVE_ONE);
        }
        else
        {
            return x + y;
        }
    }
}
