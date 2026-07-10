using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;

namespace UniswapSharp.V3.Entities;

public class RouteInput<TInput, TOutput> where TOutput : BaseCurrency where TInput : BaseCurrency
{
    public required Route<TInput, TOutput> Route { get; init; }
    public required CurrencyAmount<TInput> InputAmount { get; init; }
    public required CurrencyAmount<TOutput> OutputAmount { get; init; }
}