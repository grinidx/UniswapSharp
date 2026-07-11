using UniswapSharp.Core.Entities;
using UniswapSharp.V4.Entities;

namespace UniswapSharp.V4.Utils;

/// <summary>A single hop of a V4 path (a struct entry, unlike V3's tightly-packed bytes path).</summary>
public record PathKey(string IntermediateCurrency, int Fee, int TickSpacing, string Hooks, string HookData);

/// <summary>Ported from v4-sdk/src/utils/encodeRouteToPath.ts.</summary>
public static class EncodeRouteToPath
{
    public static List<PathKey> Encode<TInput, TOutput>(Route<TInput, TOutput> route, bool exactOutput = false)
        where TInput : BaseCurrency where TOutput : BaseCurrency
    {
        // Copy so we don't tamper with the route's pool array.
        var pools = new List<Pool>(route.Pools);
        if (exactOutput)
        {
            pools.Reverse();
        }

        BaseCurrency startingCurrency = exactOutput ? route.PathOutput : route.PathInput;
        var pathKeys = new List<PathKey>();

        foreach (var pool in pools)
        {
            BaseCurrency nextCurrency = startingCurrency.Equals(pool.Currency0) ? pool.Currency1 : pool.Currency0;
            pathKeys.Add(new PathKey(CurrencyMap.ToAddress(nextCurrency), pool.Fee, pool.TickSpacing, pool.Hooks, "0x"));
            startingCurrency = nextCurrency;
        }

        if (exactOutput)
        {
            pathKeys.Reverse();
        }
        return pathKeys;
    }
}
