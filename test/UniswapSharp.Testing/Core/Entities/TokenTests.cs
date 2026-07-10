using UniswapSharp.Core.Entities;

namespace UniswapSharp.Testing.Core.Entities;

public class TokenTests
{
    private const string ADDRESS_ONE = "0x0000000000000000000000000000000000000001";
    private const string ADDRESS_TWO = "0x0000000000000000000000000000000000000002";
    private const string DAI_MAINNET = "0x6B175474E89094C44Da98b954EedeAC495271d0F";

    [Fact]
    public void Constructor_FailsWithInvalidAddress()
    {
        Assert.Throws<ArgumentException>(() => new Token(3, "0xhello00000000000000000000000000000000002", 18));
    }

    [Fact]
    public void Constructor_FailsWithNegativeDecimals()
    {
        Assert.Throws<ArgumentException>(() => new Token(3, ADDRESS_ONE, -1));
    }

    [Fact]
    public void Constructor_FailsWith256Decimals()
    {
        Assert.Throws<ArgumentException>(() => new Token(3, ADDRESS_ONE, 256));
    }

    //[Fact]
    //public void Constructor_FailsWithNonIntegerDecimals()
    //{
    //    Assert.Throws<ArgumentException>(() => new Token(3, ADDRESS_ONE, 1.5));
    //}

    //[Fact]
    //public void Constructor_FailsWithNegativeFOTFees()
    //{
    //    Assert.Throws<ArgumentException>(() => new Token(3, ADDRESS_ONE, 18, null, null, null, BigInteger.Parse("-1"), null));
    //    Assert.Throws<ArgumentException>(() => new Token(3, ADDRESS_ONE, 18, null, null, null, null, BigInteger.Parse("-1")));
    //}

    [Fact]
    public void Constructor_WithBypassChecksum_CreatesTokenWithValidAddress()
    {
        var token = new Token(3, ADDRESS_TWO, 18, null, null, true);
        Assert.Equal(ADDRESS_TWO, token.Address);
    }

    [Fact]
    public void Constructor_WithBypassChecksum_FailsWithInvalidAddress()
    {
        Assert.Throws<ArgumentException>(() => new Token(3, "0xhello00000000000000000000000000000000002", 18, null, null, true));
    }

    [Fact]
    public void Equals_FalseIfAddressDiffers()
    {
        var token1 = new Token(1, ADDRESS_ONE, 18);
        var token2 = new Token(1, ADDRESS_TWO, 18);
        Assert.False(token1.Equals(token2));
    }

    [Fact]
    public void Equals_FalseIfChainIdDiffers()
    {
        var token1 = new Token(3, ADDRESS_ONE, 18);
        var token2 = new Token(1, ADDRESS_ONE, 18);
        Assert.False(token1.Equals(token2));
    }

    [Fact]
    public void Equals_TrueIfOnlyDecimalsDiffer()
    {
        var token1 = new Token(1, ADDRESS_ONE, 9);
        var token2 = new Token(1, ADDRESS_ONE, 18);
        Assert.True(token1.Equals(token2));
    }

    [Fact]
    public void Equals_TrueIfAddressIsSame()
    {
        var token1 = new Token(1, ADDRESS_ONE, 18);
        var token2 = new Token(1, ADDRESS_ONE, 18);
        Assert.True(token1.Equals(token2));
    }

    [Fact]
    public void Equals_TrueOnReferenceEquality()
    {
        var token = new Token(1, ADDRESS_ONE, 18);
        Assert.True(token.Equals(token));
    }

    [Fact]
    public void Equals_TrueEvenIfNameSymbolDecimalsDiffer()
    {
        var tokenA = new Token(1, ADDRESS_ONE, 9, "abc", "def");
        var tokenB = new Token(1, ADDRESS_ONE, 18, "ghi", "jkl");
        Assert.True(tokenA.Equals(tokenB));
    }

    [Fact]
    public void Equals_TrueEvenIfOneTokenIsChecksummedAndOtherIsNot()
    {
        var tokenA = new Token(1, DAI_MAINNET, 18, "DAI", null, false);
        var tokenB = new Token(1, DAI_MAINNET.ToLowerInvariant(), 18, "DAI", null, true);
        Assert.True(tokenA.Equals(tokenB));
    }
}
