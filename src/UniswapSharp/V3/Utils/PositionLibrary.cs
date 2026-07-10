using System.Numerics;

namespace UniswapSharp.V3.Utils;

public static class PositionLibrary
{
    private static readonly BigInteger Q128 = BigInteger.Pow(2, 128);

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

    // Assuming SubIn256 is defined elsewhere in your C# codebase
    private static BigInteger SubIn256(BigInteger a, BigInteger b)
    {
        // Implementation of SubIn256 should be provided
        throw new NotImplementedException("SubIn256 method needs to be implemented");
    }
}