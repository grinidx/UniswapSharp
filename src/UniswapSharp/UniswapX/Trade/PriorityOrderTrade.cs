using System.Numerics;
using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.UniswapX.Order;

namespace UniswapSharp.UniswapX.Trade;

/// <summary>Port of uniswapx-sdk <c>trade/PriorityOrderTrade.ts</c>.</summary>
public sealed class PriorityOrderTrade<TInput, TOutput>
    where TInput : BaseCurrency
    where TOutput : BaseCurrency
{
    public TradeType TradeType { get; }
    public UnsignedPriorityOrder Order { get; }
    public ExpectedAmounts? ExpectedAmounts { get; }

    private readonly TInput _currencyIn;
    private readonly IReadOnlyList<TOutput> _currenciesOut;

    private CurrencyAmount<TInput>? _inputAmount;
    private IReadOnlyList<CurrencyAmount<TOutput>>? _outputAmounts;
    private CurrencyAmount<TOutput>? _firstNonFeeOutputAmount;
    private Price<TInput, TOutput>? _executionPrice;

    public PriorityOrderTrade(TInput currencyIn, IReadOnlyList<TOutput> currenciesOut, UnsignedPriorityOrderInfo orderInfo,
        TradeType tradeType, ExpectedAmounts? expectedAmounts = null)
    {
        _currencyIn = currencyIn;
        _currenciesOut = currenciesOut;
        TradeType = tradeType;
        ExpectedAmounts = expectedAmounts;
        Order = new UnsignedPriorityOrder(orderInfo, currencyIn.ChainId);
    }

    public CurrencyAmount<TInput> InputAmount =>
        _inputAmount ??= ExpectedAmounts?.ExpectedAmountIn is not null
            ? GetExpectedAmountIn()
            : CurrencyAmount<TInput>.FromRawAmount(_currencyIn, Order.Info.Input.Amount);

    public IReadOnlyList<CurrencyAmount<TOutput>> OutputAmounts =>
        _outputAmounts ??= Order.Info.Outputs.Select(output =>
            CurrencyAmount<TOutput>.FromRawAmount(FindCurrencyOut(output.Token), output.Amount)).ToList();

    private CurrencyAmount<TOutput> GetFirstNonFeeOutputAmount()
    {
        if (_firstNonFeeOutputAmount is not null)
        {
            return _firstNonFeeOutputAmount;
        }
        if (Order.Info.Outputs.Count == 0)
        {
            throw new InvalidOperationException("there must be at least one output token");
        }
        var output = Order.Info.Outputs[0];
        var currencyOut = FindCurrencyOut(output.Token, "currency output from order must exist in currenciesOut list");
        _firstNonFeeOutputAmount = CurrencyAmount<TOutput>.FromRawAmount(currencyOut, output.Amount);
        return _firstNonFeeOutputAmount;
    }

    public CurrencyAmount<TOutput> OutputAmount =>
        ExpectedAmounts?.ExpectedAmountOut is not null ? GetExpectedAmountOut() : GetFirstNonFeeOutputAmount();

    public CurrencyAmount<TOutput> MinimumAmountOut() => GetFirstNonFeeOutputAmount();

    public CurrencyAmount<TInput> MaximumAmountIn() =>
        CurrencyAmount<TInput>.FromRawAmount(_currencyIn, Order.Info.Input.Amount);

    public Price<TInput, TOutput> ExecutionPrice =>
        _executionPrice ??= new Price<TInput, TOutput>(
            InputAmount.Currency, OutputAmount.Currency, InputAmount.Quotient, OutputAmount.Quotient);

    public Price<TInput, TOutput> WorstExecutionPrice() =>
        new(InputAmount.Currency, OutputAmount.Currency, MaximumAmountIn().Quotient, MinimumAmountOut().Quotient);

    private CurrencyAmount<TInput> GetExpectedAmountIn()
    {
        if (ExpectedAmounts?.ExpectedAmountIn is null)
        {
            throw new InvalidOperationException("expectedAmountIn not set");
        }
        return CurrencyAmount<TInput>.FromRawAmount(_currencyIn, BigInteger.Parse(ExpectedAmounts.ExpectedAmountIn));
    }

    private CurrencyAmount<TOutput> GetExpectedAmountOut()
    {
        if (ExpectedAmounts?.ExpectedAmountOut is null)
        {
            throw new InvalidOperationException("expectedAmountOut not set");
        }
        return CurrencyAmount<TOutput>.FromRawAmount(_currenciesOut[0], BigInteger.Parse(ExpectedAmounts.ExpectedAmountOut));
    }

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
