using System.Numerics;

namespace UniswapSharp.Core.Entities.Fractions;

public class Percent(BigInteger numerator, BigInteger denominator = default) : Fraction(numerator, denominator), IEquatable<Percent>
{
    // ReSharper disable once InconsistentNaming
    private static readonly Fraction ONE_HUNDRED = new(new BigInteger(100));

    public bool IsPercent { get; } = true;

    public static Percent ToPercent(Fraction fraction)
    {
        return new Percent(fraction.Numerator, fraction.Denominator);
    }

    public Percent Add(Fraction other)
    {
        return ToPercent(base.Add(other));
    }

    public Percent Add(BigInteger other)
    {
        return ToPercent(base.Add(other));
    }

    public Percent Subtract(Fraction other)
    {
        return ToPercent(base.Subtract(other));
    }

    public Percent Subtract(BigInteger other)
    {
        return ToPercent(base.Subtract(other));
    }

    public Percent Multiply(Fraction other)
    {
        return ToPercent(base.Multiply(other));
    }

    public Percent Multiply(BigInteger other)
    {
        return ToPercent(base.Multiply(other));
    }

    public Percent Divide(Fraction other)
    {
        return ToPercent(base.Divide(other));
    }

    public Percent Divide(BigInteger other)
    {
        return ToPercent(base.Divide(other));
    }

    public new string ToSignificant(int significantDigits = 5, string format = "G29", Rounding rounding = Rounding.ROUND_HALF_UP)
    {
        return base.Multiply(ONE_HUNDRED).ToSignificant(significantDigits, format, rounding);
    }

    public new string ToFixed(int decimalPlaces = 2, string format = "", Rounding rounding = Rounding.ROUND_HALF_UP)
    {
        return base.Multiply(ONE_HUNDRED).ToFixed(decimalPlaces, format, rounding);
    }

    public bool Equals(Percent? other)
    {
        return base.Equals(other);
    }
}
