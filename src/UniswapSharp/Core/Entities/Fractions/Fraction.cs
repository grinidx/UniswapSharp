using System.Globalization;
using System.Numerics;
using ExtendedNumerics;

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

    public string ToSignificant(int significantDigits, string format = "0.#############################", Rounding rounding = Rounding.ROUND_HALF_UP)
    {
        if (!int.TryParse(significantDigits.ToString(), out _) || significantDigits <= 0)
        {
            throw new ArgumentException($"{significantDigits} is not a positive integer.");
        }
        // From BigRationalLibrary
 ;
        var quotient = SetSigFigs((decimal)new BigRational(Numerator, Denominator), significantDigits, rounding);

        return quotient.ToString(format);
    }

    public string ToFixed(int decimalPlaces, string? format = null, Rounding rounding = Rounding.ROUND_HALF_UP)
    {
        if (!int.TryParse(decimalPlaces.ToString(), out _) || decimalPlaces < 0)
        {
            throw new ArgumentException($"{decimalPlaces} is not a non-negative integer.");
        }

        var quotient = (decimal)((double)Numerator / (double)Denominator);
        return Round(quotient, decimalPlaces, rounding).ToString(format ?? $"F{decimalPlaces}", CultureInfo.InvariantCulture);
    }

    public static decimal Round(decimal value, int decimals, Rounding mode)
    {
        return mode switch
        {
            Rounding.ROUND_DOWN => Math.Floor(value * (decimal)Math.Pow(10, decimals)) / (decimal)Math.Pow(10, decimals),
            Rounding.ROUND_HALF_UP => Math.Round(value, decimals, MidpointRounding.AwayFromZero),
            Rounding.ROUND_UP => Math.Ceiling(value * (decimal)Math.Pow(10, decimals)) / (decimal)Math.Pow(10, decimals),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
    }


    public static decimal SetSigFigs(decimal d, int significantDigits, Rounding mode)
    {
        if (d == 0)
        {
            return 0;
        }

        var scale = (decimal)Math.Pow(10, Math.Floor(Math.Log10((double)Math.Abs(d))) + 1);

        return scale * Round(d / scale, significantDigits, mode);
    }

    public bool Equals(Fraction? other)
    {
        var otherParsed = TryParseFraction(other);
        return Numerator * otherParsed.Denominator == otherParsed.Numerator * Denominator;
    }
}
