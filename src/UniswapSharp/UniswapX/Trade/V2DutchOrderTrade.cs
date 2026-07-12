using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.UniswapX.Order;

namespace UniswapSharp.UniswapX.Trade;

/// <summary>Port of uniswapx-sdk <c>trade/V2DutchOrderTrade.ts</c>.</summary>
public sealed class V2DutchOrderTrade<TInput, TOutput>
    where TInput : BaseCurrency
    where TOutput : BaseCurrency
{
    public TradeType TradeType { get; }
    public UnsignedV2DutchOrder Order { get; }

    private readonly TInput _currencyIn;
    private readonly IReadOnlyList<TOutput> _currenciesOut;

    private CurrencyAmount<TInput>? _inputAmount;
    private IReadOnlyList<CurrencyAmount<TOutput>>? _outputAmounts;
    private (CurrencyAmount<TOutput> Start, CurrencyAmount<TOutput> End)? _firstNonFeeOutputStartEndAmounts;
    private Price<TInput, TOutput>? _executionPrice;

    public V2DutchOrderTrade(TInput currencyIn, IReadOnlyList<TOutput> currenciesOut, UnsignedV2DutchOrderInfo orderInfo, TradeType tradeType)
    {
        _currencyIn = currencyIn;
        _currenciesOut = currenciesOut;
        TradeType = tradeType;
        Order = new UnsignedV2DutchOrder(orderInfo, currencyIn.ChainId);
    }

    public CurrencyAmount<TInput> InputAmount =>
        _inputAmount ??= CurrencyAmount<TInput>.FromRawAmount(_currencyIn, Order.Info.Input.StartAmount);

    public IReadOnlyList<CurrencyAmount<TOutput>> OutputAmounts =>
        _outputAmounts ??= Order.Info.Outputs.Select(output =>
            CurrencyAmount<TOutput>.FromRawAmount(FindCurrencyOut(output.Token), output.StartAmount)).ToList();

    private (CurrencyAmount<TOutput> Start, CurrencyAmount<TOutput> End) GetFirstNonFeeOutputStartEndAmounts()
    {
        if (_firstNonFeeOutputStartEndAmounts is { } cached)
        {
            return cached;
        }
        if (Order.Info.Outputs.Count == 0)
        {
            throw new InvalidOperationException("there must be at least one output token");
        }
        var output = Order.Info.Outputs[0];
        var currencyOut = FindCurrencyOut(output.Token, "currency output from order must exist in currenciesOut list");
        var result = (
            CurrencyAmount<TOutput>.FromRawAmount(currencyOut, output.StartAmount),
            CurrencyAmount<TOutput>.FromRawAmount(currencyOut, output.EndAmount));
        _firstNonFeeOutputStartEndAmounts = result;
        return result;
    }

    public CurrencyAmount<TOutput> OutputAmount => GetFirstNonFeeOutputStartEndAmounts().Start;

    public CurrencyAmount<TOutput> MinimumAmountOut() => GetFirstNonFeeOutputStartEndAmounts().End;

    public CurrencyAmount<TInput> MaximumAmountIn() =>
        CurrencyAmount<TInput>.FromRawAmount(_currencyIn, Order.Info.Input.EndAmount);

    public Price<TInput, TOutput> ExecutionPrice =>
        _executionPrice ??= new Price<TInput, TOutput>(
            InputAmount.Currency, OutputAmount.Currency, InputAmount.Quotient, OutputAmount.Quotient);

    public Price<TInput, TOutput> WorstExecutionPrice() =>
        new(InputAmount.Currency, OutputAmount.Currency, MaximumAmountIn().Quotient, MinimumAmountOut().Quotient);

    private TOutput FindCurrencyOut(string token, string error = "currency not found in output array")
    {
        foreach (var currency in _currenciesOut)
        {
            if (TradeUtils.AreCurrenciesEqual(currency, token, currency.ChainId))
            {
                return currency;
            }
        }
        throw new InvalidOperationException(error);
    }
}
