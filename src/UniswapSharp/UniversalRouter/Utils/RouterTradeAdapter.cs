using System.Numerics;
using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.Router.Entities;
using UniswapSharp.Router.Entities.MixedRoute;
using MixedSDK = UniswapSharp.Router.Entities.MixedRoute.MixedRouteSDK<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;
using RouterTrade = UniswapSharp.Router.Entities.Trade<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;
using V2Pair = UniswapSharp.V2.Entities.Pair;
using V2Route = UniswapSharp.V2.Entities.Route<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;
using V3Pool = UniswapSharp.V3.Entities.Pool;
using V3Route = UniswapSharp.V3.Entities.Route<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;
using V4Pool = UniswapSharp.V4.Entities.Pool;
using V4Route = UniswapSharp.V4.Entities.Route<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;

namespace UniswapSharp.UniversalRouter.Utils;

/// <summary>A token as described in a classic-quote route. Port of <c>TokenInRoute</c>.</summary>
public sealed record TokenInRoute(
    string Address, int ChainId, string Symbol, string Decimals,
    string? Name = null, string? BuyFeeBps = null, string? SellFeeBps = null);

/// <summary>Pool kind discriminator. Port of <c>PoolType</c>.</summary>
public enum PoolType
{
    V2Pool,
    V3Pool,
    V4Pool,
}

/// <summary>A V2 reserve entry. Port of <c>V2Reserve</c>.</summary>
public sealed record V2Reserve(TokenInRoute Token, string Quotient);

/// <summary>Base of the classic-quote pool-in-route union.</summary>
public abstract record PoolInRoute
{
    public abstract PoolType Type { get; }
    public TokenInRoute? TokenIn { get; init; }
    public TokenInRoute? TokenOut { get; init; }
    public string? AmountIn { get; init; }
    public string? AmountOut { get; init; }
}

/// <summary>A V2 pool-in-route. Port of <c>V2PoolInRoute</c>.</summary>
public sealed record V2PoolInRoute : PoolInRoute
{
    public override PoolType Type => PoolType.V2Pool;
    public string? Address { get; init; }
    public required V2Reserve Reserve0 { get; init; }
    public required V2Reserve Reserve1 { get; init; }
}

/// <summary>A V3 pool-in-route. Port of <c>V3PoolInRoute</c>.</summary>
public sealed record V3PoolInRoute : PoolInRoute
{
    public override PoolType Type => PoolType.V3Pool;
    public string? Address { get; init; }
    public required string SqrtRatioX96 { get; init; }
    public required string Liquidity { get; init; }
    public required string TickCurrent { get; init; }
    public required string Fee { get; init; }
}

/// <summary>A V4 pool-in-route. Port of <c>V4PoolInRoute</c>.</summary>
public sealed record V4PoolInRoute : PoolInRoute
{
    public override PoolType Type => PoolType.V4Pool;
    public string? Address { get; init; }
    public required string Fee { get; init; }
    public required string TickSpacing { get; init; }
    public required string Hooks { get; init; }
    public required string Liquidity { get; init; }
    public required string SqrtRatioX96 { get; init; }
    public required string TickCurrent { get; init; }
}

/// <summary>A partial classic quote. Port of <c>PartialClassicQuote</c>.</summary>
public sealed record PartialClassicQuote(
    string TokenIn, string TokenOut, TradeType TradeType, List<List<PoolInRoute>>? Route);

/// <summary>
/// Port of universal-router-sdk <c>utils/routerTradeAdapter.ts</c> — maps a classic-quote response into a
/// router-sdk <see cref="RouterTrade"/> usable to build a <c>UniswapTrade</c>.
/// </summary>
public static class RouterTradeAdapter
{
    private sealed record RouteResult(
        V4Route? Routev4, V3Route? Routev3, V2Route? Routev2, MixedSDK? MixedRoute,
        CurrencyAmount<BaseCurrency> InputAmount, CurrencyAmount<BaseCurrency> OutputAmount);

    public static bool IsNativeCurrency(string address) =>
        address.ToLowerInvariant() == Constants.ETH_ADDRESS.ToLowerInvariant() ||
        address.ToLowerInvariant() == Constants.E_ETH_ADDRESS.ToLowerInvariant();

