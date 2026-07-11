using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.V4.Entities;

namespace UniswapSharp.V4.Utils;

/// <summary>Ported from v4-sdk/src/utils/pathCurrency.ts.</summary>
public static class PathCurrency
{
    public static CurrencyAmount<BaseCurrency> AmountWithPathCurrency(CurrencyAmount<BaseCurrency> amount, Pool pool)
    {
        return CurrencyAmount<BaseCurrency>.FromFractionalAmount(GetPathCurrency(amount.Currency, pool), amount.Numerator, amount.Denominator);
    }

    public static BaseCurrency GetPathCurrency(BaseCurrency currency, Pool pool)
    {
        if (pool.InvolvesCurrency(currency))
        {
            return currency;
        }
        if (pool.InvolvesCurrency(currency.Wrapped()))
        {
            return currency.Wrapped();
        }
        if (pool.Currency0.Wrapped().Equals(currency))
        {
            return pool.Currency0;
        }
        if (pool.Currency1.Wrapped().Equals(currency))
        {
            return pool.Currency1;
        }
        throw new InvalidOperationException($"Expected currency {currency.Symbol} to be either {pool.Currency0.Symbol} or {pool.Currency1.Symbol}");
    }
}
