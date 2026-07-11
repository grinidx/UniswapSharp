using System.Numerics;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.V3.Utils;

namespace UniswapSharp.V4.Utils;

/// <summary>
/// Almost identical to the v3-sdk price/tick conversion, except it accepts a <see cref="BaseCurrency"/>
/// (Currency) instead of a Token and uses the native-aware currency ordering.
/// Ported from v4-sdk/src/utils/priceTickConversions.ts.
/// </summary>
public static class PriceTick
{
    public static Price<BaseCurrency, BaseCurrency> TickToPrice(BaseCurrency baseCurrency, BaseCurrency quoteCurrency, int tick)
    {
        BigInteger sqrtRatioX96 = TickMath.GetSqrtRatioAtTick(tick);
        BigInteger ratioX192 = sqrtRatioX96 * sqrtRatioX96;

        return CurrencyOrder.SortsBefore(baseCurrency, quoteCurrency)
            ? new Price<BaseCurrency, BaseCurrency>(baseCurrency, quoteCurrency, Constants.Q192, ratioX192)
            : new Price<BaseCurrency, BaseCurrency>(baseCurrency, quoteCurrency, ratioX192, Constants.Q192);
    }

    public static int PriceToClosestTick(Price<BaseCurrency, BaseCurrency> price)
    {
        bool sorted = CurrencyOrder.SortsBefore(price.BaseCurrency, price.QuoteCurrency);

        BigInteger sqrtRatioX96 = sorted
            ? EncodeSqrtRatioX96.Encode(price.Numerator, price.Denominator)
            : EncodeSqrtRatioX96.Encode(price.Denominator, price.Numerator);

        int tick = TickMath.GetTickAtSqrtRatio(sqrtRatioX96);
        var nextTickPrice = TickToPrice(price.BaseCurrency, price.QuoteCurrency, tick + 1);

        if (sorted)
        {
            if (!price.LessThan(nextTickPrice))
            {
                tick++;
            }
        }
        else
        {
            if (!price.GreaterThan(nextTickPrice))
            {
                tick++;
            }
        }

        return tick;
    }
}
