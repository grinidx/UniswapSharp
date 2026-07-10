using System.Numerics;
using UniswapSharp.Core.Entities.Fractions;

namespace UniswapSharp.Testing.Core.Entities.Fractions;

public class FractionTests
{
    [Fact]
    public void Quotient_FloorDivision()
    {
        Assert.Equal(BigInteger.Parse("2"), new Fraction(BigInteger.Parse("8"), BigInteger.Parse("3")).Quotient); // one below
        Assert.Equal(BigInteger.Parse("3"), new Fraction(BigInteger.Parse("12"), BigInteger.Parse("4")).Quotient); // exact
        Assert.Equal(BigInteger.Parse("3"), new Fraction(BigInteger.Parse("16"), BigInteger.Parse("5")).Quotient); // one above
    }

    [Fact]
    public void Remainder_ReturnsFractionAfterDivision()
    {
        Assert.Equal(new Fraction(BigInteger.Parse("2"), BigInteger.Parse("3")), new Fraction(BigInteger.Parse("8"), BigInteger.Parse("3")).Remainder());
        Assert.Equal(new Fraction(BigInteger.Parse("0"), BigInteger.Parse("4")), new Fraction(BigInteger.Parse("12"), BigInteger.Parse("4")).Remainder());
        Assert.Equal(new Fraction(BigInteger.Parse("1"), BigInteger.Parse("5")), new Fraction(BigInteger.Parse("16"), BigInteger.Parse("5")).Remainder());
    }

    [Fact]
    public void Invert_FlipsNumAndDenom()
    {
        var inverted = new Fraction(BigInteger.Parse("5"), BigInteger.Parse("10")).Invert();
        Assert.Equal(BigInteger.Parse("10"), inverted.Numerator);
        Assert.Equal(BigInteger.Parse("5"), inverted.Denominator);
    }

    [Fact]
    public void Add_MultiplesDenomAndAddsNums()
    {
        Assert.Equal(
            new Fraction(BigInteger.Parse("52"), BigInteger.Parse("120")),
            new Fraction(BigInteger.Parse("1"), BigInteger.Parse("10")).Add(new Fraction(BigInteger.Parse("4"), BigInteger.Parse("12")))
        );
    }

    [Fact]
    public void Add_SameDenom()
    {
        Assert.Equal(
            new Fraction(BigInteger.Parse("3"), BigInteger.Parse("5")),
            new Fraction(BigInteger.Parse("1"), BigInteger.Parse("5")).Add(new Fraction(BigInteger.Parse("2"), BigInteger.Parse("5")))
        );
    }

    [Fact]
    public void Subtract_MultiplesDenomAndSubtractsNums()
    {
        Assert.Equal(
            new Fraction(BigInteger.Parse("-28"), BigInteger.Parse("120")),
            new Fraction(BigInteger.Parse("1"), BigInteger.Parse("10")).Subtract(new Fraction(BigInteger.Parse("4"), BigInteger.Parse("12")))
        );
    }

    [Fact]
    public void Subtract_SameDenom()
    {
        Assert.Equal(
            new Fraction(BigInteger.Parse("1"), BigInteger.Parse("5")),
            new Fraction(BigInteger.Parse("3"), BigInteger.Parse("5")).Subtract(new Fraction(BigInteger.Parse("2"), BigInteger.Parse("5")))
        );
    }

    [Fact]
    public void LessThan_Correct()
    {
        Assert.True(new Fraction(BigInteger.Parse("1"), BigInteger.Parse("10")).LessThan(new Fraction(BigInteger.Parse("4"), BigInteger.Parse("12"))));
        Assert.False(new Fraction(BigInteger.Parse("1"), BigInteger.Parse("3")).LessThan(new Fraction(BigInteger.Parse("4"), BigInteger.Parse("12"))));
        Assert.False(new Fraction(BigInteger.Parse("5"), BigInteger.Parse("12")).LessThan(new Fraction(BigInteger.Parse("4"), BigInteger.Parse("12"))));
    }

    [Fact]
    public void EqualTo_Correct()
    {
        Assert.False(new Fraction(BigInteger.Parse("1"), BigInteger.Parse("10")).Equals(new Fraction(BigInteger.Parse("4"), BigInteger.Parse("12"))));
        Assert.True(new Fraction(BigInteger.Parse("1"), BigInteger.Parse("3")).Equals(new Fraction(BigInteger.Parse("4"), BigInteger.Parse("12"))));
        Assert.False(new Fraction(BigInteger.Parse("5"), BigInteger.Parse("12")).Equals(new Fraction(BigInteger.Parse("4"), BigInteger.Parse("12"))));
    }

    [Fact]
    public void GreaterThan_Correct()
    {
        Assert.False(new Fraction(BigInteger.Parse("1"), BigInteger.Parse("10")).GreaterThan(new Fraction(BigInteger.Parse("4"), BigInteger.Parse("12"))));
        Assert.False(new Fraction(BigInteger.Parse("1"), BigInteger.Parse("3")).GreaterThan(new Fraction(BigInteger.Parse("4"), BigInteger.Parse("12"))));
        Assert.True(new Fraction(BigInteger.Parse("5"), BigInteger.Parse("12")).GreaterThan(new Fraction(BigInteger.Parse("4"), BigInteger.Parse("12"))));
    }

    [Fact]
    public void Multiply_Correct()
    {
        Assert.Equal(
            new Fraction(BigInteger.Parse("4"), BigInteger.Parse("120")),
            new Fraction(BigInteger.Parse("1"), BigInteger.Parse("10")).Multiply(new Fraction(BigInteger.Parse("4"), BigInteger.Parse("12")))
        );
        Assert.Equal(
            new Fraction(BigInteger.Parse("4"), BigInteger.Parse("36")),
            new Fraction(BigInteger.Parse("1"), BigInteger.Parse("3")).Multiply(new Fraction(BigInteger.Parse("4"), BigInteger.Parse("12")))
        );
        Assert.Equal(
            new Fraction(BigInteger.Parse("20"), BigInteger.Parse("144")),
            new Fraction(BigInteger.Parse("5"), BigInteger.Parse("12")).Multiply(new Fraction(BigInteger.Parse("4"), BigInteger.Parse("12")))
        );
    }

    [Fact]
    public void Divide_Correct()
    {
        Assert.Equal(
            new Fraction(BigInteger.Parse("12"), BigInteger.Parse("40")),
            new Fraction(BigInteger.Parse("1"), BigInteger.Parse("10")).Divide(new Fraction(BigInteger.Parse("4"), BigInteger.Parse("12")))
        );
        Assert.Equal(
            new Fraction(BigInteger.Parse("12"), BigInteger.Parse("12")),
            new Fraction(BigInteger.Parse("1"), BigInteger.Parse("3")).Divide(new Fraction(BigInteger.Parse("4"), BigInteger.Parse("12")))
        );
        Assert.Equal(
            new Fraction(BigInteger.Parse("60"), BigInteger.Parse("48")),
            new Fraction(BigInteger.Parse("5"), BigInteger.Parse("12")).Divide(new Fraction(BigInteger.Parse("4"), BigInteger.Parse("12")))
        );
    }

    [Fact]
    public void AsFraction_ReturnsEquivalentButNotSameReferenceFraction()
    {
        var f = new Fraction(1, 2);
        Assert.Equal(f, f.AsFraction());
        Assert.NotSame(f, f.AsFraction());
    }
}
