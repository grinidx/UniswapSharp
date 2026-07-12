using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.Router.Entities.MixedRoute;
using UniswapSharp.Router.Utils;

namespace UniswapSharp.Router.Entities;

/// <summary>Free helpers exported from router-sdk <c>entities/route.ts</c>.</summary>
public static class RouteHelpers
{
    /// <summary>
    /// Get the path token for a V2 / V3 route. The currency could be native so we compare against
    /// the wrapped version as those protocols don't support native ETH in the path.
    /// </summary>
    public static Token GetPathToken(BaseCurrency currency, object pool)
    {
        var token0 = (Token)TPool.Token0(pool);
        var token1 = (Token)TPool.Token1(pool);
        if (token0.Wrapped().Equals(currency.Wrapped()))
        {
            return token0;
        }
        if (token1.Wrapped().Equals(currency.Wrapped()))
        {
            return token1;
        }
        throw new ArgumentException($"Expected token {currency.Symbol} to be either {token0.Symbol} or {token1.Symbol}");
    }
}

/// <summary>Port of router-sdk <c>IRoute</c>: a protocol-tagged route over a pool union.</summary>
public interface IRoute<TInput, TOutput>
    where TInput : BaseCurrency
    where TOutput : BaseCurrency
{
    Protocol Protocol { get; }

    /// <summary>Array of pools (V3/V4) or pairs (V2), as the <c>TPool</c> union.</summary>
    List<object> Pools { get; }

    List<BaseCurrency> Path { get; }
    Price<TInput, TOutput> MidPrice { get; }
    TInput Input { get; }
    TOutput Output { get; }
    BaseCurrency PathInput { get; }
    BaseCurrency PathOutput { get; }
    int ChainId { get; }
}

/// <summary>V2 route wrapper. Port of router-sdk <c>RouteV2</c>.</summary>
public class RouteV2<TInput, TOutput> : IRoute<TInput, TOutput>
    where TInput : BaseCurrency
    where TOutput : BaseCurrency
{
    public Protocol Protocol => Protocol.V2;
    public V2.Entities.Route<TInput, TOutput> V2Route { get; }
    public List<object> Pools { get; }
    public List<BaseCurrency> Path { get; }
    public BaseCurrency PathInput { get; }
    public BaseCurrency PathOutput { get; }
    public TInput Input => V2Route.Input;
    public TOutput Output => V2Route.Output;
    public Price<TInput, TOutput> MidPrice => V2Route.MidPrice;
    public int ChainId => V2Route.ChainId;

    public RouteV2(V2.Entities.Route<TInput, TOutput> v2Route)
    {
        V2Route = v2Route;
        Pools = v2Route.Pairs.Cast<object>().ToList();
        Path = v2Route.Path.Cast<BaseCurrency>().ToList();
        PathInput = RouteHelpers.GetPathToken(v2Route.Input, v2Route.Pairs[0]);
        PathOutput = RouteHelpers.GetPathToken(v2Route.Output, v2Route.Pairs[^1]);
    }
}

/// <summary>V3 route wrapper. Port of router-sdk <c>RouteV3</c>.</summary>
public class RouteV3<TInput, TOutput> : IRoute<TInput, TOutput>
    where TInput : BaseCurrency
    where TOutput : BaseCurrency
{
    public Protocol Protocol => Protocol.V3;
    public V3.Entities.Route<TInput, TOutput> V3Route { get; }
    public List<object> Pools { get; }
    public List<BaseCurrency> Path { get; }
    public BaseCurrency PathInput { get; }
    public BaseCurrency PathOutput { get; }
    public TInput Input => V3Route.Input;
    public TOutput Output => V3Route.Output;
    public Price<TInput, TOutput> MidPrice => V3Route.MidPrice;
    public int ChainId => V3Route.ChainId;

    public RouteV3(V3.Entities.Route<TInput, TOutput> v3Route)
    {
        V3Route = v3Route;
        Pools = v3Route.Pools.Cast<object>().ToList();
        Path = v3Route.TokenPath.Cast<BaseCurrency>().ToList();
        PathInput = RouteHelpers.GetPathToken(v3Route.Input, v3Route.Pools[0]);
        PathOutput = RouteHelpers.GetPathToken(v3Route.Output, v3Route.Pools[^1]);
    }
}

/// <summary>V4 route wrapper. Port of router-sdk <c>RouteV4</c>.</summary>
public class RouteV4<TInput, TOutput> : IRoute<TInput, TOutput>
    where TInput : BaseCurrency
    where TOutput : BaseCurrency
{
    public Protocol Protocol => Protocol.V4;
    public V4.Entities.Route<TInput, TOutput> V4Route { get; }
    public List<object> Pools { get; }
    public List<BaseCurrency> Path { get; }
    public BaseCurrency PathInput => V4Route.PathInput;
    public BaseCurrency PathOutput => V4Route.PathOutput;
    public TInput Input => V4Route.Input;
    public TOutput Output => V4Route.Output;
    public Price<TInput, TOutput> MidPrice => V4Route.MidPrice;
    public int ChainId => V4Route.ChainId;

    public RouteV4(V4.Entities.Route<TInput, TOutput> v4Route)
    {
        V4Route = v4Route;
        Pools = v4Route.Pools.Cast<object>().ToList();
        Path = v4Route.CurrencyPath.ToList();
    }
}

/// <summary>Mixed route wrapper. Port of router-sdk <c>MixedRoute</c>.</summary>
public class MixedRoute<TInput, TOutput> : MixedRouteSDK<TInput, TOutput>, IRoute<TInput, TOutput>
    where TInput : BaseCurrency
    where TOutput : BaseCurrency
{
    public Protocol Protocol => Protocol.MIXED;

    public MixedRoute(MixedRouteSDK<TInput, TOutput> mixedRoute)
        : base(mixedRoute.Pools, mixedRoute.Input, mixedRoute.Output)
    {
    }
}
