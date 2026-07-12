using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using V4Pool = UniswapSharp.V4.Entities.Pool;

namespace UniswapSharp.Router.Utils;

/// <summary>
/// Port of router-sdk <c>utils/pathCurrency.ts</c>.
/// </summary>
public static class PathCurrency
{
    public static CurrencyAmount<BaseCurrency> AmountWithPathCurrency(CurrencyAmount<BaseCurrency> amount, object pool)
    {
        return CurrencyAmount<BaseCurrency>.FromFractionalAmount(
            GetPathCurrency(amount.Currency, pool),
            amount.Numerator,
            amount.Denominator);
    }

    public static BaseCurrency GetPathCurrency(BaseCurrency currency, object pool)
    {
        // return currency if the currency matches a currency of the pool
        if (TPool.InvolvesToken(pool, currency))
        {
            return currency;
        }

        // return currency.wrapped if pool involves wrapped currency
        if (TPool.InvolvesToken(pool, currency.Wrapped()))
        {
            return currency.Wrapped();
        }

        // return native currency if pool involves native version of wrapped currency (only applies to V4)
        if (pool is V4Pool)
        {
            if (TPool.Token0(pool).Wrapped().Equals(currency))
            {
                return TPool.Token0(pool);
            }
            if (TPool.Token1(pool).Wrapped().Equals(currency))
            {
                return TPool.Token1(pool);
            }
        }
        else
        {
            // otherwise the token is invalid
            throw new ArgumentException(
                $"Expected currency {currency.Symbol} to be either {TPool.Token0(pool).Symbol} or {TPool.Token1(pool).Symbol}");
        }

        return currency;
    }
}
