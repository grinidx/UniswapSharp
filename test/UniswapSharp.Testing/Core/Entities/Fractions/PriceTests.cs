using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;

namespace UniswapSharp.Testing.Core.Entities.Fractions;

public class PriceTests
{
    private const string ADDRESS_ZERO = "0x0000000000000000000000000000000000000000";
    private const string ADDRESS_ONE = "0x0000000000000000000000000000000000000001";

    private readonly Token t0;
    private readonly Token t0_6;
    private readonly Token t1;

    public PriceTests()
    {
        t0 = new Token(1, ADDRESS_ZERO, 18);
        t0_6 = new Token(1, ADDRESS_ZERO, 6);
        t1 = new Token(1, ADDRESS_ONE, 18);
    }

    [Fact]
    public void Constructor_ArrayFormat_Works()
    {
        var price = new Price<Token,Token>(t0, t1, 1, 54321);
        Assert.Equal("54321", price.ToSignificant(5));
        Assert.True(price.BaseCurrency.Equals(t0));
        Assert.True(price.QuoteCurrency.Equals(t1));
    }

    [Fact]
    public void Constructor_ObjectFormat_Works()
    {
        var price = new Price<Token, Token>(CurrencyAmount<Token>.FromRawAmount(t0, 1),CurrencyAmount<Token>.FromRawAmount(t1, 54321));
        Assert.Equal("54321", price.ToSignificant(5));
        Assert.True(price.BaseCurrency.Equals(t0));
        Assert.True(price.QuoteCurrency.Equals(t1));
    }

    [Fact]
    public void Quote_ReturnsCorrectValue()
    {
        var price = new Price<Token, Token>(t0, t1, 1, 5);
        var result = price.Quote(CurrencyAmount<Token>.FromRawAmount(t0, 10));
        Assert.Equal(CurrencyAmount<Token>.FromRawAmount(t1, 50), result);
    }

    [Fact]
    public void ToSignificant_NoDecimals()
    {
        var p = new Price<Token, Token>(t0, t1, 123, 456);
        Assert.Equal("3.707", p.ToSignificant(4));
    }

    [Fact]
    public void ToSignificant_NoDecimalsFlipRatio()
    {
        var p = new Price< Token, Token> (t0, t1, 456, 123);
        Assert.Equal("0.2697", p.ToSignificant(4));
    }

    [Fact]
    public void ToSignificant_WithDecimalDifference()
    {
        var p = new Price<Token, Token>(t0_6, t1, 123, 456);
        Assert.Equal("0.000000000003707", p.ToSignificant(4));
    }

    [Fact]
    public void ToSignificant_WithDecimalDifferenceFlipped()
    {
        var p = new Price<Token, Token>(t0_6, t1, 456, 123);
        Assert.Equal("0.0000000000002697", p.ToSignificant(4));
    }

    [Fact]
    public void ToSignificant_WithDecimalDifferenceFlippedBaseQuoteFlipped()
    {
        var p = new Price<Token, Token>(t1, t0_6, 456, 123);
        Assert.Equal("269700000000", p.ToSignificant(4));
    }
}