using System.Numerics;
using UniswapSharp.Core.Entities.Fractions;

namespace UniswapSharp.Testing.Core.Entities.Fractions;

// Exact-formatting parity for Fraction.ToFixed / ToSignificant. Expected strings are ground
// truth generated with the exact upstream libraries (big.js / decimal.js-light), including
// large values that overflow System.Decimal (~7.9e28), all three rounding modes, negatives,
// and the significant-figure carry case (9999 -> 10000).
public class FractionFormattingTests
{
    private const string MAX = "115792089237316195423570985008687907853269984665640564039457584007913129639935";
    private const string E30 = "1000000000000000000000000000000";
    private const string E18 = "1000000000000000000";

    [Theory]
    [InlineData("1", "3", 4, Rounding.ROUND_HALF_UP, "0.3333")]
    [InlineData("2", "3", 4, Rounding.ROUND_HALF_UP, "0.6667")]
    [InlineData("2", "3", 4, Rounding.ROUND_DOWN, "0.6666")]
    [InlineData("2", "3", 4, Rounding.ROUND_UP, "0.6667")]
    [InlineData("-1", "3", 4, Rounding.ROUND_HALF_UP, "-0.3333")]
    [InlineData("-2", "3", 4, Rounding.ROUND_HALF_UP, "-0.6667")]
    [InlineData(E30, "1", 2, Rounding.ROUND_HALF_UP, "1000000000000000000000000000000.00")]
    [InlineData(MAX, E18, 18, Rounding.ROUND_HALF_UP, "115792089237316195423570985008687907853269984665640564039457.584007913129639935")]
    [InlineData("1", "1", 0, Rounding.ROUND_HALF_UP, "1")]
    [InlineData("5", "2", 0, Rounding.ROUND_HALF_UP, "3")]
    [InlineData("5", "2", 0, Rounding.ROUND_DOWN, "2")]
    [InlineData("123456789", "1000000", 3, Rounding.ROUND_HALF_UP, "123.457")]
    [InlineData("0", "7", 5, Rounding.ROUND_HALF_UP, "0.00000")]
    public void ToFixed_Matches(string num, string den, int decimalPlaces, Rounding rounding, string expected)
    {
        var f = new Fraction(BigInteger.Parse(num), BigInteger.Parse(den));
        Assert.Equal(expected, f.ToFixed(decimalPlaces, null, rounding));
    }

    [Theory]
    [InlineData("1", "3", 5, Rounding.ROUND_HALF_UP, "0.33333")]
    [InlineData("2", "3", 5, Rounding.ROUND_HALF_UP, "0.66667")]
    [InlineData("123456", "1", 4, Rounding.ROUND_DOWN, "123400")]
    [InlineData("123456", "1", 4, Rounding.ROUND_HALF_UP, "123500")]
    [InlineData(E30, "3", 5, Rounding.ROUND_HALF_UP, "333330000000000000000000000000")]
    [InlineData(MAX, "1", 10, Rounding.ROUND_HALF_UP, "115792089200000000000000000000000000000000000000000000000000000000000000000000")]
    [InlineData("1", "1000", 1, Rounding.ROUND_HALF_UP, "0.001")]
    [InlineData("5", "1", 1, Rounding.ROUND_HALF_UP, "5")]
    [InlineData("1003", "10", 2, Rounding.ROUND_DOWN, "100")]
    [InlineData("9999", "1", 2, Rounding.ROUND_HALF_UP, "10000")]
    public void ToSignificant_Matches(string num, string den, int significantDigits, Rounding rounding, string expected)
    {
        var f = new Fraction(BigInteger.Parse(num), BigInteger.Parse(den));
        Assert.Equal(expected, f.ToSignificant(significantDigits, "", rounding));
    }
}
