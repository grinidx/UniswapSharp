using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;

namespace UniswapSharp.V4.Entities;

/// <summary>
/// One leg of a <see cref="Trade{TInput,TOutput}"/>: a route and the amounts swapped in and out of it.
/// Ported from the anonymous <c>swaps</c> object in v4-sdk/src/entities/trade.ts.
/// </summary>
public class Swap<TInput, TOutput> where TInput : BaseCurrency where TOutput : BaseCurrency
{
    public Route<TInput, TOutput> Route { get; }
    public CurrencyAmount<TInput> InputAmount { get; }
    public CurrencyAmount<TOutput> OutputAmount { get; }

    public Swap(Route<TInput, TOutput> route, CurrencyAmount<TInput> inputAmount, CurrencyAmount<TOutput> outputAmount)
    {
        Route = route;
        InputAmount = inputAmount;
        OutputAmount = outputAmount;
    }
}
