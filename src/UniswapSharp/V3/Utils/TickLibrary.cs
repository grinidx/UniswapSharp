using System.Numerics;

namespace UniswapSharp.V3.Utils;

public abstract class TickLibrary
{
    private TickLibrary() { }

    public static (BigInteger, BigInteger) GetFeeGrowthInside(
        FeeGrowthOutside feeGrowthOutsideLower,
        FeeGrowthOutside feeGrowthOutsideUpper,
        int tickLower,
        int tickUpper,
        int tickCurrent,
        BigInteger feeGrowthGlobal0X128,
        BigInteger feeGrowthGlobal1X128)
    {
        BigInteger feeGrowthBelow0X128;
        BigInteger feeGrowthBelow1X128;
        if (tickCurrent >= tickLower)
        {
            feeGrowthBelow0X128 = feeGrowthOutsideLower.FeeGrowthOutside0X128;
            feeGrowthBelow1X128 = feeGrowthOutsideLower.FeeGrowthOutside1X128;
        }
        else
        {
            feeGrowthBelow0X128 = SubIn256(feeGrowthGlobal0X128, feeGrowthOutsideLower.FeeGrowthOutside0X128);
            feeGrowthBelow1X128 = SubIn256(feeGrowthGlobal1X128, feeGrowthOutsideLower.FeeGrowthOutside1X128);
        }

        BigInteger feeGrowthAbove0X128;
        BigInteger feeGrowthAbove1X128;
        if (tickCurrent < tickUpper)
        {
            feeGrowthAbove0X128 = feeGrowthOutsideUpper.FeeGrowthOutside0X128;
            feeGrowthAbove1X128 = feeGrowthOutsideUpper.FeeGrowthOutside1X128;
        }
        else
        {
            feeGrowthAbove0X128 = SubIn256(feeGrowthGlobal0X128, feeGrowthOutsideUpper.FeeGrowthOutside0X128);
            feeGrowthAbove1X128 = SubIn256(feeGrowthGlobal1X128, feeGrowthOutsideUpper.FeeGrowthOutside1X128);
        }

        return (
            SubIn256(SubIn256(feeGrowthGlobal0X128, feeGrowthBelow0X128), feeGrowthAbove0X128),
            SubIn256(SubIn256(feeGrowthGlobal1X128, feeGrowthBelow1X128), feeGrowthAbove1X128)
        );
    }

    public interface FeeGrowthOutside
    {
        BigInteger FeeGrowthOutside0X128 { get; set; }
        BigInteger FeeGrowthOutside1X128 { get; set; }
    }

    public static BigInteger SubIn256(BigInteger x, BigInteger y)
    {
        BigInteger difference = x - y;

        if (difference < Constants.ZERO)
        {
            return Q256 + difference;
        }
        else
        {
            return difference;
        }
    }

    static BigInteger Q256 = BigInteger.Pow(2, 256);
}