using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using CoreUtils = UniswapSharp.Core.Utils;

namespace UniswapSharp.V2.Entities;

/// <summary>
/// Options for the best-trade search.
/// </summary>
public class BestTradeOptions
{
    /// <summary>How many results to return.</summary>
    public int MaxNumResults { get; set; } = 3;
    /// <summary>The maximum number of hops a trade should contain.</summary>
    public int MaxHops { get; set; } = 3;
}

/// <summary>
/// Port of v2-sdk <c>entities/trade.ts</c>.
/// Represents a trade executed against a list of pairs. Does not account for slippage, i.e. trades that front run
/// this trade and move the price.
/// </summary>
public class Trade<TInput, TOutput>
    where TInput : BaseCurrency
    where TOutput : BaseCurrency
{
    /// <summary>The route of the trade, i.e. which pairs the trade goes through and the input/output currencies.</summary>
    public Route<TInput, TOutput> Route { get; }
    /// <summary>The type of the trade, either exact in or exact out.</summary>
    public TradeType TradeType { get; }
    /// <summary>The input amount for the trade assuming no slippage.</summary>
    public CurrencyAmount<TInput> InputAmount { get; }
    /// <summary>The output amount for the trade assuming no slippage.</summary>
    public CurrencyAmount<TOutput> OutputAmount { get; }
    /// <summary>The price expressed in terms of output amount/input amount.</summary>
    public Price<TInput, TOutput> ExecutionPrice { get; }
    /// <summary>The percent difference between the mid price before the trade and the trade execution price.</summary>
    public Percent PriceImpact { get; }

    /// <summary>
    /// Constructs an exact in trade with the given amount in and route.
    /// </summary>
    public static Trade<TInput, TOutput> ExactIn(Route<TInput, TOutput> route, CurrencyAmount<TInput> amountIn)
    {
        return new Trade<TInput, TOutput>(route, amountIn.AsBaseCurrency()!, TradeType.EXACT_INPUT);
    }

    /// <summary>
    /// Constructs an exact out trade with the given amount out and route.
    /// </summary>
    public static Trade<TInput, TOutput> ExactOut(Route<TInput, TOutput> route, CurrencyAmount<TOutput> amountOut)
    {
        return new Trade<TInput, TOutput>(route, amountOut.AsBaseCurrency()!, TradeType.EXACT_OUTPUT);
    }

    public Trade(Route<TInput, TOutput> route, CurrencyAmount<BaseCurrency> amount, TradeType tradeType)
    {
        Route = route;
        TradeType = tradeType;

        var tokenAmounts = new CurrencyAmount<Token>[route.Path.Count];
        if (tradeType == TradeType.EXACT_INPUT)
        {
            if (!amount.Currency.Equals(route.Input))
            {
                throw new ArgumentException("INPUT");
            }
            tokenAmounts[0] = amount.Wrapped()!;
            for (var i = 0; i < route.Path.Count - 1; i++)
            {
                var pair = route.Pairs[i];
                var (outputAmount, _) = pair.GetOutputAmount(tokenAmounts[i]);
                tokenAmounts[i + 1] = outputAmount;
            }
            InputAmount = CurrencyAmount<TInput>.FromFractionalAmount(route.Input, amount.Numerator, amount.Denominator);
            OutputAmount = CurrencyAmount<TOutput>.FromFractionalAmount(
                route.Output,
                tokenAmounts[^1].Numerator,
                tokenAmounts[^1].Denominator
            );
        }
        else
        {
            if (!amount.Currency.Equals(route.Output))
            {
                throw new ArgumentException("OUTPUT");
            }
            tokenAmounts[^1] = amount.Wrapped()!;
            for (var i = route.Path.Count - 1; i > 0; i--)
            {
                var pair = route.Pairs[i - 1];
                var (inputAmount, _) = pair.GetInputAmount(tokenAmounts[i]);
                tokenAmounts[i - 1] = inputAmount;
            }
            InputAmount = CurrencyAmount<TInput>.FromFractionalAmount(
                route.Input,
                tokenAmounts[0].Numerator,
                tokenAmounts[0].Denominator
            );
            OutputAmount = CurrencyAmount<TOutput>.FromFractionalAmount(route.Output, amount.Numerator, amount.Denominator);
        }

        ExecutionPrice = new Price<TInput, TOutput>(
            InputAmount.Currency,
            OutputAmount.Currency,
            InputAmount.Quotient,
            OutputAmount.Quotient
        );
        PriceImpact = CoreUtils.PriceImpact.Compute(route.MidPrice, InputAmount, OutputAmount);
    }

    /// <summary>
    /// Get the minimum amount that must be received from this trade for the given slippage tolerance.
    /// </summary>
    public CurrencyAmount<TOutput> MinimumAmountOut(Percent slippageTolerance)
    {
        if (slippageTolerance.LessThan(Constants.ZERO))
        {
            throw new ArgumentException("SLIPPAGE_TOLERANCE");
        }
        if (TradeType == TradeType.EXACT_OUTPUT)
        {
            return OutputAmount;
        }

        var slippageAdjustedAmountOut = new Fraction(Constants.ONE)
            .Subtract(slippageTolerance)
            .Multiply(OutputAmount.Quotient)
            .Quotient;
        var clampedAmount = slippageAdjustedAmountOut > Constants.ZERO ? slippageAdjustedAmountOut : Constants.ZERO;
        return CurrencyAmount<TOutput>.FromRawAmount(OutputAmount.Currency, clampedAmount);
    }

    /// <summary>
    /// Get the maximum amount in that can be spent via this trade for the given slippage tolerance.
    /// </summary>
    public CurrencyAmount<TInput> MaximumAmountIn(Percent slippageTolerance)
    {
        if (slippageTolerance.LessThan(Constants.ZERO))
        {
            throw new ArgumentException("SLIPPAGE_TOLERANCE");
        }
        if (TradeType == TradeType.EXACT_INPUT)
        {
            return InputAmount;
        }

        var slippageAdjustedAmountIn = new Fraction(Constants.ONE)
            .Add(slippageTolerance)
            .Multiply(InputAmount.Quotient)
            .Quotient;
        return CurrencyAmount<TInput>.FromRawAmount(InputAmount.Currency, slippageAdjustedAmountIn);
    }

    /// <summary>
    /// Return the execution price after accounting for slippage tolerance.
    /// </summary>
    public Price<TInput, TOutput> WorstExecutionPrice(Percent slippageTolerance)
    {
        return new Price<TInput, TOutput>(
            InputAmount.Currency,
            OutputAmount.Currency,
            MaximumAmountIn(slippageTolerance).Quotient,
            MinimumAmountOut(slippageTolerance).Quotient
        );
    }

    /// <summary>
    /// Given a list of pairs, and a fixed amount in, returns the top <c>maxNumResults</c> trades that go from an input
    /// token amount to an output token, making at most <c>maxHops</c> hops.
    /// </summary>
    public static List<Trade<TInput, TOutput>> BestTradeExactIn(
        List<Pair> pairs,
        CurrencyAmount<TInput> currencyAmountIn,
        TOutput currencyOut,
        BestTradeOptions? options = null,
        List<Pair>? currentPairs = null,
        CurrencyAmount<BaseCurrency>? nextAmountIn = null,
        List<Trade<TInput, TOutput>>? bestTrades = null)
    {
        options ??= new BestTradeOptions();
        currentPairs ??= new List<Pair>();
        nextAmountIn ??= currencyAmountIn.AsBaseCurrency()!;
        bestTrades ??= new List<Trade<TInput, TOutput>>();

        if (pairs.Count == 0) throw new ArgumentException("PAIRS");
        if (options.MaxHops <= 0) throw new ArgumentException("MAX_HOPS");
        if (!(currencyAmountIn.Equals(nextAmountIn) || currentPairs.Count > 0)) throw new ArgumentException("INVALID_RECURSION");

        var amountIn = nextAmountIn.Wrapped()!;
        var tokenOut = currencyOut.Wrapped();
        for (var i = 0; i < pairs.Count; i++)
        {
            var pair = pairs[i];
            // pair irrelevant
            if (!pair.Token0.Equals(amountIn.Currency) && !pair.Token1.Equals(amountIn.Currency)) continue;
            if (pair.Reserve0.Quotient.IsZero || pair.Reserve1.Quotient.IsZero) continue;

            CurrencyAmount<Token> amountOut;
            try
            {
                (amountOut, _) = pair.GetOutputAmount(amountIn);
            }
            catch (InsufficientInputAmountError)
            {
                // input too low
                continue;
            }

            // we have arrived at the output token, so this is the final trade of one of the paths
            if (amountOut.Currency.Equals(tokenOut))
            {
                CoreUtils.SortedInsert.Insert(
                    bestTrades,
                    new Trade<TInput, TOutput>(
                        new Route<TInput, TOutput>(currentPairs.Concat(new[] { pair }).ToList(), currencyAmountIn.Currency, currencyOut),
                        currencyAmountIn.AsBaseCurrency()!,
                        TradeType.EXACT_INPUT
                    ),
                    options.MaxNumResults,
                    TradeComparator
                );
            }
            else if (options.MaxHops > 1 && pairs.Count > 1)
            {
                var pairsExcludingThisPair = pairs.Take(i).Concat(pairs.Skip(i + 1)).ToList();

                // otherwise, consider all the other paths that lead from this token as long as we have not exceeded maxHops
                BestTradeExactIn(
                    pairsExcludingThisPair,
                    currencyAmountIn,
                    currencyOut,
                    new BestTradeOptions { MaxNumResults = options.MaxNumResults, MaxHops = options.MaxHops - 1 },
                    currentPairs.Concat(new[] { pair }).ToList(),
                    amountOut.AsBaseCurrency()!,
                    bestTrades
                );
            }
        }

        return bestTrades;
    }

    /// <summary>
    /// Similar to <see cref="BestTradeExactIn"/> but instead targets a fixed output amount.
    /// </summary>
    public static List<Trade<TInput, TOutput>> BestTradeExactOut(
        List<Pair> pairs,
        TInput currencyIn,
        CurrencyAmount<TOutput> currencyAmountOut,
        BestTradeOptions? options = null,
        List<Pair>? currentPairs = null,
        CurrencyAmount<BaseCurrency>? nextAmountOut = null,
        List<Trade<TInput, TOutput>>? bestTrades = null)
    {
        options ??= new BestTradeOptions();
        currentPairs ??= new List<Pair>();
        nextAmountOut ??= currencyAmountOut.AsBaseCurrency()!;
        bestTrades ??= new List<Trade<TInput, TOutput>>();

        if (pairs.Count == 0) throw new ArgumentException("PAIRS");
        if (options.MaxHops <= 0) throw new ArgumentException("MAX_HOPS");
        if (!(currencyAmountOut.Equals(nextAmountOut) || currentPairs.Count > 0)) throw new ArgumentException("INVALID_RECURSION");

        var amountOut = nextAmountOut.Wrapped()!;
        var tokenIn = currencyIn.Wrapped();
        for (var i = 0; i < pairs.Count; i++)
        {
            var pair = pairs[i];
            // pair irrelevant
            if (!pair.Token0.Equals(amountOut.Currency) && !pair.Token1.Equals(amountOut.Currency)) continue;
            if (pair.Reserve0.Quotient.IsZero || pair.Reserve1.Quotient.IsZero) continue;

            CurrencyAmount<Token> amountIn;
            try
            {
                (amountIn, _) = pair.GetInputAmount(amountOut);
            }
            catch (InsufficientReservesError)
            {
                // not enough liquidity in this pair
                continue;
            }

            // we have arrived at the input token, so this is the first trade of one of the paths
            if (amountIn.Currency.Equals(tokenIn))
            {
                CoreUtils.SortedInsert.Insert(
                    bestTrades,
                    new Trade<TInput, TOutput>(
                        new Route<TInput, TOutput>(new[] { pair }.Concat(currentPairs).ToList(), currencyIn, currencyAmountOut.Currency),
                        currencyAmountOut.AsBaseCurrency()!,
                        TradeType.EXACT_OUTPUT
                    ),
                    options.MaxNumResults,
                    TradeComparator
                );
            }
            else if (options.MaxHops > 1 && pairs.Count > 1)
            {
                var pairsExcludingThisPair = pairs.Take(i).Concat(pairs.Skip(i + 1)).ToList();

                // otherwise, consider all the other paths that arrive at this token as long as we have not exceeded maxHops
                BestTradeExactOut(
                    pairsExcludingThisPair,
                    currencyIn,
                    currencyAmountOut,
                    new BestTradeOptions { MaxNumResults = options.MaxNumResults, MaxHops = options.MaxHops - 1 },
                    new[] { pair }.Concat(currentPairs).ToList(),
                    amountIn.AsBaseCurrency()!,
                    bestTrades
                );
            }
        }

        return bestTrades;
    }

    // comparator function that allows sorting trades by their output amounts, in decreasing order, and then input
    // amounts in increasing order. i.e. the best trades have the most outputs for the least inputs and are sorted first
    public static int InputOutputComparator(Trade<TInput, TOutput> a, Trade<TInput, TOutput> b)
    {
        // must have same input and output token for comparison
        if (!a.InputAmount.Currency.Equals(b.InputAmount.Currency)) throw new InvalidOperationException("INPUT_CURRENCY");
        if (!a.OutputAmount.Currency.Equals(b.OutputAmount.Currency)) throw new InvalidOperationException("OUTPUT_CURRENCY");
        if (a.OutputAmount.Equals(b.OutputAmount))
        {
            if (a.InputAmount.Equals(b.InputAmount))
            {
                return 0;
            }
            // trade A requires less input than trade B, so A should come first
            return a.InputAmount.LessThan(b.InputAmount) ? -1 : 1;
        }
        // tradeA has less output than trade B, so should come second
        return a.OutputAmount.LessThan(b.OutputAmount) ? 1 : -1;
    }

    // extension of the input output comparator that also considers other dimensions of the trade in ranking them
    public static int TradeComparator(Trade<TInput, TOutput> a, Trade<TInput, TOutput> b)
    {
        var ioComp = InputOutputComparator(a, b);
        if (ioComp != 0)
        {
            return ioComp;
        }

        // consider lowest slippage next, since these are less likely to fail
        if (a.PriceImpact.LessThan(b.PriceImpact))
        {
            return -1;
        }
        if (a.PriceImpact.GreaterThan(b.PriceImpact))
        {
            return 1;
        }

        // finally consider the number of hops since each hop costs gas
        return a.Route.Path.Count - b.Route.Path.Count;
    }
}