    /// <summary>Generate a <see cref="RouterTrade"/> from a classic-quote response.</summary>
    public static RouterTrade FromClassicQuote(PartialClassicQuote quote)
    {
        var route = quote.Route;
        if (route is null)
        {
            throw new InvalidOperationException("Expected route to be present");
        }
        if (route.Count == 0)
        {
            throw new InvalidOperationException("Expected there to be at least one route");
        }
        if (route.Any(r => r.Count == 0))
        {
            throw new InvalidOperationException("Expected all routes to have at least one pool");
        }

        var firstRoute = route[0];
        var tokenInData = firstRoute[0].TokenIn;
        var tokenOutData = firstRoute[^1].TokenOut;

        if (tokenInData is null || tokenOutData is null)
        {
            throw new InvalidOperationException("Expected both tokenIn and tokenOut to be present");
        }
        if (tokenInData.ChainId != tokenOutData.ChainId)
        {
            throw new InvalidOperationException("Expected tokenIn and tokenOut to be have same chainId");
        }

        var parsedCurrencyIn = ToCurrency(IsNativeCurrency(quote.TokenIn), tokenInData);
        var parsedCurrencyOut = ToCurrency(IsNativeCurrency(quote.TokenOut), tokenOutData);

        var typedRoutes = route.Select(subRoute =>
        {
            var rawAmountIn = subRoute[0].AmountIn;
            var rawAmountOut = subRoute[^1].AmountOut;
            if (rawAmountIn is null || rawAmountOut is null)
            {
                throw new InvalidOperationException("Expected both raw amountIn and raw amountOut to be present");
            }

            var inputAmount = CurrencyAmount<BaseCurrency>.FromRawAmount(parsedCurrencyIn, BigInteger.Parse(rawAmountIn));
            var outputAmount = CurrencyAmount<BaseCurrency>.FromRawAmount(parsedCurrencyOut, BigInteger.Parse(rawAmountOut));

            bool isOnlyV2 = subRoute.All(p => p.Type == PoolType.V2Pool);
            bool isOnlyV3 = subRoute.All(p => p.Type == PoolType.V3Pool);
            bool isOnlyV4 = subRoute.All(p => p.Type == PoolType.V4Pool);

            return new RouteResult(
                isOnlyV4 ? new V4Route(subRoute.Cast<V4PoolInRoute>().Select(ToV4Pool).ToList(), parsedCurrencyIn, parsedCurrencyOut) : null,
                isOnlyV3 ? new V3Route(subRoute.Cast<V3PoolInRoute>().Select(ToV3Pool).ToList(), parsedCurrencyIn, parsedCurrencyOut) : null,
                isOnlyV2 ? new V2Route(subRoute.Cast<V2PoolInRoute>().Select(ToPair).ToList(), parsedCurrencyIn, parsedCurrencyOut) : null,
                !isOnlyV4 && !isOnlyV3 && !isOnlyV2 ? new MixedSDK(subRoute.Select(ToPoolOrPair).ToList(), parsedCurrencyIn, parsedCurrencyOut) : null,
                inputAmount, outputAmount);
        }).ToList();

        return new RouterTrade(
            quote.TradeType,
            v2Routes: typedRoutes.Where(r => r.Routev2 is not null)
                .Select(r => new V2RouteAmounts<BaseCurrency, BaseCurrency>(r.Routev2!, r.InputAmount, r.OutputAmount)),
            v3Routes: typedRoutes.Where(r => r.Routev3 is not null)
                .Select(r => new V3RouteAmounts<BaseCurrency, BaseCurrency>(r.Routev3!, r.InputAmount, r.OutputAmount)),
            v4Routes: typedRoutes.Where(r => r.Routev4 is not null)
                .Select(r => new V4RouteAmounts<BaseCurrency, BaseCurrency>(r.Routev4!, r.InputAmount, r.OutputAmount)),
            mixedRoutes: typedRoutes.Where(r => r.MixedRoute is not null)
                .Select(r => new MixedRouteAmounts<BaseCurrency, BaseCurrency>(r.MixedRoute!, r.InputAmount, r.OutputAmount)));
    }

    private static BaseCurrency ToCurrency(bool isNative, TokenInRoute token) =>
        isNative ? Ether.OnChain(token.ChainId) : ToToken(token);

    private static object ToPoolOrPair(PoolInRoute pool) => pool switch
    {
        V4PoolInRoute v4 => ToV4Pool(v4),
        V3PoolInRoute v3 => ToV3Pool(v3),
        V2PoolInRoute v2 => ToPair(v2),
        _ => throw new InvalidOperationException("Invalid pool type"),
    };

    private static Token ToToken(TokenInRoute token) => new(
        token.ChainId,
        token.Address,
        int.Parse(token.Decimals),
        token.Symbol,
        name: null,
        bypassChecksum: false,
        buyFeeBps: token.BuyFeeBps is not null ? BigInteger.Parse(token.BuyFeeBps) : null,
        sellFeeBps: token.SellFeeBps is not null ? BigInteger.Parse(token.SellFeeBps) : null);

    private static V3Pool ToV3Pool(V3PoolInRoute p) => new(
        ToToken(p.TokenIn!), ToToken(p.TokenOut!),
        (UniswapSharp.V3.Constants.FeeAmount)int.Parse(p.Fee),
        BigInteger.Parse(p.SqrtRatioX96), BigInteger.Parse(p.Liquidity), int.Parse(p.TickCurrent));

    private static V4Pool ToV4Pool(V4PoolInRoute p)
    {
        var currencyIn = ToCurrency(IsNativeCurrency(p.TokenIn!.Address), p.TokenIn);
        var currencyOut = ToCurrency(IsNativeCurrency(p.TokenOut!.Address), p.TokenOut);
        return new V4Pool(currencyIn, currencyOut, int.Parse(p.Fee), int.Parse(p.TickSpacing), p.Hooks,
            BigInteger.Parse(p.SqrtRatioX96), BigInteger.Parse(p.Liquidity), int.Parse(p.TickCurrent));
    }

    private static V2Pair ToPair(V2PoolInRoute p) => new(
        CurrencyAmount<Token>.FromRawAmount(ToToken(p.Reserve0.Token), BigInteger.Parse(p.Reserve0.Quotient)),
        CurrencyAmount<Token>.FromRawAmount(ToToken(p.Reserve1.Token), BigInteger.Parse(p.Reserve1.Quotient)));
}
