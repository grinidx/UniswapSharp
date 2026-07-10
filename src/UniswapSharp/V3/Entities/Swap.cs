using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;

namespace UniswapSharp.V3.Entities;

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