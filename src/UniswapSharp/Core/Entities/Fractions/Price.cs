using System.Numerics;
using System.Reflection;

namespace UniswapSharp.Core.Entities.Fractions;

public class Price<TBase, TQuote> : Fraction,IEquatable<Price<TBase, TQuote>> where TBase : BaseCurrency where TQuote : BaseCurrency
{
    public TBase BaseCurrency { get; } // input i.e. denominator
    public TQuote QuoteCurrency { get; } // output i.e. numerator
    public Fraction Scalar { get; } // used to adjust the raw fraction w/r/t the decimals of the {base,quote}Token

    public Price(TBase baseCurrency, TQuote quoteCurrency, BigInteger denominator, BigInteger numerator)
        : base(numerator, denominator)
    {
        BaseCurrency = baseCurrency;
        QuoteCurrency = quoteCurrency;
        Scalar = new Fraction(
            BigInteger.Pow(10, baseCurrency.Decimals),
            BigInteger.Pow(10, quoteCurrency.Decimals)
        );
    }


    public Price(CurrencyAmount<TBase> baseAmount, CurrencyAmount<TQuote> quoteAmount)
        : base(1,1)
    {
        var result = quoteAmount.Divide(baseAmount);
        BaseCurrency = baseAmount.Currency;
        QuoteCurrency = quoteAmount.Currency;
        Numerator = result.Numerator;
        Denominator = result.Denominator;
        Scalar = new Fraction(
            BigInteger.Pow(10, BaseCurrency.Decimals),
            BigInteger.Pow(10, QuoteCurrency.Decimals)
        );
    }

    public new Price<TQuote, TBase> Invert()
    {
        return new Price<TQuote, TBase>(QuoteCurrency, BaseCurrency, Numerator, Denominator);
    }

    public Price<TBase, TOtherQuote> Multiply<TOtherQuote>(Price<TQuote, TOtherQuote> other) where TOtherQuote : BaseCurrency
    {
        if (!QuoteCurrency.Equals(other.BaseCurrency))
            throw new InvalidOperationException("TOKEN");

        var fraction = base.Multiply(other);
        return new Price<TBase, TOtherQuote>(BaseCurrency, other.QuoteCurrency, fraction.Denominator, fraction.Numerator);
    }

    public CurrencyAmount<TQuote> Quote(CurrencyAmount<TBase> currencyAmount)
    {
        if (!currencyAmount.Currency.Equals(BaseCurrency))
            throw new InvalidOperationException("TOKEN");

        var result = base.Multiply(currencyAmount);
        return CurrencyAmount<TQuote>.FromFractionalAmount(QuoteCurrency, result.Numerator, result.Denominator);
    }

    private Fraction AdjustedForDecimals => base.Multiply(Scalar);

    public new string ToSignificant(int significantDigits = 6, string format = "0.#############################", Rounding rounding = Rounding.ROUND_HALF_UP)
    {
        return AdjustedForDecimals.ToSignificant(significantDigits, format, rounding);
    }

    public new string ToFixed(int decimalPlaces = 4, string? format = null, Rounding rounding = Rounding.ROUND_HALF_UP)
    {
        return AdjustedForDecimals.ToFixed(decimalPlaces, format, rounding);
    }

    public bool Equals(Price<TBase, TQuote>? other)
    {
        var otherParsed = TryParseFraction(other);
        return Numerator * otherParsed.Denominator == otherParsed.Numerator * Denominator;
    }
}