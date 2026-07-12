using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.UniswapX.Order;

namespace UniswapSharp.UniswapX.Trade;

/// <summary>
/// Port of uniswapx-sdk <c>trade/RelayOrderTrade.ts</c>: a high-level trade representing a relay order.
/// Requires the output amount to be provided (relay orders have no on-chain output concept).
/// </summary>
public sealed class RelayOrderTrade<TInput, TOutput>
    where TInput : BaseCurrency
    where TOutput : BaseCurrency
{
    public TradeType TradeType { get; }
    public RelayOrder Order { get; }

    private readonly IReadOnlyList<TInput> _currenciesIn;
    private readonly CurrencyAmount<TOutput> _outputAmount;

    private (CurrencyAmount<TInput> Start, CurrencyAmount<TInput> End)? _feeStartEndAmounts;
    private CurrencyAmount<TInput>? _inputAmount;
    private Price<TInput, TOutput>? _executionPrice;

    public RelayOrderTrade(IReadOnlyList<TInput> currenciesIn, CurrencyAmount<TOutput> outputAmount, RelayOrderInfo orderInfo, TradeType tradeType)
    {
        _currenciesIn = currenciesIn;
        _outputAmount = outputAmount;
        TradeType = tradeType;
        Order = new RelayOrder(orderInfo, outputAmount.Currency.ChainId);
    }

    public CurrencyAmount<TOutput> OutputAmount => _outputAmount;

    private (CurrencyAmount<TInput> Start, CurrencyAmount<TInput> End) GetFeeInputStartEndAmounts()
    {
        if (_feeStartEndAmounts is { } cached)
        {
            return cached;
        }
        var currencyIn = FindCurrencyIn(Order.Info.Fee.Token, "currency output from order must exist in currenciesOut list");
        var result = (
            CurrencyAmount<TInput>.FromRawAmount(currencyIn, Order.Info.Fee.StartAmount),
            CurrencyAmount<TInput>.FromRawAmount(currencyIn, Order.Info.Fee.EndAmount));
        _feeStartEndAmounts = result;
        return result;
    }

    private CurrencyAmount<TInput> GetInputAmount()
    {
        if (_inputAmount is not null)
        {
            return _inputAmount;
        }
        var currencyIn = FindCurrencyIn(Order.Info.Input.Token, "currency input from order must exist in currenciesIn list");
        _inputAmount = CurrencyAmount<TInput>.FromRawAmount(currencyIn, Order.Info.Input.Amount);
        return _inputAmount;
    }

    public CurrencyAmount<TInput> AmountIn => GetInputAmount();

    public CurrencyAmount<TInput> AmountInFee => GetFeeInputStartEndAmounts().Start;

    public CurrencyAmount<TInput> MaximumAmountInFee => GetFeeInputStartEndAmounts().End;

    public Price<TInput, TOutput> ExecutionPrice =>
        _executionPrice ??= new Price<TInput, TOutput>(
            AmountIn.Currency, OutputAmount.Currency, AmountIn.Quotient, OutputAmount.Quotient);

    public Price<TInput, TOutput> WorstExecutionPrice() =>
        new(AmountIn.Currency, OutputAmount.Currency, AmountIn.Quotient, OutputAmount.Quotient);

    private TInput FindCurrencyIn(string token, string error)
    {
        foreach (var currency in _currenciesIn)
        {
            if (TradeUtils.AreCurrenciesEqual(currency, token, currency.ChainId))
            {
                return currency;
            }
        }
        throw new InvalidOperationException(error);
    }
}
