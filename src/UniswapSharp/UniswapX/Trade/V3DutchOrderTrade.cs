using System.Numerics;
using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.UniswapX.Order;

namespace UniswapSharp.UniswapX.Trade;

/// <summary>Expected input/output amounts for a quoted trade (uniswapx-sdk trade <c>expectedAmounts</c>).</summary>
public sealed record ExpectedAmounts(string ExpectedAmountIn, string ExpectedAmountOut);

/// <summary>Port of uniswapx-sdk <c>trade/V3DutchOrderTrade.ts</c>.</summary>
public sealed class V3DutchOrderTrade<TInput, TOutput>
    where TInput : BaseCurrency
    where TOutput : BaseCurrency
{
    public TradeType TradeType { get; }
    public UnsignedV3DutchOrder Order { get; }
    public ExpectedAmounts? ExpectedAmounts { get; }

    private readonly TInput _currencyIn;
    private readonly IReadOnlyList<TOutput> _currenciesOut;

    private CurrencyAmount<TInput>? _inputAmount;
    private IReadOnlyList<CurrencyAmount<TOutput>>? _outputAmounts;
    private Price<TInput, TOutput>? _executionPrice;

    public V3DutchOrderTrade(TInput currencyIn, IReadOnlyList<TOutput> currenciesOut, UnsignedV3DutchOrderInfo orderInfo,
        TradeType tradeType, ExpectedAmounts? expectedAmounts = null)
    {
        _currencyIn = currencyIn;
        _currenciesOut = currenciesOut;
        TradeType = tradeType;
        ExpectedAmounts = expectedAmounts;
        Order = new UnsignedV3DutchOrder(orderInfo, currencyIn.ChainId);
    }

    public CurrencyAmount<TInput> InputAmount =>
        _inputAmount ??= ExpectedAmounts?.ExpectedAmountIn is not null
            ? GetExpectedAmountIn()
            : CurrencyAmount<TInput>.FromRawAmount(_currencyIn, Order.Info.Input.StartAmount);

    public IReadOnlyList<CurrencyAmount<TOutput>> OutputAmounts =>
        _outputAmounts ??= Order.Info.Outputs.Select(output =>
            CurrencyAmount<TOutput>.FromRawAmount(FindCurrencyOut(output.Token), output.StartAmount)).ToList();

    public CurrencyAmount<TOutput> OutputAmount =>
        ExpectedAmounts?.ExpectedAmountOut is not null ? GetExpectedAmountOut() : OutputAmounts[0];

    public CurrencyAmount<TOutput> MinimumAmountOut()
    {
        var nonFeeOutput = Order.Info.Outputs[0];
        var relativeAmounts = nonFeeOutput.Curve.RelativeAmounts;
        BigInteger startAmount = nonFeeOutput.StartAmount;
        BigInteger maxRelative = relativeAmounts.Aggregate(BigInteger.Zero, (max, a) => a > max ? a : max);
        BigInteger minOut = startAmount - maxRelative;
        return CurrencyAmount<TOutput>.FromRawAmount(OutputAmount.Currency, minOut);
    }

    public CurrencyAmount<TInput> MaximumAmountIn() =>
        CurrencyAmount<TInput>.FromRawAmount(_currencyIn, Order.Info.Input.MaxAmount);

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

    private TOutput FindCurrencyOut(string token)
    {
        foreach (var currency in _currenciesOut)
        {
            if (TradeUtils.AreCurrenciesEqual(currency, token, currency.ChainId))
            {
                return currency;
            }
        }
        throw new InvalidOperationException("Currency out not found");
    }
}
