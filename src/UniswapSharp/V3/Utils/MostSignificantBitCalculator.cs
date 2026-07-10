using System.Numerics;

namespace UniswapSharp.V3.Utils;

public static class MostSignificantBitCalculator
{
    private static readonly BigInteger Two = new BigInteger(2);
    private static readonly BigInteger Zero = BigInteger.Zero;
    private static readonly BigInteger MaxUint256 = BigInteger.Pow(2, 256) - 1;

    private static readonly (int Power, BigInteger Min)[] PowersOf2 = new[]
    {
        128, 64, 32, 16, 8, 4, 2, 1
    }.Select(pow => (pow, BigInteger.Pow(Two, pow))).ToArray();

    public static int MostSignificantBit(BigInteger x)
    {
        if (x <= Zero)
        {
            throw new ArgumentException("Input must be greater than zero.", nameof(x));
        }

        if (x > MaxUint256)
        {
            throw new ArgumentException("Input must be less than or equal to MaxUint256.", nameof(x));
        }

        int msb = 0;
        foreach (var (power, min) in PowersOf2)
        {
            if (x >= min)
            {
                x >>= power;
                msb += power;
            }
        }

        return msb;
    }
}
