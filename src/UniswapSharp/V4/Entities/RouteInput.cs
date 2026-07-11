using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;

namespace UniswapSharp.V4.Entities;

/// <summary>
/// The pre-computed property values passed to the unchecked-trade factories. Ported from the
/// <c>constructorArguments</c> object of v4-sdk/src/entities/trade.ts.
/// </summary>
public class RouteInput<TInput, TOutput> where TOutput : BaseCurrency where TInput : BaseCurrency
{
    public required Route<TInput, TOutput> Route { get; init; }
    public required CurrencyAmount<TInput> InputAmount { get; init; }
    public required CurrencyAmount<TOutput> OutputAmount { get; init; }
}
