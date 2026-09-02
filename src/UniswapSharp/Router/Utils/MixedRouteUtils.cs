using UniswapSharp.Core.Entities;
using UniswapSharp.Router.Entities.MixedRoute;
using V2Pair = UniswapSharp.V2.Entities.Pair;
using V3Pool = UniswapSharp.V3.Entities.Pool;
using V4Pool = UniswapSharp.V4.Entities.Pool;

namespace UniswapSharp.Router.Utils;

/// <summary>
/// Port of router-sdk <c>utils/index.ts</c>: helpers for partitioning a mixed route into
/// consecutive same-protocol sections and computing the output of a run of pools.
/// </summary>
public static class MixedRouteUtils
{
    /// <summary>
    /// Same token0/token1 membership test as <c>InvolvesToken</c>, but accepting any currency.
    /// Upstream needs this because <c>Pair.involvesToken</c>/<c>V3Pool.involvesToken</c> only take a
    /// <c>Token</c>, so calling them with a bare currency (native ETH off <c>route.path</c>) does not
    /// typecheck.
    /// </summary>
    private static bool PoolInvolvesCurrency(object pool, BaseCurrency currency) =>
        TPool.Token0(pool).Equals(currency) || TPool.Token1(pool).Equals(currency);

    /// <summary>
    /// Returns each consecutive section of Pools or Pairs in a MixedRoute, in route order.
    /// </summary>
    public static List<List<object>> PartitionMixedRouteByProtocol<TInput, TOutput>(MixedRouteSDK<TInput, TOutput> route)
        where TInput : BaseCurrency where TOutput : BaseCurrency
    {
        var acc = new List<List<object>>();

        int left = 0;
        int right = 0;
        while (right < route.Pools.Count)
        {
            if ((route.Pools[left] is V4Pool && route.Pools[right] is not V4Pool) ||
                (route.Pools[left] is V3Pool && route.Pools[right] is not V3Pool) ||
                (route.Pools[left] is V2Pair && route.Pools[right] is not V2Pair) ||
                // a native/wrapped boundary (e.g. a native-ETH v4 pool followed by a WETH v4 pool)
                // needs a wrap/unwrap between sections, so it ends the section even within a single protocol
                !PoolInvolvesCurrency(route.Pools[right], route.Path[right]))
            {
                acc.Add(route.Pools.GetRange(left, right - left));
                left = right;
            }

            // seek forward with right pointer
            right++;
            if (right == route.Pools.Count)
            {
                // we reached the end, take the rest
                acc.Add(route.Pools.GetRange(left, right - left));
            }
        }

        return acc;
    }

    /// <summary>
    /// Returns the output token of the last pool in the array, walking the pools from the given input.
    /// </summary>
    public static BaseCurrency GetOutputOfPools(IReadOnlyList<object> pools, BaseCurrency firstInputToken)
    {
        BaseCurrency inputToken = firstInputToken;
        foreach (var pool in pools)
        {
            var token0 = TPool.Token0(pool);
            var token1 = TPool.Token1(pool);

            // Exact matches take priority so genuine ETH/WETH pools resolve to the correct side;
            // the wrapped comparisons then bridge native/wrapped boundaries the same way
            // MixedRouteSDK does.
            if (token0.Equals(inputToken)) { inputToken = token1; }
            else if (token1.Equals(inputToken)) { inputToken = token0; }
            else if (token0.Wrapped().Equals(inputToken.Wrapped())) { inputToken = token1; }
            else if (token1.Wrapped().Equals(inputToken.Wrapped())) { inputToken = token0; }
            else { throw new ArgumentException("PATH"); }
        }
        return inputToken;
    }
}
