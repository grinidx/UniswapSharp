using System.Numerics;
using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;


namespace UniswapSharp.Testing.Core.Entities.Fractions;

public class CurrencyAmountTests
{
    private const string ADDRESS_ONE = "0x0000000000000000000000000000000000000001";

    [Fact]
    public void Constructor_Works()
    {
        var token = new Token(1, ADDRESS_ONE, 18);
        var amount = CurrencyAmount<Token>.FromRawAmount(token, 100);
        Assert.Equal(new BigInteger(100), amount.Quotient);
    }

    [Fact]
    public void Quotient_ReturnsAmountAfterMultiplication()
    {
        var token = new Token(1, ADDRESS_ONE, 18);
        var amount = CurrencyAmount<Token>.FromRawAmount(token, 100).Multiply(new Percent(15, 100));
        Assert.Equal(new BigInteger(15), amount.Quotient);
    }

    [Fact]
    public void Ether_ProducesEtherAmount()
    {
        var amount = CurrencyAmount<Token>.FromRawAmount(Ether.OnChain(1), 100);
        Assert.Equal(new BigInteger(100), amount.Quotient);
        Assert.Equal(Ether.OnChain(1), amount.Currency);
    }

    [Fact]
    public void TokenAmount_CanBeMaxUint256()
    {
        var amount = CurrencyAmount<Token>.FromRawAmount(new Token(1, ADDRESS_ONE, 18), Constants.MaxUint256);
        Assert.Equal(Constants.MaxUint256, amount.Quotient);
    }

    [Fact]
    public void TokenAmount_CannotExceedMaxUint256()
    {
        Assert.Throws<ArgumentException>(() =>
            CurrencyAmount<Token>.FromRawAmount(new Token(1, ADDRESS_ONE, 18), Constants.MaxUint256 + BigInteger.One)
        );
    }

    [Fact]
    public void TokenAmountQuotient_CannotExceedMaxUint256()
    {
        Assert.Throws<ArgumentException>(() =>
            CurrencyAmount<Token>.FromFractionalAmount(
                new Token(1, ADDRESS_ONE, 18),
                Constants.MaxUint256 * new BigInteger(2) + new BigInteger(2),
                new BigInteger(2)
            )
        );
    }

    [Fact]
    public void TokenAmountNumerator_CanBeGreaterThanUint256IfDenominatorIsGreaterThanOne()
    {
        var amount = CurrencyAmount<Token>.FromFractionalAmount(
            new Token(1, ADDRESS_ONE, 18),
            Constants.MaxUint256 + new BigInteger(2),
            2
        );
        Assert.Equal(Constants.MaxUint256 + new BigInteger(2), amount.Numerator);
    }

    [Fact]
    public void ToFixed_ThrowsForDecimalsGreaterThanCurrencyDecimals()
    {
        var token = new Token(1, ADDRESS_ONE, 0);
        var amount = CurrencyAmount<Token>.FromRawAmount(token, 1000);
        Assert.Throws<ArgumentException>(() => amount.ToFixed(3));
    }

    [Fact]
    public void ToFixed_IsCorrectForZeroDecimals()
    {
        var token = new Token(1, ADDRESS_ONE, 0);
        var amount = CurrencyAmount<Token>.FromRawAmount(token, 123456);
        Assert.Equal("123456", amount.ToFixed(0));
    }

    [Fact]
    public void ToFixed_IsCorrectFor18Decimals()
    {
        var token = new Token(1, ADDRESS_ONE, 18);
        var amount = CurrencyAmount<Token>.FromRawAmount(token, BigInteger.Pow(10, 15));
        Assert.Equal("0.001000000", amount.ToFixed(9));
    }

    [Fact]
    public void ToSignificant_DoesNotThrowForSigFigsGreaterThanCurrencyDecimals()
    {
        var token = new Token(1, ADDRESS_ONE, 0);
        var amount = CurrencyAmount<Token>.FromRawAmount(token, 1000);
        Assert.Equal("1000", amount.ToSignificant(3));
    }

    [Fact]
    public void ToSignificant_IsCorrectForZeroDecimals()
    {
        var token = new Token(1, ADDRESS_ONE, 0);
        var amount = CurrencyAmount<Token>.FromRawAmount(token, 123456);
        Assert.Equal("123400", amount.ToSignificant(4));
    }

    [Fact]
    public void ToSignificant_IsCorrectFor18Decimals()
    {
        var token = new Token(1, ADDRESS_ONE, 18);
        var amount = CurrencyAmount<Token>.FromRawAmount(token, BigInteger.Pow(10, 15));
        Assert.Equal("0.001", amount.ToSignificant(9));
    }

    [Fact]
    public void ToExact_DoesNotThrowForSigFigsGreaterThanCurrencyDecimals()
    {
        var token = new Token(1, ADDRESS_ONE, 0);
        var amount = CurrencyAmount<Token>.FromRawAmount(token, 1000);
        Assert.Equal("1000", amount.ToExact());
    }

    [Fact]
    public void ToExact_IsCorrectForZeroDecimals()
    {
        var token = new Token(1, ADDRESS_ONE, 0);
        var amount = CurrencyAmount<Token>.FromRawAmount(token, 123456);
        Assert.Equal("123456", amount.ToExact());
    }

    [Fact]
    public void ToExact_IsCorrectFor18Decimals()
    {
        var token = new Token(1, ADDRESS_ONE, 18);
        var amount = CurrencyAmount<Token>.FromRawAmount(token, 123 * BigInteger.Pow(10, 13));
        Assert.Equal("0.00123", amount.ToExact());
    }
}

