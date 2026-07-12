using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;

namespace UniswapSharp.V3.Entities;

public class Trade<TInput, TOutput>
    where TInput : BaseCurrency
    where TOutput : BaseCurrency

{

    public Route<TInput, TOutput> Route
    {
        get
        {
            if (Swaps.Count != 1)
            {
                throw new System.InvalidOperationException("Multiple Routes");
            }
            return Swaps[0].Route;
        }
    }

    public List<Swap<TInput, TOutput>> Swaps { get; }
    public TradeType TradeType { get; }

    private CurrencyAmount<TInput>? _inputAmount;
    private CurrencyAmount<TOutput>? _outputAmount;
    private Price<TInput, TOutput>? _executionPrice;
    private Percent? _priceImpact;


    public CurrencyAmount<TInput> InputAmount
    {
        get
        {
            if (_inputAmount != null)
            {
                return _inputAmount;
            }

            var inputCurrency = Swaps[0].InputAmount.Currency;
            var totalInputFromRoutes = Swaps
                .Aggregate(
                    CurrencyAmount<TInput>.FromRawAmount<TInput>(inputCurrency, 0),
                    (total, swap) => total.Add(swap.InputAmount)
                );

            _inputAmount = totalInputFromRoutes;
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
            var totalOutputFromRoutes = Swaps

                .Aggregate(CurrencyAmount<TOutput>.FromRawAmount<TOutput>(outputCurrency, 0),
                    (total, cur) => total.Add(cur.OutputAmount));

            _outputAmount = totalOutputFromRoutes;
            return _outputAmount;
        }
    }

    public Price<TInput, TOutput> ExecutionPrice
    {
        get
        {
            return _executionPrice ??= new Price<TInput, TOutput>(
                InputAmount.Currency,
                OutputAmount.Currency,
                InputAmount.Quotient,
                OutputAmount.Quotient
            );
        }
    }

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

    public static async Task<Trade<TInput, TOutput>> ExactIn(
        Route<TInput, TOutput> route,
        CurrencyAmount<TInput> amountIn)
    {
        return await FromRoute<TInput>(route, amountIn, TradeType.EXACT_INPUT);
    }

    public static async Task<Trade<TInput, TOutput>> ExactOut(
        Route<TInput, TOutput> route,
        CurrencyAmount<TOutput> amountOut)
    {
        return await FromRoute(route, amountOut, TradeType.EXACT_OUTPUT);
    }

    public static async Task<Trade<TInput, TOutput>> FromRoute<TAmount>(
        Route<TInput, TOutput> route,
        CurrencyAmount<TAmount> amount,
    TradeType tradeType
   ) where TAmount : BaseCurrency
    {
        var amounts = new CurrencyAmount<Token>[route.TokenPath.Count];
        CurrencyAmount<TInput> inputAmount;
        CurrencyAmount<TOutput> outputAmount;

        if (tradeType == TradeType.EXACT_INPUT)
        {
            if (!amount.Currency.Equals(route.Input))
            {
                throw new ArgumentException("INPUT");
            }

            amounts[0] = amount.Wrapped();
            for (int i = 0; i < route.TokenPath.Count - 1; i++)
            {
                var pool = route.Pools[i];
                var (outputAmountToken, _) = await pool.GetOutputAmount(amounts[i]);
                amounts[i + 1] = outputAmountToken;
            }

            inputAmount = CurrencyAmount<TInput>.FromFractionalAmount(route.Input, amount.Numerator, amount.Denominator);
            outputAmount = CurrencyAmount<TOutput>.FromFractionalAmount(
                route.Output,
                amounts[amounts.Length - 1].Numerator,
                amounts[amounts.Length - 1].Denominator
            );
        }
        else
        {
            if (!amount.Currency.Equals(route.Output))
            {
                throw new ArgumentException("OUTPUT");
            }

            amounts[amounts.Length - 1] = amount.Wrapped();
            for (int i = route.TokenPath.Count - 1; i > 0; i--)
            {
                var pool = route.Pools[i - 1];
                var (inputAmountToken, _) = await pool.GetInputAmount(amounts[i]);
                amounts[i - 1] = inputAmountToken;
            }

            inputAmount = CurrencyAmount<TInput>.FromFractionalAmount(route.Input, amounts[0].Numerator, amounts[0].Denominator);
            outputAmount = CurrencyAmount<TOutput>.FromFractionalAmount(route.Output, amount.Numerator, amount.Denominator);
        }

        return new Trade<TInput, TOutput>(new List<Swap<TInput, TOutput>>
        {
            new Swap<TInput,TOutput>(route, inputAmount, outputAmount)

        }, tradeType);
    }

    public static async Task<Trade<TInput, TOutput>> FromRoutes<TAmount>(
        List<(CurrencyAmount<TAmount> amount, Route<TInput, TOutput> route)> routes,
        TradeType tradeType) where TAmount : BaseCurrency
    {
        var populatedRoutes = new List<Swap<TInput, TOutput>>();

        foreach (var (amount, route) in routes)
        {
            var amounts = new CurrencyAmount<Token>[route.TokenPath.Count];
            CurrencyAmount<TInput> inputAmount;
            CurrencyAmount<TOutput> outputAmount;

            if (tradeType == TradeType.EXACT_INPUT)
            {
                if (!amount.Currency.Equals(route.Input))
                {
                    throw new ArgumentException("INPUT");
                }

                inputAmount = CurrencyAmount<TInput>.FromFractionalAmount(route.Input, amount.Numerator, amount.Denominator);
                amounts[0] = CurrencyAmount<TInput>.FromFractionalAmount(route.Input.Wrapped(), amount.Numerator, amount.Denominator);

                for (int i = 0; i < route.TokenPath.Count - 1; i++)
                {
                    var pool = route.Pools[i];
                    var (outputAmountToken, _) = await pool.GetOutputAmount(amounts[i]);
                    amounts[i + 1] = outputAmountToken;
                }

                outputAmount = CurrencyAmount<TOutput>.FromFractionalAmount(
                    route.Output,
                    amounts[amounts.Length - 1].Numerator,
                    amounts[amounts.Length - 1].Denominator
                );
            }
            else
            {
                if (!amount.Currency.Equals(route.Output))
                {
                    throw new ArgumentException("OUTPUT");
                }

                outputAmount = CurrencyAmount<TOutput>.FromFractionalAmount(route.Output, amount.Numerator, amount.Denominator);
                amounts[amounts.Length - 1] = CurrencyAmount<TOutput>.FromFractionalAmount(
                    route.Output.Wrapped(),
                    amount.Numerator,
                    amount.Denominator
                );

                for (int i = route.TokenPath.Count - 1; i > 0; i--)
                {
                    var pool = route.Pools[i - 1];
                    var (inputAmountToken, _) = await pool.GetInputAmount(amounts[i]);
                    amounts[i - 1] = inputAmountToken;
                }

                inputAmount = CurrencyAmount<TInput>.FromFractionalAmount(route.Input, amounts[0].Numerator, amounts[0].Denominator);
            }

            populatedRoutes.Add(new Swap<TInput, TOutput>(route, inputAmount, outputAmount));
        }

        return new Trade<TInput, TOutput>(populatedRoutes, tradeType);
    }

    public static Trade<TInput, TOutput> CreateUncheckedTrade(RouteInput<TInput, TOutput> routeInput, TradeType tradeType)
    {
        return new Trade<TInput, TOutput>(new List<Swap<TInput, TOutput>>
        {
            new Swap<TInput, TOutput>(routeInput.Route, routeInput.InputAmount, routeInput.OutputAmount)
        }, tradeType);
    }





    public static Trade<TInput, TOutput> CreateUncheckedTradeWithMultipleRoutes(List<RouteInput<TInput, TOutput>> routes, TradeType tradeType)
    {
        var swaps = routes.Select(r => new Swap<TInput, TOutput>(r.Route, r.InputAmount, r.OutputAmount)).ToList();
        return new Trade<TInput, TOutput>(swaps, tradeType);
    }

    private Trade(List<Swap<TInput, TOutput>> swaps, TradeType tradeType)
    {
        var inputCurrency = swaps[0].InputAmount.Currency;
        var outputCurrency = swaps[0].OutputAmount.Currency;

        if (!swaps.All(swap => inputCurrency.Wrapped().Equals(swap.Route.Input.Wrapped())))
        {
            throw new ArgumentException("INPUT_CURRENCY_MATCH");
        }

        if (!swaps.All(swap => outputCurrency.Wrapped().Equals(swap.Route.Output.Wrapped())))
        {
            throw new ArgumentException("OUTPUT_CURRENCY_MATCH");
        }

        var numPools = swaps.Sum(swap => swap.Route.Pools.Count);
        var poolAddressSet = new HashSet<string>();
        foreach (var swap in swaps)
        {
            foreach (var pool in swap.Route.Pools)
            {
                poolAddressSet.Add(Pool.GetAddress(pool.Token0, pool.Token1, pool.Fee));
            }
        }

        if (numPools != poolAddressSet.Count)
        {
            throw new ArgumentException("POOLS_DUPLICATED");
        }

        Swaps = swaps;
        TradeType = tradeType;
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
        else
        {
            var slippageAdjustedAmountOut = new Fraction(1)
                .Add(slippageTolerance)
                .Invert()
                .Multiply(amountOut.Quotient)
                .Quotient;
            return CurrencyAmount<TOutput>.FromRawAmount(amountOut.Currency, slippageAdjustedAmountOut);
        }
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
        else
        {
            var slippageAdjustedAmountIn = new Fraction(1)
                .Add(slippageTolerance)
                .Multiply(amountIn.Quotient)
                .Quotient;
            return CurrencyAmount<TInput>.FromRawAmount(amountIn.Currency, slippageAdjustedAmountIn);
        }
    }

    public Price<TInput, TOutput> WorstExecutionPrice(Percent slippageTolerance)
    {
        return new Price<TInput, TOutput>(
            InputAmount.Currency,
            OutputAmount.Currency,
            MaximumAmountIn(slippageTolerance).Quotient,
            MinimumAmountOut(slippageTolerance).Quotient
        );
    }

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
        if (options.MaxHops == 0) throw new ArgumentException("MAX_HOPS");

        if (!(currencyAmountIn.Equals(nextAmountIn) || currentPools.Count > 0)) throw new ArgumentException("INVALID_RECURSION");

        var amountIn = nextAmountIn?.Wrapped();
        var tokenOut = currencyOut.Wrapped();

        for (int i = 0; i < pools.Count; i++)
        {
            var pool = pools[i];
            if (!pool.Token0.Equals(amountIn?.Currency) && !pool.Token1.Equals(amountIn?.Currency)) continue;



            CurrencyAmount<Token> amountOut;
            try
            {
                (amountOut, _) = await pool.GetOutputAmount(amountIn);
            }
            catch (Exception)
            {
                // upstream swallows InsufficientInputAmountError and continues; that error type isn't ported, so rethrow.
                throw;
            }


            if (amountOut.Currency.IsToken && amountOut.Currency.Equals(tokenOut))
            {

                var newRoute = new Route<TInput, TOutput>(currentPools.Concat(new[] { pool }).ToList(), currencyAmountIn.Currency, currencyOut);
                var newTrade = await Trade<TInput, TOutput>.FromRoute(newRoute, currencyAmountIn, TradeType.EXACT_INPUT);
                SortedInsert(bestTrades, newTrade, options.MaxNumResults, TradeComparator);


            }
            else if (options.MaxHops > 1 && pools.Count > 1)
            {
                var poolsExcludingThisPool = pools.Take(i).Concat(pools.Skip(i + 1)).ToList();

                await BestTradeExactIn(
                    poolsExcludingThisPool,
                    currencyAmountIn,
                    currencyOut,
                    new BestTradeOptions
                    {
                        MaxNumResults = options.MaxNumResults,
                        MaxHops = options.MaxHops - 1
                    },
                    currentPools.Concat(new[] { pool }).ToList(),
                    amountOut.AsBaseCurrency(),
                    bestTrades
                );
            }
        }

        return bestTrades;
    }

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

        var amountOut = nextAmountOut.Wrapped();
        var tokenIn = currencyIn.Wrapped();

        for (int i = 0; i < pools.Count; i++)
        {
            var pool = pools[i];
            if (!pool.Token0.Equals(amountOut.Currency) && !pool.Token1.Equals(amountOut.Currency)) continue;

            CurrencyAmount<Token> amountIn;
            try
            {
                (amountIn, _) = await pool.GetInputAmount(amountOut);
            }
            catch (Exception)
            {
                // upstream swallows InsufficientReservesError and continues; that error type isn't ported, so rethrow.
                throw;
            }

            if (amountIn.Currency.Equals(tokenIn))
            {
                var newRoute = new Route<TInput, TOutput>(new[] { pool }.Concat(currentPools).ToList(), currencyIn, currencyAmountOut.Currency);
                var newTrade = await Trade<TInput, TOutput>.FromRoute(newRoute, currencyAmountOut, TradeType.EXACT_OUTPUT);
                SortedInsert(bestTrades, newTrade, options.MaxNumResults, TradeComparator);
            }
            else if (options.MaxHops > 1 && pools.Count > 1)
            {
                var poolsExcludingThisPool = pools.Take(i).Concat(pools.Skip(i + 1)).ToList();

                await BestTradeExactOut(
                    poolsExcludingThisPool,
                    currencyIn,
                    currencyAmountOut,
                    new BestTradeOptions
                    {
                        MaxNumResults = options.MaxNumResults,
                        MaxHops = options.MaxHops - 1
                    },
                    new[] { pool }.Concat(currentPools).ToList(),
                    amountIn.AsBaseCurrency(),
                    bestTrades
                );
            }
        }

        return bestTrades;
    }

    private static void SortedInsert<T>(List<T> list, T element, int maxSize, Comparison<T> comparator)
    {
        if (list.Count < maxSize)
        {
            list.Add(element);
            list.Sort(comparator);
        }
        else if (comparator(element, list[^1]) < 0)
        {
            list[^1] = element;
            list.Sort(comparator);
        }
    }



    public static int TradeComparator(
        Trade<TInput, TOutput> a,
        Trade<TInput, TOutput> b)


    {
        // must have same input and output token for comparison
        if (!a.InputAmount.Currency.Equals(b.InputAmount.Currency))
            throw new InvalidOperationException("INPUT_CURRENCY");
        if (!a.OutputAmount.Currency.Equals(b.OutputAmount.Currency))
            throw new InvalidOperationException("OUTPUT_CURRENCY");

        if (a.OutputAmount.Equals(b.OutputAmount))
        {
            if (a.InputAmount.Equals(b.InputAmount))
            {
                // consider the number of hops since each hop costs gas
                int aHops = a.Swaps.Sum(swap => swap.Route.TokenPath.Count);
                int bHops = b.Swaps.Sum(swap => swap.Route.TokenPath.Count);
                return aHops - bHops;
            }
            // trade A requires less input than trade B, so A should come first
            if (a.InputAmount.LessThan(b.InputAmount))
            {
                return -1;
            }
            else
            {
                return 1;
            }
        }
        else
        {
            // tradeA has less output than trade B, so should come second
            if (a.OutputAmount.LessThan(b.OutputAmount))
            {
                return 1;
            }
            else
            {
                return -1;
            }
        }
    }


    public class BestTradeOptions
    {
        public int MaxNumResults { get; set; } = 3;
        public int MaxHops { get; set; } = 3;
    }


}
