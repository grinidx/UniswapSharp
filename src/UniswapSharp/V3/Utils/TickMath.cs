using System.Globalization;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;

namespace UniswapSharp.V3.Utils;

public static class TickMath
{
    public const int MIN_TICK = -887272;
    public static int MAX_TICK => -MIN_TICK;

    public static readonly BigInteger MIN_SQRT_RATIO = BigInteger.Parse("4295128739");
    public static readonly BigInteger MAX_SQRT_RATIO = BigInteger.Parse("1461446703485210103287273052203988822378723970342");

    private static readonly BigInteger Q32 = BigInteger.Pow(2, 32);
    private static readonly BigInteger ONE = BigInteger.One;
    private static readonly BigInteger ZERO = BigInteger.Zero;
    internal static readonly BigInteger MaxUint256 = BigInteger.Pow(2, 256) - 1;

    private static BigInteger MulShift(BigInteger val, string mulBy)
    {
        return (val * mulBy.HexToBigInteger(false)) >> 128;
    }

    public static BigInteger GetSqrtRatioAtTick(int tick)
    {
        if (tick < MIN_TICK || tick > MAX_TICK || !int.TryParse(tick.ToString(), out _))
            throw new ArgumentException("Invalid tick", nameof(tick));

        int absTick = Math.Abs(tick);

        BigInteger ratio = (absTick & 0x1) != 0
            ? "0xfffcb933bd6fad37aa2d162d1a594001".HexToBigInteger(false)
            : "0x100000000000000000000000000000000".HexToBigInteger(false);

        if ((absTick & 0x2) != 0) ratio = MulShift(ratio, "0xfff97272373d413259a46990580e213a");
        if ((absTick & 0x4) != 0) ratio = MulShift(ratio, "0xfff2e50f5f656932ef12357cf3c7fdcc");
        if ((absTick & 0x8) != 0) ratio = MulShift(ratio, "0xffe5caca7e10e4e61c3624eaa0941cd0");
        if ((absTick & 0x10) != 0) ratio = MulShift(ratio, "0xffcb9843d60f6159c9db58835c926644");
        if ((absTick & 0x20) != 0) ratio = MulShift(ratio, "0xff973b41fa98c081472e6896dfb254c0");
        if ((absTick & 0x40) != 0) ratio = MulShift(ratio, "0xff2ea16466c96a3843ec78b326b52861");
        if ((absTick & 0x80) != 0) ratio = MulShift(ratio, "0xfe5dee046a99a2a811c461f1969c3053");
        if ((absTick & 0x100) != 0) ratio = MulShift(ratio, "0xfcbe86c7900a88aedcffc83b479aa3a4");
        if ((absTick & 0x200) != 0) ratio = MulShift(ratio, "0xf987a7253ac413176f2b074cf7815e54");
        if ((absTick & 0x400) != 0) ratio = MulShift(ratio, "0xf3392b0822b70005940c7a398e4b70f3");
        if ((absTick & 0x800) != 0) ratio = MulShift(ratio, "0xe7159475a2c29b7443b29c7fa6e889d9");
        if ((absTick & 0x1000) != 0) ratio = MulShift(ratio, "0xd097f3bdfd2022b8845ad8f792aa5825");
        if ((absTick & 0x2000) != 0) ratio = MulShift(ratio, "0xa9f746462d870fdf8a65dc1f90e061e5");
        if ((absTick & 0x4000) != 0) ratio = MulShift(ratio, "0x70d869a156d2a1b890bb3df62baf32f7");
        if ((absTick & 0x8000) != 0) ratio = MulShift(ratio, "0x31be135f97d08fd981231505542fcfa6");
        if ((absTick & 0x10000) != 0) ratio = MulShift(ratio, "0x9aa508b5b7a84e1c677de54f3e99bc9");
        if ((absTick & 0x20000) != 0) ratio = MulShift(ratio, "0x5d6af8dedb81196699c329225ee604");
        if ((absTick & 0x40000) != 0) ratio = MulShift(ratio, "0x2216e584f5fa1ea926041bedfe98");
        if ((absTick & 0x80000) != 0) ratio = MulShift(ratio, "0x48a170391f7dc42444e8fa2");

        if (tick > 0) ratio = MaxUint256 / ratio;

        return (ratio % Q32) > ZERO ? (ratio / Q32) + ONE : (ratio / Q32);
    }

    public static int GetTickAtSqrtRatio(BigInteger sqrtRatioX96)
    {
        if (sqrtRatioX96 < MIN_SQRT_RATIO || sqrtRatioX96 >= MAX_SQRT_RATIO)
            throw new ArgumentException("Invalid sqrtRatioX96", nameof(sqrtRatioX96));

        BigInteger sqrtRatioX128 = sqrtRatioX96 << 32;

        int msb = MostSignificantBit(sqrtRatioX128);

        BigInteger r = msb >= 128 ? sqrtRatioX128 >> (msb - 127) : sqrtRatioX128 << (127 - msb);

        BigInteger log_2 = ((BigInteger)msb - 128) << 64;

        for (int i = 0; i < 14; i++)
        {
            r = (r * r) >> 127;
            BigInteger f = r >> 128;
            log_2 |= f << (63 - i);
            r >>= (int)f;
        }

        BigInteger log_sqrt10001 = log_2 * BigInteger.Parse("255738958999603826347141");

        int tickLow = (int)((log_sqrt10001 - BigInteger.Parse("3402992956809132418596140100660247210")) >> 128);
        int tickHigh = (int)((log_sqrt10001 + BigInteger.Parse("291339464771989622907027621153398088495")) >> 128);

        return tickLow == tickHigh
            ? tickLow
            : GetSqrtRatioAtTick(tickHigh) <= sqrtRatioX96
                ? tickHigh
                : tickLow;
    }

    private static int MostSignificantBit(BigInteger x)
    {
        int msb = 0;
        while (x > 0)
        {
            x >>= 1;
            msb++;
        }
        return msb - 1;
    }
}