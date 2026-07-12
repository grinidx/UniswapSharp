using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.UniswapX.Order.V4;

namespace UniswapSharp.UniswapX.Trade;

/// <summary>Port of uniswapx-sdk <c>trade/HybridOrderTrade.ts</c>.</summary>
public sealed class HybridOrderTrade<TInput, TOutput>
    where TInput : BaseCurrency
    where TOutput : BaseCurrency
{
    public TradeType TradeType { get; }
    public CosignedHybridOrder Order { get; }
    public ExpectedAmounts? ExpectedAmounts { get; }

    private readonly TInput _currencyIn;
    private readonly IReadOnlyList<TOutput> _currenciesOut;

    private CurrencyAmount<TInput>? _inputAmount;
    private IReadOnlyList<CurrencyAmount<TOutput>>? _outputAmounts;
    private Price<TInput, TOutput>? _executionPrice;

    public HybridOrderTrade(
        TInput currencyIn,
        IReadOnlyList<TOutput> currenciesOut,
        CosignedHybridOrderInfo orderInfo,
        int chainId,
        string resolver,
        TradeType tradeType,
        string? permit2Address = null,
        ExpectedAmounts? expectedAmounts = null)
    {
        _currencyIn = currencyIn;
        _currenciesOut = currenciesOut;
        TradeType = tradeType;
        ExpectedAmounts = expectedAmounts;
        Order = new CosignedHybridOrder(orderInfo, chainId, resolver, permit2Address);
    }

    public CurrencyAmount<TInput> InputAmount =>
        _inputAmount ??= ExpectedAmounts?.ExpectedAmountIn is not null
            ? GetExpectedAmountIn()
            : CurrencyAmount<TInput>.FromRawAmount(_currencyIn, Order.Info.Input.MaxAmount);

    public IReadOnlyList<CurrencyAmount<TOutput>> OutputAmounts =>
        _outputAmounts ??= Order.Info.Outputs.Select(output =>
            CurrencyAmount<TOutput>.FromRawAmount(FindCurrencyOut(output.Token), output.MinAmount)).ToList();

    public CurrencyAmount<TOutput> OutputAmount =>
        ExpectedAmounts?.ExpectedAmountOut is not null ? GetExpectedAmountOut() : OutputAmounts[0];

    public CurrencyAmount<TOutput> MinimumAmountOut() =>
        CurrencyAmount<TOutput>.FromRawAmount(OutputAmount.Currency, Order.Info.Outputs[0].MinAmount);

    public CurrencyAmount<TInput> MaximumAmountIn() =>
        CurrencyAmount<TInput>.FromRawAmount(_currencyIn, Order.Info.Input.MaxAmount);

    public Price<TInput, TOutput> ExecutionPrice =>
        _executionPrice ??= new Price<TInput, TOutput>(
            InputAmount.Currency, OutputAmount.Currency, InputAmount.Quotient, OutputAmount.Quotient);

    public Price<TInput, TOutput> WorstExecutionPrice() =>
        new(InputAmount.Currency, OutputAmount.Currency, MaximumAmountIn().Quotient, MinimumAmountOut().Quotient);

    /// <summary>Whether this is an exact-in order (scalingFactor &gt;= 1e18).</summary>
    public bool IsExactIn() => Order.Info.ScalingFactor >= ConstantsV4.BaseScalingFactor;

    /// <summary>Whether this is an exact-out order (scalingFactor &lt; 1e18).</summary>
    public bool IsExactOut() => Order.Info.ScalingFactor < ConstantsV4.BaseScalingFactor;

    private CurrencyAmount<TInput> GetExpectedAmountIn()
    {
        if (ExpectedAmounts?.ExpectedAmountIn is null)
        {
            throw new InvalidOperationException("expectedAmountIn not set");
        }
        return CurrencyAmount<TInput>.FromRawAmount(_currencyIn, System.Numerics.BigInteger.Parse(ExpectedAmounts.ExpectedAmountIn));
    }

    private CurrencyAmount<TOutput> GetExpectedAmountOut()
    {
        if (ExpectedAmounts?.ExpectedAmountOut is null)
        {
            throw new InvalidOperationException("expectedAmountOut not set");
        }
        return CurrencyAmount<TOutput>.FromRawAmount(_currenciesOut[0], System.Numerics.BigInteger.Parse(ExpectedAmounts.ExpectedAmountOut));
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
