using System.Numerics;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;

namespace UniswapSharp.V3.Utils;

public static class PriceTick
{
    private static readonly BigInteger Q192 = BigInteger.Pow(2, 192);

    public static Price<Token, Token> TickToPrice(Token baseToken, Token quoteToken, int tick)
    {
        BigInteger sqrtRatioX96 = TickMath.GetSqrtRatioAtTick(tick);
        BigInteger ratioX192 = sqrtRatioX96 * sqrtRatioX96;

        return baseToken.SortsBefore(quoteToken)
            ? new Price<Token, Token>(baseToken, quoteToken, Q192, ratioX192)
            : new Price<Token, Token>(baseToken, quoteToken, ratioX192, Q192);
    }

    public static int PriceToClosestTick(Price<Token, Token> price)
    {
        bool sorted = price.BaseCurrency.SortsBefore(price.QuoteCurrency);

        BigInteger sqrtRatioX96 = sorted
            ? EncodeSqrtRatioX96(price.Numerator, price.Denominator)
            : EncodeSqrtRatioX96(price.Denominator, price.Numerator);

        int tick = TickMath.GetTickAtSqrtRatio(sqrtRatioX96);
        Price<Token, Token> nextTickPrice = TickToPrice(price.BaseCurrency, price.QuoteCurrency, tick + 1);

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

    private static BigInteger EncodeSqrtRatioX96(BigInteger numerator, BigInteger denominator)
    {
        // Implementation of encodeSqrtRatioX96 function
        // This is a placeholder and should be replaced with the actual implementation
        throw new NotImplementedException();
    }
}
