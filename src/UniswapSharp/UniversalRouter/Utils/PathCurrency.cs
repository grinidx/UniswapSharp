using UniswapSharp.Core.Entities;
using UniswapSharp.Router.Utils;
using V4Pool = UniswapSharp.V4.Entities.Pool;

namespace UniswapSharp.UniversalRouter.Utils;

/// <summary>Port of universal-router-sdk <c>utils/pathCurrency.ts</c>.</summary>
public static class PathCurrency
{
    /// <summary>
    /// Resolves the currency to use along a route through <paramref name="pool"/>: the currency itself,
    /// its wrapped form, or (for V4 pools) the native/wrapped variant the pool actually holds.
    /// </summary>
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
        if (pool is V4Pool v4a && v4a.Currency0.Wrapped().Equals(currency))
        {
            return v4a.Currency0;
        }
        if (pool is V4Pool v4b && v4b.Currency1.Wrapped().Equals(currency))
        {
            return v4b.Currency1;
        }

        throw new InvalidOperationException(
            $"Expected currency {currency.Symbol} to be either {TPool.Token0(pool).Symbol} or {TPool.Token1(pool).Symbol}");
    }
}
