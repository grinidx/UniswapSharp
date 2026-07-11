using System.Globalization;
using System.Numerics;

namespace UniswapSharp.Core.Entities.Fractions;

public class Fraction(BigInteger numerator, BigInteger denominator = default) : IEquatable<Fraction>
{
    public BigInteger Numerator { get; protected set; } = numerator;
    public BigInteger Denominator { get; protected set; } = denominator == default ? BigInteger.One : denominator;

    public BigInteger Quotient => BigInteger.Divide(Numerator, Denominator);

    public Fraction Remainder() => new(BigInteger.Remainder(Numerator, Denominator), Denominator);

    public Fraction AsFraction() => new(Numerator, Denominator);

    protected static Fraction TryParseFraction(object? fractionish)
    {
        if (fractionish is BigInteger bigInt)
        {
            return new Fraction(bigInt);
        }

        if (fractionish is int intValue)
        {
            return new Fraction(intValue);
        }

        if (fractionish is string strValue)
        {
            return new Fraction(BigInteger.Parse(strValue));
        }

        if (fractionish is Fraction fraction)
        {
            return fraction;
        }

        throw new ArgumentException("Could not parse fraction");
    }

    public Fraction Invert()
    {
        return new Fraction(Denominator, Numerator);
    }

    public Fraction Add(object other)
    {
        var otherParsed = TryParseFraction(other);
        if (Denominator == otherParsed.Denominator)
        {
            return new Fraction(Numerator + otherParsed.Numerator, Denominator);
        }

        return new Fraction(
            Numerator * otherParsed.Denominator + otherParsed.Numerator * Denominator,
            Denominator * otherParsed.Denominator
        );
    }

    public Fraction Subtract(object other)
    {
        var otherParsed = TryParseFraction(other);
        if (Denominator == otherParsed.Denominator)
        {
            return new Fraction(Numerator - otherParsed.Numerator, Denominator);
        }

        return new Fraction(
            Numerator * otherParsed.Denominator - otherParsed.Numerator * Denominator,
            Denominator * otherParsed.Denominator
        );
    }

    public bool LessThan(object other)
    {
        var otherParsed = TryParseFraction(other);
        return Numerator * otherParsed.Denominator < otherParsed.Numerator * Denominator;
    }



    public bool GreaterThan(object other)
    {
        var otherParsed = TryParseFraction(other);
        return Numerator * otherParsed.Denominator > otherParsed.Numerator * Denominator;
    }

    public Fraction Multiply(object other)
    {
        var otherParsed = TryParseFraction(other);
        return new Fraction(
            Numerator * otherParsed.Numerator,
            Denominator * otherParsed.Denominator
        );
    }

    public Fraction Divide(object other)
    {
        var otherParsed = TryParseFraction(other);
        return new Fraction(
            Numerator * otherParsed.Denominator,
            Denominator * otherParsed.Numerator
        );
    }

    // The `format` argument is retained for source compatibility but ignored: upstream's
    // toFormat only controls the (always-empty) group separator. Both formatters build the
    // exact decimal string with BigInteger — no floating point — matching decimal.js-light
    // (toSignificant) and big.js (toFixed) to the digit, including values that overflow
    // System.Decimal (~7.9e28).
    public string ToSignificant(int significantDigits, string format = "0.#############################", Rounding rounding = Rounding.ROUND_HALF_UP)
    {
        if (significantDigits <= 0)
        {
            throw new ArgumentException($"{significantDigits} is not a positive integer.");
        }

        return FormatSignificant(Numerator, Denominator, significantDigits, rounding);
    }

    public string ToFixed(int decimalPlaces, string? format = null, Rounding rounding = Rounding.ROUND_HALF_UP)
    {
        if (decimalPlaces < 0)
        {
            throw new ArgumentException($"{decimalPlaces} is not a non-negative integer.");
        }

        return FormatFixed(Numerator, Denominator, decimalPlaces, rounding);
    }

    private static BigInteger Pow10(int n) => BigInteger.Pow(10, n);

    // Rounds num/den (both > 0) to an integer per the rounding mode.
    private static BigInteger RoundDiv(BigInteger num, BigInteger den, Rounding mode)
    {
        BigInteger q = BigInteger.DivRem(num, den, out BigInteger r);
        return mode switch
        {
            Rounding.ROUND_DOWN => q,
            Rounding.ROUND_UP => r.IsZero ? q : q + BigInteger.One,
            Rounding.ROUND_HALF_UP => 2 * r >= den ? q + BigInteger.One : q,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
    }

    // floor(log10(n/d)) for n > 0, d > 0, computed exactly.
    private static int OrderOfMagnitude(BigInteger n, BigInteger d)
    {
        if (n >= d)
        {
            int e = 0;
            while (d * Pow10(e + 1) <= n) e++;
            return e;
        }
        else
        {
            int k = 1;
            while (n * Pow10(k) < d) k++;
            return -k;
        }
    }

    // big.js div + toFixed: value rounded to exactly `decimalPlaces` decimals (zero-padded).
    private static string FormatFixed(BigInteger numerator, BigInteger denominator, int decimalPlaces, Rounding rounding)
    {
        bool negative = numerator.Sign * denominator.Sign < 0;
        BigInteger n = BigInteger.Abs(numerator);
        BigInteger d = BigInteger.Abs(denominator);

        BigInteger scaled = RoundDiv(n * Pow10(decimalPlaces), d, rounding);
        string digits = scaled.ToString(CultureInfo.InvariantCulture);

        string result;
        if (decimalPlaces == 0)
        {
            result = digits;
        }
        else
        {
            if (digits.Length <= decimalPlaces)
            {
                digits = digits.PadLeft(decimalPlaces + 1, '0');
            }
            int split = digits.Length - decimalPlaces;
            result = digits[..split] + "." + digits[split..];
        }

        return negative && !scaled.IsZero ? "-" + result : result;
    }

    // decimal.js-light toSignificantDigits + toFormat(decimalPlaces()): rounded to
    // `significantDigits` significant figures, trailing fractional zeros stripped.
    private static string FormatSignificant(BigInteger numerator, BigInteger denominator, int significantDigits, Rounding rounding)
    {
        if (numerator.IsZero)
        {
            return "0";
        }

        bool negative = numerator.Sign * denominator.Sign < 0;
        BigInteger n = BigInteger.Abs(numerator);
        BigInteger d = BigInteger.Abs(denominator);

        int e = OrderOfMagnitude(n, d);
        int shift = significantDigits - 1 - e;

        BigInteger m = shift >= 0
            ? RoundDiv(n * Pow10(shift), d, rounding)
            : RoundDiv(n, d * Pow10(-shift), rounding);

        // value = m * 10^(-shift)
        string result;
        if (shift <= 0)
        {
            result = m.ToString(CultureInfo.InvariantCulture) + new string('0', -shift);
        }
        else
        {
            string digits = m.ToString(CultureInfo.InvariantCulture);
            if (digits.Length <= shift)
            {
                digits = digits.PadLeft(shift + 1, '0');
            }
            int split = digits.Length - shift;
            string frac = digits[split..].TrimEnd('0');
            result = frac.Length == 0 ? digits[..split] : digits[..split] + "." + frac;
        }

        return negative && !m.IsZero ? "-" + result : result;
    }

    public bool Equals(Fraction? other)
    {
        var otherParsed = TryParseFraction(other);
        return Numerator * otherParsed.Denominator == otherParsed.Numerator * Denominator;
    }
}
