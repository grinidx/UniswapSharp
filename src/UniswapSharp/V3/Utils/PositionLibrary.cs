using System.Numerics;

namespace UniswapSharp.V3.Utils;

public static class PositionLibrary
{
    private static readonly BigInteger Q128 = BigInteger.Pow(2, 128);
    private static readonly BigInteger Q256 = BigInteger.Pow(2, 256);

    // Replicates the portions of Position#update required to compute unaccounted fees
    public static (BigInteger, BigInteger) GetTokensOwed(
        BigInteger feeGrowthInside0LastX128,
        BigInteger feeGrowthInside1LastX128,
        BigInteger liquidity,
        BigInteger feeGrowthInside0X128,
        BigInteger feeGrowthInside1X128)
    {
        BigInteger tokensOwed0 = BigInteger.Divide(
            BigInteger.Multiply(SubIn256(feeGrowthInside0X128, feeGrowthInside0LastX128), liquidity),
            Q128
        );

        BigInteger tokensOwed1 = BigInteger.Divide(
            BigInteger.Multiply(SubIn256(feeGrowthInside1X128, feeGrowthInside1LastX128), liquidity),
            Q128
        );

        return (tokensOwed0, tokensOwed1);
    }

    // Ported from sdks/v3-sdk/src/utils/tickLibrary.ts (subIn256): 256-bit
    // modular subtraction — if the difference underflows, wrap by +2^256.
    private static BigInteger SubIn256(BigInteger a, BigInteger b)
    {
        var difference = a - b;
        return difference < 0 ? Q256 + difference : difference;
    }
}
