using System.Numerics;
using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.Router.Entities.MixedRoute;
using UniswapSharp.Router.Utils;

namespace UniswapSharp.Router.Entities;

/// <summary>A single swap within an aggregated <see cref="Trade{TInput,TOutput}"/>.</summary>
public class RouterSwap<TInput, TOutput>
    where TInput : BaseCurrency
    where TOutput : BaseCurrency
{
    public IRoute<TInput, TOutput> Route { get; }
    public CurrencyAmount<TInput> InputAmount { get; }
    public CurrencyAmount<TOutput> OutputAmount { get; }
    public BigInteger[]? MinHopPriceX36 { get; }

    public RouterSwap(IRoute<TInput, TOutput> route, CurrencyAmount<TInput> inputAmount, CurrencyAmount<TOutput> outputAmount, BigInteger[]? minHopPriceX36 = null)
    {
        Route = route;
        InputAmount = inputAmount;
        OutputAmount = outputAmount;
        MinHopPriceX36 = minHopPriceX36;
    }
}

/// <summary>
/// Port of router-sdk <c>entities/trade.ts</c>. An aggregated trade split across V2/V3/V4/mixed routes.
/// </summary>
public class Trade<TInput, TOutput>
    where TInput : BaseCurrency
    where TOutput : BaseCurrency
{
    public List<IRoute<TInput, TOutput>> Routes { get; }
    public List<RouterSwap<TInput, TOutput>> Swaps { get; }
    public TradeType TradeType { get; }

    private CurrencyAmount<TInput>? _inputAmount;
    private CurrencyAmount<TOutput>? _outputAmount;
    private List<IRoute<TInput, TOutput>>? _nativeInputRoutes;
    private List<IRoute<TInput, TOutput>>? _wethInputRoutes;
    private Price<TInput, TOutput>? _executionPrice;
    private Percent? _priceImpact;

    /// <summary>
    /// Constructs a trade across pre-computed V2/V3/V4/mixed routes (no swap simulation). Mirrors the
    /// upstream public constructor's <c>{ v2Routes, v3Routes, v4Routes, mixedRoutes, tradeType }</c> shape.
    /// </summary>
    public Trade(
        TradeType tradeType,
        IEnumerable<V2RouteAmounts<TInput, TOutput>>? v2Routes = null,
        IEnumerable<V3RouteAmounts<TInput, TOutput>>? v3Routes = null,
        IEnumerable<V4RouteAmounts<TInput, TOutput>>? v4Routes = null,
        IEnumerable<MixedRouteAmounts<TInput, TOutput>>? mixedRoutes = null)
        : this(BuildSwaps(v2Routes, v3Routes, v4Routes, mixedRoutes), tradeType)
    {
    }

    private static List<RouterSwap<TInput, TOutput>> BuildSwaps(
        IEnumerable<V2RouteAmounts<TInput, TOutput>>? v2Routes,
        IEnumerable<V3RouteAmounts<TInput, TOutput>>? v3Routes,
        IEnumerable<V4RouteAmounts<TInput, TOutput>>? v4Routes,
        IEnumerable<MixedRouteAmounts<TInput, TOutput>>? mixedRoutes)
    {
        var swaps = new List<RouterSwap<TInput, TOutput>>();
        foreach (var r in v2Routes ?? Enumerable.Empty<V2RouteAmounts<TInput, TOutput>>())
        {
            swaps.Add(new RouterSwap<TInput, TOutput>(new RouteV2<TInput, TOutput>(r.Routev2), r.InputAmount, r.OutputAmount, r.MinHopPriceX36));
        }
        foreach (var r in v3Routes ?? Enumerable.Empty<V3RouteAmounts<TInput, TOutput>>())
        {
            swaps.Add(new RouterSwap<TInput, TOutput>(new RouteV3<TInput, TOutput>(r.Routev3), r.InputAmount, r.OutputAmount, r.MinHopPriceX36));
        }
        foreach (var r in v4Routes ?? Enumerable.Empty<V4RouteAmounts<TInput, TOutput>>())
        {
            swaps.Add(new RouterSwap<TInput, TOutput>(new RouteV4<TInput, TOutput>(r.Routev4), r.InputAmount, r.OutputAmount, r.MinHopPriceX36));
        }
        foreach (var r in mixedRoutes ?? Enumerable.Empty<MixedRouteAmounts<TInput, TOutput>>())
        {
            swaps.Add(new RouterSwap<TInput, TOutput>(new MixedRoute<TInput, TOutput>(r.MixedRoute), r.InputAmount, r.OutputAmount, r.MinHopPriceX36));
        }
        return swaps;
    }

    private Trade(List<RouterSwap<TInput, TOutput>> swaps, TradeType tradeType)
    {
        if (swaps.Count == 0)
        {
            throw new InvalidOperationException("No routes provided when calling Trade constructor");
        }

        Swaps = swaps;
        Routes = swaps.Select(s => s.Route).ToList();
        TradeType = tradeType;

        // each route must have the same input and output currency
        var inputCurrency = swaps[0].InputAmount.Currency;
        var outputCurrency = swaps[0].OutputAmount.Currency;
        if (!swaps.All(s => inputCurrency.Wrapped().Equals(s.Route.Input.Wrapped())))
        {
            throw new ArgumentException("INPUT_CURRENCY_MATCH");
        }
        if (!swaps.All(s => outputCurrency.Wrapped().Equals(s.Route.Output.Wrapped())))
        {
            throw new ArgumentException("OUTPUT_CURRENCY_MATCH");
        }

        // pools must be unique inter protocols
        int numPools = swaps.Sum(s => s.Route.Pools.Count);
        var poolIdentifierSet = new HashSet<string>();
        foreach (var swap in swaps)
        {
            foreach (var pool in swap.Route.Pools)
            {
                poolIdentifierSet.Add(TPool.PoolIdentifier(pool));
            }
        }
        if (numPools != poolIdentifierSet.Count)
        {
            throw new ArgumentException("POOLS_DUPLICATED");
        }
    }

    public CurrencyAmount<TInput> InputAmount
    {
        get
        {
            if (_inputAmount != null)
            {
                return _inputAmount;
            }
            var currency = Swaps[0].InputAmount.Currency;
            _inputAmount = Swaps.Aggregate(
                CurrencyAmount<TInput>.FromRawAmount(currency, 0),
                (total, swap) => total.Add(swap.InputAmount));
            return _inputAmount;
        }
    }

    public CurrencyAmount<TOutput> OutputAmount
    {
        get
        {
            if (_outputAmount != null)
            {
                return _outputAmount;
            }
            var currency = Swaps[0].OutputAmount.Currency;
            _outputAmount = Swaps.Aggregate(
                CurrencyAmount<TOutput>.FromRawAmount(currency, 0),
                (total, swap) => total.Add(swap.OutputAmount));
            return _outputAmount;
        }
    }

    /// <summary>
    /// Returns the total input/output amounts plus the portion of each satisfied by native currency paths.
    /// </summary>
    public (CurrencyAmount<TInput> InputAmount, CurrencyAmount<TInput>? InputAmountNative,
            CurrencyAmount<TOutput> OutputAmount, CurrencyAmount<TOutput>? OutputAmountNative) Amounts
    {
        get
        {
            var inputNativeCurrency = Swaps.FirstOrDefault(s => s.InputAmount.Currency.IsNative)?.InputAmount.Currency;
            var outputNativeCurrency = Swaps.FirstOrDefault(s => s.OutputAmount.Currency.IsNative)?.OutputAmount.Currency;

            CurrencyAmount<TInput>? inputAmountNative = inputNativeCurrency != null
                ? Swaps.Aggregate(
                    CurrencyAmount<TInput>.FromRawAmount(inputNativeCurrency, 0),
                    (total, swap) => swap.Route.PathInput.IsNative ? total.Add(swap.InputAmount) : total)
                : null;

            CurrencyAmount<TOutput>? outputAmountNative = outputNativeCurrency != null
                ? Swaps.Aggregate(
                    CurrencyAmount<TOutput>.FromRawAmount(outputNativeCurrency, 0),
                    (total, swap) => swap.Route.PathOutput.IsNative ? total.Add(swap.OutputAmount) : total)
                : null;

            return (InputAmount, inputAmountNative, OutputAmount, outputAmountNative);
        }
    }

    public int NumberOfInputWraps => InputAmount.Currency.IsNative ? WethInputRoutes.Count : 0;

    public int NumberOfInputUnwraps => IsWrappedNative(InputAmount.Currency) ? NativeInputRoutes.Count : 0;

    public List<IRoute<TInput, TOutput>> NativeInputRoutes =>
        _nativeInputRoutes ??= Routes.Where(route => route.PathInput.IsNative).ToList();

    public List<IRoute<TInput, TOutput>> WethInputRoutes =>
        _wethInputRoutes ??= Routes.Where(route => IsWrappedNative(route.PathInput)).ToList();

    public Price<TInput, TOutput> ExecutionPrice =>
        _executionPrice ??= new Price<TInput, TOutput>(
            InputAmount.Currency, OutputAmount.Currency, InputAmount.Quotient, OutputAmount.Quotient);

    /// <summary>Returns the sell tax of the input token.</summary>
    public Percent InputTax
    {
        get
        {
            var inputCurrency = InputAmount.Currency;
            var sellFeeBps = inputCurrency.Wrapped().SellFeeBps;
            if (inputCurrency.IsNative || sellFeeBps is null || sellFeeBps.Value.IsZero)
            {
                return Constants.ZERO_PERCENT;
            }
            return new Percent(sellFeeBps.Value, 10000);
        }
    }

    /// <summary>Returns the buy tax of the output token.</summary>
    public Percent OutputTax
    {
        get
        {
            var outputCurrency = OutputAmount.Currency;
            var buyFeeBps = outputCurrency.Wrapped().BuyFeeBps;
            if (outputCurrency.IsNative || buyFeeBps is null || buyFeeBps.Value.IsZero)
            {
                return Constants.ZERO_PERCENT;
            }
            return new Percent(buyFeeBps.Value, 10000);
        }
    }

    private static bool IsWrappedNative(BaseCurrency currency) =>
        currency.Equals(Ether.OnChain(currency.ChainId).Wrapped());

    public Percent PriceImpact
    {
        get
        {
            if (_priceImpact != null)
            {
                return _priceImpact;
            }

            // returns 0% price impact even though this may be inaccurate: without the pre-buy-tax amount, use 0%.
            if (OutputTax.Equals(Constants.ONE_HUNDRED_PERCENT))
            {
                return Constants.ZERO_PERCENT;
            }

            var spotOutputAmount = CurrencyAmount<TOutput>.FromRawAmount(OutputAmount.Currency, 0);
            foreach (var swap in Swaps)
            {
                var midPrice = swap.Route.MidPrice;
                var postTaxInputAmount = swap.InputAmount.Multiply(new Fraction(Constants.ONE).Subtract(InputTax));
                spotOutputAmount = spotOutputAmount.Add(midPrice.Quote(postTaxInputAmount));
            }

            // if the total output of this trade is 0, the post-tax input was likely 0 too, so no market move.
            if (spotOutputAmount.Numerator.IsZero)
            {
                return Constants.ZERO_PERCENT;
            }

            var preTaxOutputAmount = OutputAmount.Divide(new Fraction(Constants.ONE).Subtract(OutputTax));
            var priceImpact = spotOutputAmount.Subtract(preTaxOutputAmount).Divide(spotOutputAmount);
            _priceImpact = new Percent(priceImpact.Numerator, priceImpact.Denominator);
            return _priceImpact;
        }
    }

    public CurrencyAmount<TOutput> MinimumAmountOut(Percent slippageTolerance, CurrencyAmount<TOutput>? amountOut = null)
    {
        if (slippageTolerance.LessThan(Constants.ZERO))
        {
            throw new ArgumentException("SLIPPAGE_TOLERANCE");
        }
        amountOut ??= OutputAmount;
        if (TradeType == TradeType.EXACT_OUTPUT)
        {
            return amountOut;
        }
        var slippageAdjustedAmountOut = new Fraction(Constants.ONE)
            .Subtract(slippageTolerance)
            .Multiply(amountOut.Quotient)
            .Quotient;
        var clampedAmount = slippageAdjustedAmountOut > Constants.ZERO ? slippageAdjustedAmountOut : Constants.ZERO;
        return CurrencyAmount<TOutput>.FromRawAmount(amountOut.Currency, clampedAmount);
    }

    public CurrencyAmount<TInput> MaximumAmountIn(Percent slippageTolerance, CurrencyAmount<TInput>? amountIn = null)
    {
        if (slippageTolerance.LessThan(Constants.ZERO))
        {
            throw new ArgumentException("SLIPPAGE_TOLERANCE");
        }
        amountIn ??= InputAmount;
        if (TradeType == TradeType.EXACT_INPUT)
        {
            return amountIn;
        }
        var slippageAdjustedAmountIn = new Fraction(Constants.ONE)
            .Add(slippageTolerance)
            .Multiply(amountIn.Quotient)
            .Quotient;
        return CurrencyAmount<TInput>.FromRawAmount(amountIn.Currency, slippageAdjustedAmountIn);
    }

    public Price<TInput, TOutput> WorstExecutionPrice(Percent slippageTolerance)
    {
        return new Price<TInput, TOutput>(
            InputAmount.Currency, OutputAmount.Currency,
            MaximumAmountIn(slippageTolerance).Quotient, MinimumAmountOut(slippageTolerance).Quotient);
    }

    private static CurrencyAmount<TInput> ToInputAmount(CurrencyAmount<BaseCurrency> amount) =>
        CurrencyAmount<TInput>.FromFractionalAmount((TInput)amount.Currency, amount.Numerator, amount.Denominator);

    public static async Task<Trade<TInput, TOutput>> FromRoute(object route, CurrencyAmount<BaseCurrency> amount, TradeType tradeType)
    {
        // Upstream accepts the route wrappers (which extend the SDK routes); ours wrap by composition, so unwrap.
        object underlying = route switch
        {
            RouteV2<TInput, TOutput> w => w.V2Route,
            RouteV3<TInput, TOutput> w => w.V3Route,
            RouteV4<TInput, TOutput> w => w.V4Route,
            _ => route, // MixedRoute is-a MixedRouteSDK; raw SDK routes pass through
        };

        RouterSwap<TInput, TOutput> swap;
        switch (underlying)
        {
            case V2.Entities.Route<TInput, TOutput> routev2:
            {
                var t = new V2.Entities.Trade<TInput, TOutput>(routev2, amount, tradeType);
                swap = new RouterSwap<TInput, TOutput>(new RouteV2<TInput, TOutput>(routev2), t.InputAmount, t.OutputAmount);
                break;
            }
            case V3.Entities.Route<TInput, TOutput> routev3:
            {
                var t = await V3.Entities.Trade<TInput, TOutput>.FromRoute(routev3, amount, tradeType);
                swap = new RouterSwap<TInput, TOutput>(new RouteV3<TInput, TOutput>(routev3), t.InputAmount, t.OutputAmount);
                break;
            }
            case V4.Entities.Route<TInput, TOutput> routev4:
            {
                var t = await V4.Entities.Trade<TInput, TOutput>.FromRoute(routev4, amount, tradeType);
                swap = new RouterSwap<TInput, TOutput>(new RouteV4<TInput, TOutput>(routev4), t.InputAmount, t.OutputAmount);
                break;
            }
            case MixedRouteSDK<TInput, TOutput> mixedRoute:
            {
                var t = await MixedRouteTrade<TInput, TOutput>.FromRoute(mixedRoute, ToInputAmount(amount), tradeType);
                swap = new RouterSwap<TInput, TOutput>(new MixedRoute<TInput, TOutput>(mixedRoute), t.InputAmount, t.OutputAmount);
                break;
            }
            default:
                throw new ArgumentException("Invalid route type");
        }

        return new Trade<TInput, TOutput>(new List<RouterSwap<TInput, TOutput>> { swap }, tradeType);
    }

    public static async Task<Trade<TInput, TOutput>> FromRoutes(
        List<(V2.Entities.Route<TInput, TOutput> routev2, CurrencyAmount<BaseCurrency> amount)> v2Routes,
        List<(V3.Entities.Route<TInput, TOutput> routev3, CurrencyAmount<BaseCurrency> amount)> v3Routes,
        TradeType tradeType,
        List<(MixedRouteSDK<TInput, TOutput> mixedRoute, CurrencyAmount<BaseCurrency> amount)>? mixedRoutes = null,
        List<(V4.Entities.Route<TInput, TOutput> routev4, CurrencyAmount<BaseCurrency> amount)>? v4Routes = null)
    {
        var swaps = new List<RouterSwap<TInput, TOutput>>();

        foreach (var (routev2, amount) in v2Routes)
        {
            var t = new V2.Entities.Trade<TInput, TOutput>(routev2, amount, tradeType);
            swaps.Add(new RouterSwap<TInput, TOutput>(new RouteV2<TInput, TOutput>(routev2), t.InputAmount, t.OutputAmount));
        }

        foreach (var (routev3, amount) in v3Routes)
        {
            var t = await V3.Entities.Trade<TInput, TOutput>.FromRoute(routev3, amount, tradeType);
            swaps.Add(new RouterSwap<TInput, TOutput>(new RouteV3<TInput, TOutput>(routev3), t.InputAmount, t.OutputAmount));
        }

        if (v4Routes != null)
        {
            foreach (var (routev4, amount) in v4Routes)
            {
                var t = await V4.Entities.Trade<TInput, TOutput>.FromRoute(routev4, amount, tradeType);
                swaps.Add(new RouterSwap<TInput, TOutput>(new RouteV4<TInput, TOutput>(routev4), t.InputAmount, t.OutputAmount));
            }
        }

        if (mixedRoutes != null)
        {
            foreach (var (mixedRoute, amount) in mixedRoutes)
            {
                var t = await MixedRouteTrade<TInput, TOutput>.FromRoute(mixedRoute, ToInputAmount(amount), tradeType);
                swaps.Add(new RouterSwap<TInput, TOutput>(new MixedRoute<TInput, TOutput>(mixedRoute), t.InputAmount, t.OutputAmount));
            }
        }

        return new Trade<TInput, TOutput>(swaps, tradeType);
    }
}

