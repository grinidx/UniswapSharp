using System.Numerics;
using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.Core.Utils;
using UniswapSharp.V4.Utils;

namespace UniswapSharp.V4.Entities;

/// <summary>
/// Represents a V4 trade executed against a set of routes where some percentage of the input is
/// split across each route. Ported from v4-sdk/src/entities/trade.ts.
///
/// Each route has its own set of pools. Pools can not be re-used across routes. Does not account
/// for slippage, i.e. changes in the price environment between submission and execution.
/// </summary>
public class Trade<TInput, TOutput>
    where TInput : BaseCurrency
    where TOutput : BaseCurrency
{
    /// <summary>
    /// Deprecated in favour of <see cref="Swaps"/>. When the trade consists of a single route this
    /// returns that route; otherwise it throws MULTIPLE_ROUTES.
    /// </summary>
    public Route<TInput, TOutput> Route
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

    /// <summary>The swaps of the trade, i.e. which routes and how much is swapped in each.</summary>
    public List<Swap<TInput, TOutput>> Swaps { get; }

    /// <summary>The type of the trade, either exact in or exact out.</summary>
    public TradeType TradeType { get; }

    private CurrencyAmount<TInput>? _inputAmount;
    private CurrencyAmount<TOutput>? _outputAmount;
    private Price<TInput, TOutput>? _executionPrice;
    private Percent? _priceImpact;

    /// <summary>The input amount for the trade assuming no slippage.</summary>
    public CurrencyAmount<TInput> InputAmount
    {
        get
        {
            if (_inputAmount != null)
            {
                return _inputAmount;
            }

            var inputCurrency = Swaps[0].InputAmount.Currency;
            var totalInputFromRoutes = Swaps.Aggregate(
                CurrencyAmount<TInput>.FromRawAmount(inputCurrency, 0),
                (total, swap) => total.Add(swap.InputAmount));

            _inputAmount = totalInputFromRoutes;
            return _inputAmount;
        }
    }

    /// <summary>The output amount for the trade assuming no slippage.</summary>
    public CurrencyAmount<TOutput> OutputAmount
    {
        get
        {
            if (_outputAmount != null)
            {
                return _outputAmount;
            }

            var outputCurrency = Swaps[0].OutputAmount.Currency;
            var totalOutputFromRoutes = Swaps.Aggregate(
                CurrencyAmount<TOutput>.FromRawAmount(outputCurrency, 0),
                (total, swap) => total.Add(swap.OutputAmount));

            _outputAmount = totalOutputFromRoutes;
            return _outputAmount;
        }
    }

    /// <summary>The price expressed in terms of output amount/input amount.</summary>
    public Price<TInput, TOutput> ExecutionPrice =>
        _executionPrice ??= new Price<TInput, TOutput>(
            InputAmount.Currency,
            OutputAmount.Currency,
            InputAmount.Quotient,
            OutputAmount.Quotient);

    /// <summary>Returns the percent difference between the route's mid price and the execution price.</summary>
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

    /// <summary>Constructs an exact in trade with the given amount in and route.</summary>
    public static async Task<Trade<TInput, TOutput>> ExactIn(
        Route<TInput, TOutput> route,
        CurrencyAmount<TInput> amountIn)
    {
        return await FromRoute<TInput>(route, amountIn, TradeType.EXACT_INPUT);
    }

    /// <summary>Constructs an exact out trade with the given amount out and route.</summary>
    public static async Task<Trade<TInput, TOutput>> ExactOut(
        Route<TInput, TOutput> route,
        CurrencyAmount<TOutput> amountOut)
    {
        return await FromRoute(route, amountOut, TradeType.EXACT_OUTPUT);
    }

    /// <summary>Constructs a trade by simulating swaps through the given route.</summary>
    public static async Task<Trade<TInput, TOutput>> FromRoute<TAmount>(
        Route<TInput, TOutput> route,
        CurrencyAmount<TAmount> amount,
        TradeType tradeType) where TAmount : BaseCurrency
    {
        CurrencyAmount<TInput> inputAmount;
        CurrencyAmount<TOutput> outputAmount;

        if (tradeType == TradeType.EXACT_INPUT)
        {
            if (!amount.Currency.Equals(route.Input))
            {
                throw new ArgumentException("INPUT");
            }

            // Account for trades that wrap/unwrap as a first step.
            var tokenAmount = PathCurrency.AmountWithPathCurrency(amount.AsBaseCurrency()!, route.Pools[0]);
            for (int i = 0; i < route.Pools.Count; i++)
            {
                var pool = route.Pools[i];
                (tokenAmount, _) = await pool.GetOutputAmount(tokenAmount);
            }

            inputAmount = CurrencyAmount<TInput>.FromFractionalAmount(route.Input, amount.Numerator, amount.Denominator);
            outputAmount = CurrencyAmount<TOutput>.FromFractionalAmount(route.Output, tokenAmount.Numerator, tokenAmount.Denominator);
        }
        else
        {
            if (!amount.Currency.Equals(route.Output))
            {
                throw new ArgumentException("OUTPUT");
            }

            // Account for trades that wrap/unwrap as a last step.
            var tokenAmount = PathCurrency.AmountWithPathCurrency(amount.AsBaseCurrency()!, route.Pools[^1]);
            for (int i = route.Pools.Count - 1; i >= 0; i--)
            {
                var pool = route.Pools[i];
                (tokenAmount, _) = await pool.GetInputAmount(tokenAmount);

                // Special case: if this is the last pool (first in backward iteration) and it's an
                // ETH-WETH pool, convert the amount between native/wrapped so the next pool works.
                if (i == route.Pools.Count - 1)
                {
                    bool isEthWethPool = pool.Currency1.Equals(pool.Currency0.Wrapped());
                    if (isEthWethPool)
                    {
                        var previousPool = i - 1 >= 0 ? route.Pools[i - 1] : null;
                        if (route.Output.IsNative && previousPool != null && previousPool.Currency0.IsNative)
                        {
                            // Convert WETH amount to ETH for the next pool.
                            tokenAmount = CurrencyAmount<BaseCurrency>.FromFractionalAmount(
                                pool.Currency0, tokenAmount.Numerator, tokenAmount.Denominator);
                        }
                        else if (route.Output.Equals(pool.Currency1) && previousPool != null && !previousPool.Currency0.IsNative)
                        {
                            // Convert ETH amount to WETH for the next pool.
                            tokenAmount = CurrencyAmount<BaseCurrency>.FromFractionalAmount(
                                pool.Currency1, tokenAmount.Numerator, tokenAmount.Denominator);
                        }
                    }
                }
            }

            inputAmount = CurrencyAmount<TInput>.FromFractionalAmount(route.Input, tokenAmount.Numerator, tokenAmount.Denominator);
            outputAmount = CurrencyAmount<TOutput>.FromFractionalAmount(route.Output, amount.Numerator, amount.Denominator);
        }

        return new Trade<TInput, TOutput>(
            new List<Swap<TInput, TOutput>> { new(route, inputAmount, outputAmount) },
            tradeType);
    }

    /// <summary>Constructs a trade from routes by simulating swaps.</summary>
    public static async Task<Trade<TInput, TOutput>> FromRoutes<TAmount>(
        List<(CurrencyAmount<TAmount> amount, Route<TInput, TOutput> route)> routes,
        TradeType tradeType) where TAmount : BaseCurrency
    {
        var swaps = new List<Swap<TInput, TOutput>>();
        foreach (var (amount, route) in routes)
        {
            var trade = await FromRoute(route, amount, tradeType);
            swaps.Add(trade.Swaps[0]);
        }

        return new Trade<TInput, TOutput>(swaps, tradeType);
    }

    /// <summary>
    /// Creates a trade without computing the result of swapping through the route. Useful when the
    /// trade has been simulated elsewhere and there is no tick data.
    /// </summary>
    public static Trade<TInput, TOutput> CreateUncheckedTrade(RouteInput<TInput, TOutput> routeInput, TradeType tradeType)
    {
        return new Trade<TInput, TOutput>(
            new List<Swap<TInput, TOutput>> { new(routeInput.Route, routeInput.InputAmount, routeInput.OutputAmount) },
            tradeType);
    }

    /// <summary>
    /// Creates a trade without computing the result of swapping through the routes. Useful when the
    /// trade has been simulated elsewhere and there is no tick data.
    /// </summary>
    public static Trade<TInput, TOutput> CreateUncheckedTradeWithMultipleRoutes(List<RouteInput<TInput, TOutput>> routes, TradeType tradeType)
    {
        var swaps = routes.Select(r => new Swap<TInput, TOutput>(r.Route, r.InputAmount, r.OutputAmount)).ToList();
        return new Trade<TInput, TOutput>(swaps, tradeType);
    }

    private Trade(List<Swap<TInput, TOutput>> swaps, TradeType tradeType)
    {
        var inputCurrency = swaps[0].InputAmount.Currency;
        var outputCurrency = swaps[0].OutputAmount.Currency;

        if (!swaps.All(swap => inputCurrency.Equals(swap.Route.Input)))
        {
            throw new ArgumentException("INPUT_CURRENCY_MATCH");
        }

        if (!swaps.All(swap => outputCurrency.Equals(swap.Route.Output)))
        {
            throw new ArgumentException("OUTPUT_CURRENCY_MATCH");
        }

        var numPools = swaps.Sum(swap => swap.Route.Pools.Count);
        var poolIdSet = new HashSet<string>();
        foreach (var swap in swaps)
        {
            foreach (var pool in swap.Route.Pools)
            {
                poolIdSet.Add(Pool.GetPoolId(pool.Currency0, pool.Currency1, pool.Fee, pool.TickSpacing, pool.Hooks));
            }
        }

        if (numPools != poolIdSet.Count)
        {
            throw new ArgumentException("POOLS_DUPLICATED");
        }

        Swaps = swaps;
        TradeType = tradeType;
    }

    /// <summary>The minimum amount that must be received for the given slippage tolerance.</summary>
    public CurrencyAmount<TOutput> MinimumAmountOut(Percent slippageTolerance, CurrencyAmount<TOutput>? amountOut = null)
    {
        if (slippageTolerance.LessThan(BigInteger.Zero))
        {
            throw new ArgumentException("SLIPPAGE_TOLERANCE");
        }

        amountOut ??= OutputAmount;

        if (TradeType == TradeType.EXACT_OUTPUT)
        {
            return amountOut;
        }

        var slippageAdjustedAmountOut = new Fraction(1)
            .Subtract(slippageTolerance)
            .Multiply(amountOut.Quotient)
            .Quotient;
        var clampedAmount = slippageAdjustedAmountOut > BigInteger.Zero ? slippageAdjustedAmountOut : BigInteger.Zero;
        return CurrencyAmount<TOutput>.FromRawAmount(amountOut.Currency, clampedAmount);
    }

    /// <summary>The maximum amount in that can be spent for the given slippage tolerance.</summary>
    public CurrencyAmount<TInput> MaximumAmountIn(Percent slippageTolerance, CurrencyAmount<TInput>? amountIn = null)
    {
        if (slippageTolerance.LessThan(BigInteger.Zero))
        {
            throw new ArgumentException("SLIPPAGE_TOLERANCE");
        }

        amountIn ??= InputAmount;

        if (TradeType == TradeType.EXACT_INPUT)
        {
            return amountIn;
        }

        var slippageAdjustedAmountIn = new Fraction(1)
            .Add(slippageTolerance)
            .Multiply(amountIn.Quotient)
            .Quotient;
        return CurrencyAmount<TInput>.FromRawAmount(amountIn.Currency, slippageAdjustedAmountIn);
    }

    /// <summary>The execution price after accounting for slippage tolerance.</summary>
    public Price<TInput, TOutput> WorstExecutionPrice(Percent slippageTolerance)
    {
        return new Price<TInput, TOutput>(
            InputAmount.Currency,
            OutputAmount.Currency,
            MaximumAmountIn(slippageTolerance).Quotient,
            MinimumAmountOut(slippageTolerance).Quotient);
    }

    /// <summary>
    /// Given a list of pools and a fixed amount in, returns the top <c>maxNumResults</c> trades that go from an
    /// input currency amount to an output currency, making at most <c>maxHops</c> hops.
    /// </summary>
    public static async Task<List<Trade<TInput, TOutput>>> BestTradeExactIn(
        List<Pool> pools,
        CurrencyAmount<TInput> currencyAmountIn,
        TOutput currencyOut,
        BestTradeOptions? options = null,
        List<Pool>? currentPools = null,
        CurrencyAmount<BaseCurrency>? nextAmountIn = null,
        List<Trade<TInput, TOutput>>? bestTrades = null)
    {
        options ??= new BestTradeOptions();
        currentPools ??= new List<Pool>();
        nextAmountIn ??= currencyAmountIn.AsBaseCurrency();
        bestTrades ??= new List<Trade<TInput, TOutput>>();

        if (pools.Count == 0) throw new ArgumentException("POOLS");
        if (options.MaxHops <= 0) throw new ArgumentException("MAX_HOPS");
        if (!(currencyAmountIn.Equals(nextAmountIn) || currentPools.Count > 0)) throw new ArgumentException("INVALID_RECURSION");

        var amountIn = nextAmountIn!;
        for (int i = 0; i < pools.Count; i++)
        {
            var pool = pools[i];
            // pool irrelevant
            if (!pool.Currency0.Equals(amountIn.Currency) && !pool.Currency1.Equals(amountIn.Currency)) continue;

            CurrencyAmount<BaseCurrency> amountOut;
            try
            {
                (amountOut, _) = await pool.GetOutputAmount(amountIn);
            }
            catch (ArgumentException)
            {
                // input too low / swap infeasible for this pool
                continue;
            }

            // we have arrived at the output currency, so this is the final trade of one of the paths
            if (amountOut.Currency.Equals(currencyOut))
            {
                var newRoute = new Route<TInput, TOutput>(
                    currentPools.Concat(new[] { pool }).ToList(), currencyAmountIn.Currency, currencyOut);
                var newTrade = await FromRoute(newRoute, currencyAmountIn, TradeType.EXACT_INPUT);
                SortedInsert.Insert(bestTrades, newTrade, options.MaxNumResults, TradeComparator);
            }
            else if (options.MaxHops > 1 && pools.Count > 1)
            {
                var poolsExcludingThisPool = pools.Take(i).Concat(pools.Skip(i + 1)).ToList();

                // otherwise, consider all the other paths that lead from this currency as long as we have not exceeded maxHops
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
    /// Similar to <see cref="BestTradeExactIn"/> but targets a fixed output amount. Given a list of pools and a
    /// fixed amount out, returns the top <c>maxNumResults</c> trades that go from an input currency to an output
    /// currency amount, making at most <c>maxHops</c> hops.
    /// </summary>
    public static async Task<List<Trade<TInput, TOutput>>> BestTradeExactOut(
        List<Pool> pools,
        TInput currencyIn,
        CurrencyAmount<TOutput> currencyAmountOut,
        BestTradeOptions? options = null,
        List<Pool>? currentPools = null,
        CurrencyAmount<BaseCurrency>? nextAmountOut = null,
        List<Trade<TInput, TOutput>>? bestTrades = null)
    {
        options ??= new BestTradeOptions();
        currentPools ??= new List<Pool>();
        nextAmountOut ??= currencyAmountOut.AsBaseCurrency();
        bestTrades ??= new List<Trade<TInput, TOutput>>();

        if (pools.Count == 0) throw new ArgumentException("POOLS");
        if (options.MaxHops <= 0) throw new ArgumentException("MAX_HOPS");
        if (!(currencyAmountOut.Equals(nextAmountOut) || currentPools.Count > 0)) throw new ArgumentException("INVALID_RECURSION");

        var amountOut = nextAmountOut!;
        for (int i = 0; i < pools.Count; i++)
        {
            var pool = pools[i];
            // pool irrelevant
            if (!pool.Currency0.Equals(amountOut.Currency) && !pool.Currency1.Equals(amountOut.Currency)) continue;

            CurrencyAmount<BaseCurrency> amountIn;
            try
            {
                (amountIn, _) = await pool.GetInputAmount(amountOut);
            }
            catch (ArgumentException)
            {
                // not enough liquidity in this pool / swap infeasible
                continue;
            }

            // we have arrived at the input currency, so this is the first trade of one of the paths
            if (amountIn.Currency.Equals(currencyIn))
            {
                var newRoute = new Route<TInput, TOutput>(
                    new[] { pool }.Concat(currentPools).ToList(), currencyIn, currencyAmountOut.Currency);
                var newTrade = await FromRoute(newRoute, currencyAmountOut, TradeType.EXACT_OUTPUT);
                SortedInsert.Insert(bestTrades, newTrade, options.MaxNumResults, TradeComparator);
            }
            else if (options.MaxHops > 1 && pools.Count > 1)
            {
                var poolsExcludingThisPool = pools.Take(i).Concat(pools.Skip(i + 1)).ToList();

                // otherwise, consider all the other paths that arrive at this currency as long as we have not exceeded maxHops
                await BestTradeExactOut(
                    poolsExcludingThisPool,
                    currencyIn,
                    currencyAmountOut,
                    new BestTradeOptions { MaxNumResults = options.MaxNumResults, MaxHops = options.MaxHops - 1 },
                    new[] { pool }.Concat(currentPools).ToList(),
                    amountIn,
                    bestTrades);
            }
        }

        return bestTrades;
    }

    /// <summary>
    /// Trades comparator, an extension of the input/output comparator that also considers other dimensions of
    /// the trade in ranking them. Ported from <c>tradeComparator</c> in v4-sdk/src/entities/trade.ts.
    /// </summary>
    public static int TradeComparator(Trade<TInput, TOutput> a, Trade<TInput, TOutput> b)
    {
        // must have same input and output currency for comparison
        if (!a.InputAmount.Currency.Equals(b.InputAmount.Currency))
            throw new InvalidOperationException("INPUT_CURRENCY");
        if (!a.OutputAmount.Currency.Equals(b.OutputAmount.Currency))
            throw new InvalidOperationException("OUTPUT_CURRENCY");

        if (a.OutputAmount.Equals(b.OutputAmount))
        {
            if (a.InputAmount.Equals(b.InputAmount))
            {
                // consider the number of hops since each hop costs gas
                int aHops = a.Swaps.Sum(swap => swap.Route.CurrencyPath.Count);
                int bHops = b.Swaps.Sum(swap => swap.Route.CurrencyPath.Count);
                return aHops - bHops;
            }
            // trade A requires less input than trade B, so A should come first
            return a.InputAmount.LessThan(b.InputAmount) ? -1 : 1;
        }

        // tradeA has less output than trade B, so should come second
        return a.OutputAmount.LessThan(b.OutputAmount) ? 1 : -1;
    }

    public class BestTradeOptions
    {
        public int MaxNumResults { get; set; } = 3;
        public int MaxHops { get; set; } = 3;
    }
}
