using System.Globalization;
using System.Numerics;

namespace UniswapSharp.Core.Entities.Fractions;




public class CurrencyAmount<T> : Fraction, IEquatable<CurrencyAmount<T>> where T : BaseCurrency
{
    public T Currency { get; }
    public BigInteger DecimalScale { get; }

    public static CurrencyAmount<T2> FromRawAmount<T2>(T2 currency, BigInteger rawAmount) where T2 : BaseCurrency
    {
        return new CurrencyAmount<T2>(currency, rawAmount);
    }

    public static CurrencyAmount<T2> FromFractionalAmount<T2>(T2 currency, BigInteger numerator, BigInteger denominator) where T2 : BaseCurrency
    {
        return new CurrencyAmount<T2>(currency, numerator, denominator);
    }


    protected CurrencyAmount(T currency, BigInteger numerator, BigInteger? denominator = null)
        : base(numerator, denominator ?? BigInteger.One)
    {
        if (Quotient > BigInteger.Parse("115792089237316195423570985008687907853269984665640564039457584007913129639935"))
            throw new ArgumentException("AMOUNT");

        Currency = (T)currency;
        DecimalScale = BigInteger.Pow(10, currency.Decimals);
    }

    public CurrencyAmount<T> Add(CurrencyAmount<T> other)
    {
        if (!Currency.Equals(other.Currency))
            throw new ArgumentException("CURRENCY");

        var added = base.Add(other);
        return FromFractionalAmount(Currency, added.Numerator, added.Denominator);
    }

    public CurrencyAmount<T> Subtract(CurrencyAmount<T> other)
    {
        if (!Currency.Equals(other.Currency))
            throw new ArgumentException("CURRENCY");

        var subtracted = base.Subtract(other);
        return FromFractionalAmount(Currency, subtracted.Numerator, subtracted.Denominator);
    }

    public CurrencyAmount<T> Multiply(Fraction other)
    {
        var multiplied = base.Multiply(other);
        return FromFractionalAmount(Currency, multiplied.Numerator, multiplied.Denominator);
    }

    public CurrencyAmount<T> Divide(Fraction other)
    {
        var divided = base.Divide(other);
        return FromFractionalAmount(Currency, divided.Numerator, divided.Denominator);
    }

    public new string ToSignificant(int significantDigits = 6, string format = "G29", Rounding rounding = Rounding.ROUND_DOWN)
    {
        return base.Divide(new Fraction(DecimalScale)).ToSignificant(significantDigits, format, rounding);
    }

    public new string ToFixed(int decimalPlaces = -1, string? format = null, Rounding rounding = Rounding.ROUND_DOWN)
    {
        if (decimalPlaces == -1)
            decimalPlaces = Currency.Decimals;

        if (decimalPlaces > Currency.Decimals)
            throw new ArgumentException("DECIMALS");

        return base.Divide(new Fraction(DecimalScale)).ToFixed(decimalPlaces, format, rounding);
    }

    public string ToExact(string format = "0.#############################")
    {
        // Exact decimal representation of Quotient / DecimalScale computed with
        // BigInteger (Decimal.js parity). The previous `(decimal)` cast overflowed
        // System.Decimal (~7.9e28) for large token amounts.
        BigInteger integerPart = BigInteger.DivRem(Quotient, DecimalScale, out BigInteger remainder);
        if (remainder.IsZero)
        {
            return integerPart.ToString(CultureInfo.InvariantCulture);
        }

        string fractional = BigInteger.Abs(remainder)
            .ToString(CultureInfo.InvariantCulture)
            .PadLeft(Currency.Decimals, '0')
            .TrimEnd('0');

        return integerPart.ToString(CultureInfo.InvariantCulture) + "." + fractional;
    }

    public CurrencyAmount<BaseCurrency> AsBaseCurrency() => new(this.Currency, Numerator, Denominator);

    /// <summary>
    /// The amount re-expressed against the currency's wrapped <see cref="Token"/>.
    /// Never null: every path either reuses this instance or constructs a new amount.
    /// </summary>
    public CurrencyAmount<Token> Wrapped()
    {
        if (Currency is Token)
        {
            var x = this as CurrencyAmount<Token>;
            return x ?? FromFractionalAmount(Currency.Wrapped(), Numerator, Denominator);
        }

        return FromFractionalAmount(Currency.Wrapped(), Numerator, Denominator);
    }

    public bool Equals(CurrencyAmount<T>? other)
    {
        return base.Equals(other);
    }






}