/// <summary>A pre-computed V2 route + amounts for the aggregated <see cref="Trade{TInput,TOutput}"/> constructor.</summary>
public record V2RouteAmounts<TInput, TOutput>(
    V2.Entities.Route<TInput, TOutput> Routev2,
    CurrencyAmount<TInput> InputAmount,
    CurrencyAmount<TOutput> OutputAmount,
    BigInteger[]? MinHopPriceX36 = null)
    where TInput : BaseCurrency where TOutput : BaseCurrency;

/// <summary>A pre-computed V3 route + amounts for the aggregated <see cref="Trade{TInput,TOutput}"/> constructor.</summary>
public record V3RouteAmounts<TInput, TOutput>(
    V3.Entities.Route<TInput, TOutput> Routev3,
    CurrencyAmount<TInput> InputAmount,
    CurrencyAmount<TOutput> OutputAmount,
    BigInteger[]? MinHopPriceX36 = null)
    where TInput : BaseCurrency where TOutput : BaseCurrency;

/// <summary>A pre-computed V4 route + amounts for the aggregated <see cref="Trade{TInput,TOutput}"/> constructor.</summary>
public record V4RouteAmounts<TInput, TOutput>(
    V4.Entities.Route<TInput, TOutput> Routev4,
    CurrencyAmount<TInput> InputAmount,
    CurrencyAmount<TOutput> OutputAmount,
    BigInteger[]? MinHopPriceX36 = null)
    where TInput : BaseCurrency where TOutput : BaseCurrency;

/// <summary>A pre-computed mixed route + amounts for the aggregated <see cref="Trade{TInput,TOutput}"/> constructor.</summary>
public record MixedRouteAmounts<TInput, TOutput>(
    MixedRouteSDK<TInput, TOutput> MixedRoute,
    CurrencyAmount<TInput> InputAmount,
    CurrencyAmount<TOutput> OutputAmount,
    BigInteger[]? MinHopPriceX36 = null)
    where TInput : BaseCurrency where TOutput : BaseCurrency;
