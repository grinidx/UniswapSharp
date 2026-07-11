using System.Numerics;

namespace UniswapSharp.Core.Utils;

public static class MathUtils
{
    // Number.MAX_SAFE_INTEGER (2^53 - 1). Above this a double loses integer precision, so the
    // Math.Sqrt fast path can only be used strictly below it (upstream uses Newton's method above).
    public static readonly BigInteger MAX_SAFE_INTEGER = new BigInteger(9007199254740991L);

    private static readonly BigInteger ZERO = BigInteger.Zero;
    private static readonly BigInteger ONE = BigInteger.One;
    private static readonly BigInteger TWO = new BigInteger(2);

    /// <summary>
    /// Computes floor(sqrt(value))
    /// </summary>
    /// <param name="value">The value for which to compute the square root, rounded down</param>
    /// <returns>The square root of the input value, rounded down</returns>
    public static BigInteger Sqrt(this BigInteger value)
    {
        if (value < ZERO)
        {
            throw new ArgumentException("Input value cannot be negative", nameof(value));
        }

        // rely on built in sqrt if possible
        if (value < MAX_SAFE_INTEGER)
        {
            return new BigInteger(Math.Floor(Math.Sqrt((double)value)));
        }

        BigInteger z = value;
        BigInteger x = (value / TWO) + ONE;
        while (x < z)
        {
            z = x;
            x = (value / x + x) / TWO;
        }
        return z;
    }
}
