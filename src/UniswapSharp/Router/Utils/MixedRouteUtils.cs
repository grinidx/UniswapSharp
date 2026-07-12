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
                (route.Pools[left] is V2Pair && route.Pools[right] is not V2Pair))
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
            if (!TPool.InvolvesToken(pool, inputToken))
            {
                throw new ArgumentException("PATH");
            }
            inputToken = TPool.Token0(pool).Equals(inputToken) ? TPool.Token1(pool) : TPool.Token0(pool);
        }
        return inputToken;
    }
}
