using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.Core.Utils;
using UniswapSharp.Router.Utils;
using V2Pair = UniswapSharp.V2.Entities.Pair;
using V4Pool = UniswapSharp.V4.Entities.Pool;

namespace UniswapSharp.Router.Entities.MixedRoute;

/// <summary>A single swap within a <see cref="MixedRouteTrade{TInput,TOutput}"/>.</summary>
public class MixedRouteSwap<TInput, TOutput>
    where TInput : BaseCurrency
    where TOutput : BaseCurrency
{
    public MixedRouteSDK<TInput, TOutput> Route { get; }
    public CurrencyAmount<TInput> InputAmount { get; }
    public CurrencyAmount<TOutput> OutputAmount { get; }

    public MixedRouteSwap(MixedRouteSDK<TInput, TOutput> route, CurrencyAmount<TInput> inputAmount, CurrencyAmount<TOutput> outputAmount)
    {
        Route = route;
        InputAmount = inputAmount;
        OutputAmount = outputAmount;
    }
}

/// <summary>
/// Port of router-sdk <c>entities/mixedRoute/trade.ts</c>. Represents a trade executed against a set of
/// mixed routes. Only exact-input trades are supported.
/// </summary>
public class MixedRouteTrade<TInput, TOutput>
    where TInput : BaseCurrency
    where TOutput : BaseCurrency
{
    /// <summary>The swaps of the trade, i.e. which routes and how much is swapped in each.</summary>
    public List<MixedRouteSwap<TInput, TOutput>> Swaps { get; }

    /// <summary>The type of the trade, always exact in.</summary>
    public TradeType TradeType { get; }

    private CurrencyAmount<TInput>? _inputAmount;
    private CurrencyAmount<TOutput>? _outputAmount;
    private Price<TInput, TOutput>? _executionPrice;
    private Percent? _priceImpact;

    /// <summary>
    /// Deprecated in favour of <see cref="Swaps"/>. Returns the single route or throws MULTIPLE_ROUTES.
    /// </summary>
    public MixedRouteSDK<TInput, TOutput> Route
    {
        get
        {
            if (Swaps.Count != 1)
            {
                throw new InvalidOperationException("MULTIPLE_ROUTES");
            }
            return Swaps[0].Route;
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
            var inputCurrency = Swaps[0].InputAmount.Currency;
            _inputAmount = Swaps.Aggregate(
                CurrencyAmount<TInput>.FromRawAmount(inputCurrency, 0),
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
            var outputCurrency = Swaps[0].OutputAmount.Currency;
            _outputAmount = Swaps.Aggregate(
                CurrencyAmount<TOutput>.FromRawAmount(outputCurrency, 0),
                (total, swap) => total.Add(swap.OutputAmount));
            return _outputAmount;
        }
    }

    public Price<TInput, TOutput> ExecutionPrice =>
        _executionPrice ??= new Price<TInput, TOutput>(
            InputAmount.Currency, OutputAmount.Currency, InputAmount.Quotient, OutputAmount.Quotient);

    public Percent PriceImpact
    {
        get
        {
            if (_priceImpact != null)
            {
                return _priceImpact;
            }

            var spotOutputAmount = CurrencyAmount<TOutput>.FromRawAmount(OutputAmount.Currency, 0);
            foreach (var swap in Swaps)
            {
                var midPrice = swap.Route.MidPrice;
                spotOutputAmount = spotOutputAmount.Add(midPrice.Quote(swap.InputAmount));
            }

            var priceImpact = spotOutputAmount.Subtract(OutputAmount).Divide(spotOutputAmount);
            _priceImpact = new Percent(priceImpact.Numerator, priceImpact.Denominator);
            return _priceImpact;
        }
    }

    private MixedRouteTrade(List<MixedRouteSwap<TInput, TOutput>> routes, TradeType tradeType)
    {
        var inputCurrency = routes[0].InputAmount.Currency;
        var outputCurrency = routes[0].OutputAmount.Currency;

        if (!routes.All(r => inputCurrency.Wrapped().Equals(r.Route.Input.Wrapped())))
        {
            throw new ArgumentException("INPUT_CURRENCY_MATCH");
        }
        if (!routes.All(r => outputCurrency.Wrapped().Equals(r.Route.Output.Wrapped())))
        {
            throw new ArgumentException("OUTPUT_CURRENCY_MATCH");
        }

        int numPools = routes.Sum(r => r.Route.Pools.Count);
        var poolIdentifierSet = new HashSet<string>();
        foreach (var r in routes)
        {
            foreach (var pool in r.Route.Pools)
            {
                poolIdentifierSet.Add(TPool.PoolIdentifier(pool));
            }
        }
        if (numPools != poolIdentifierSet.Count)
        {
            throw new ArgumentException("POOLS_DUPLICATED");
        }

        if (tradeType != TradeType.EXACT_INPUT)
        {
            throw new ArgumentException("TRADE_TYPE");
        }

        Swaps = routes;
        TradeType = tradeType;
    }

    public static async Task<MixedRouteTrade<TInput, TOutput>> FromRoute(
        MixedRouteSDK<TInput, TOutput> route, CurrencyAmount<TInput> amount, TradeType tradeType)
    {
        if (tradeType != TradeType.EXACT_INPUT)
        {
            throw new ArgumentException("TRADE_TYPE");
        }
        if (!amount.Currency.Equals(route.Input))
        {
            throw new ArgumentException("INPUT");
        }

        var amounts = new CurrencyAmount<BaseCurrency>[route.Path.Count];
        amounts[0] = PathCurrency.AmountWithPathCurrency(amount.AsBaseCurrency()!, route.Pools[0]);
        for (int i = 0; i < route.Path.Count - 1; i++)
        {
            var pool = route.Pools[i];
            amounts[i + 1] = await TPool.GetOutputAmount(pool, PathCurrency.AmountWithPathCurrency(amounts[i], pool));
        }

        var inputAmount = CurrencyAmount<TInput>.FromFractionalAmount(route.Input, amount.Numerator, amount.Denominator);
        var last = amounts[^1];
        var outputAmount = CurrencyAmount<TOutput>.FromFractionalAmount(route.Output, last.Numerator, last.Denominator);

        return new MixedRouteTrade<TInput, TOutput>(
            new List<MixedRouteSwap<TInput, TOutput>> { new(route, inputAmount, outputAmount) }, tradeType);
    }

    public static async Task<MixedRouteTrade<TInput, TOutput>> FromRoutes(
        List<(CurrencyAmount<TInput> amount, MixedRouteSDK<TInput, TOutput> route)> routes, TradeType tradeType)
    {
        if (tradeType != TradeType.EXACT_INPUT)
        {
            throw new ArgumentException("TRADE_TYPE");
        }

        var populatedRoutes = new List<MixedRouteSwap<TInput, TOutput>>();
        foreach (var (amount, route) in routes)
        {
            if (!amount.Currency.Equals(route.Input))
            {
                throw new ArgumentException("INPUT");
            }

            var inputAmount = CurrencyAmount<TInput>.FromFractionalAmount(route.Input, amount.Numerator, amount.Denominator);
            var amounts = new CurrencyAmount<BaseCurrency>[route.Path.Count];
            amounts[0] = CurrencyAmount<BaseCurrency>.FromFractionalAmount(route.PathInput, amount.Numerator, amount.Denominator);

            for (int i = 0; i < route.Path.Count - 1; i++)
            {
                var pool = route.Pools[i];
                amounts[i + 1] = await TPool.GetOutputAmount(pool, PathCurrency.AmountWithPathCurrency(amounts[i], pool));
            }

            var last = amounts[^1];
            var outputAmount = CurrencyAmount<TOutput>.FromFractionalAmount(route.Output, last.Numerator, last.Denominator);
            populatedRoutes.Add(new MixedRouteSwap<TInput, TOutput>(route, inputAmount, outputAmount));
        }

        return new MixedRouteTrade<TInput, TOutput>(populatedRoutes, tradeType);
    }

    public static MixedRouteTrade<TInput, TOutput> CreateUncheckedTrade(
        MixedRouteSDK<TInput, TOutput> route,
        CurrencyAmount<TInput> inputAmount,
        CurrencyAmount<TOutput> outputAmount,
        TradeType tradeType)
    {
        return new MixedRouteTrade<TInput, TOutput>(
            new List<MixedRouteSwap<TInput, TOutput>> { new(route, inputAmount, outputAmount) }, tradeType);
    }

    public static MixedRouteTrade<TInput, TOutput> CreateUncheckedTradeWithMultipleRoutes(
        List<MixedRouteSwap<TInput, TOutput>> routes, TradeType tradeType)
    {
        return new MixedRouteTrade<TInput, TOutput>(routes, tradeType);
    }

    public CurrencyAmount<TOutput> MinimumAmountOut(Percent slippageTolerance, CurrencyAmount<TOutput>? amountOut = null)
    {
        if (slippageTolerance.LessThan(Constants.ZERO))
        {
            throw new ArgumentException("SLIPPAGE_TOLERANCE");
        }

        amountOut ??= OutputAmount;
        // does not support exactOutput, as enforced in the constructor
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
        return amountIn ?? InputAmount;
        // does not support exactOutput
    }

    public Price<TInput, TOutput> WorstExecutionPrice(Percent slippageTolerance)
    {
        return new Price<TInput, TOutput>(
            InputAmount.Currency, OutputAmount.Currency,
            MaximumAmountIn(slippageTolerance).Quotient, MinimumAmountOut(slippageTolerance).Quotient);
    }

    public class BestTradeOptions
    {
        public int MaxNumResults { get; set; } = 3;
        public int MaxHops { get; set; } = 3;
    }

    public static async Task<List<MixedRouteTrade<TInput, TOutput>>> BestTradeExactIn(
        List<object> pools,
        CurrencyAmount<TInput> currencyAmountIn,
        TOutput currencyOut,
        BestTradeOptions? options = null,
        List<object>? currentPools = null,
        CurrencyAmount<BaseCurrency>? nextAmountIn = null,
        List<MixedRouteTrade<TInput, TOutput>>? bestTrades = null)
    {
        options ??= new BestTradeOptions();
        currentPools ??= new List<object>();
        nextAmountIn ??= currencyAmountIn.AsBaseCurrency();
        bestTrades ??= new List<MixedRouteTrade<TInput, TOutput>>();

        if (pools.Count == 0) throw new ArgumentException("POOLS");
        if (options.MaxHops <= 0) throw new ArgumentException("MAX_HOPS");
        if (!(currencyAmountIn.Equals(nextAmountIn) || currentPools.Count > 0)) throw new ArgumentException("INVALID_RECURSION");

        var amountIn = nextAmountIn!;
        for (int i = 0; i < pools.Count; i++)
        {
            var pool = pools[i];
            var amountInAdjusted = pool is V4Pool ? amountIn : amountIn.Wrapped()!.AsBaseCurrency()!;

            // pool irrelevant
            if (!TPool.Token0(pool).Equals(amountInAdjusted.Currency) && !TPool.Token1(pool).Equals(amountInAdjusted.Currency))
            {
                continue;
            }
            if (pool is V2Pair pair && (pair.Reserve0.Quotient.IsZero || pair.Reserve1.Quotient.IsZero))
            {
                continue;
            }

            CurrencyAmount<BaseCurrency> amountOut;
            try
            {
                var input = pool is V4Pool ? amountInAdjusted : amountInAdjusted.Wrapped()!.AsBaseCurrency()!;
                amountOut = await TPool.GetOutputAmount(pool, input);
            }
            catch (V2.InsufficientInputAmountError)
            {
                // input too low
                continue;
            }

            // we have arrived at the output token, so this is the final trade of one of the paths
            if (amountOut.Currency.Wrapped().Equals(currencyOut.Wrapped()))
            {
                var newRoute = new MixedRouteSDK<TInput, TOutput>(
                    currentPools.Concat(new[] { pool }).ToList(), currencyAmountIn.Currency, currencyOut);
                var newTrade = await FromRoute(newRoute, currencyAmountIn, TradeType.EXACT_INPUT);
                SortedInsert.Insert(bestTrades, newTrade, options.MaxNumResults, TradeComparator);
            }
            else if (options.MaxHops > 1 && pools.Count > 1)
            {
                var poolsExcludingThisPool = pools.Take(i).Concat(pools.Skip(i + 1)).ToList();
                await BestTradeExactIn(
                    poolsExcludingThisPool,
                    currencyAmountIn,
                    currencyOut,
                    new BestTradeOptions { MaxNumResults = options.MaxNumResults, MaxHops = options.MaxHops - 1 },
                    currentPools.Concat(new[] { pool }).ToList(),
                    amountOut,
                    bestTrades);
            }
        }

        return bestTrades;
    }

    /// <summary>
    /// Trades comparator, an extension of the input/output comparator that also considers other
    /// dimensions of the trade. Port of <c>tradeComparator</c>.
    /// </summary>
    public static int TradeComparator(MixedRouteTrade<TInput, TOutput> a, MixedRouteTrade<TInput, TOutput> b)
    {
        if (!a.InputAmount.Currency.Equals(b.InputAmount.Currency))
            throw new InvalidOperationException("INPUT_CURRENCY");
        if (!a.OutputAmount.Currency.Equals(b.OutputAmount.Currency))
            throw new InvalidOperationException("OUTPUT_CURRENCY");

        if (a.OutputAmount.Equals(b.OutputAmount))
        {
            if (a.InputAmount.Equals(b.InputAmount))
            {
                // consider the number of hops since each hop costs gas
                int aHops = a.Swaps.Sum(swap => swap.Route.Path.Count);
                int bHops = b.Swaps.Sum(swap => swap.Route.Path.Count);
                return aHops - bHops;
            }
            return a.InputAmount.LessThan(b.InputAmount) ? -1 : 1;
        }
        return a.OutputAmount.LessThan(b.OutputAmount) ? 1 : -1;
    }
}
