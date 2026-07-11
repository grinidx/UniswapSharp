using System.Numerics;
using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.V2;
using UniswapSharp.V2.Entities;

namespace UniswapSharp.Testing.V2;

public class PairTests
{
    private static readonly Token USDC = new(1, "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48", 18, "USDC", "USD Coin");
    private static readonly Token DAI = new(1, "0x6B175474E89094C44Da98b954EedeAC495271d0F", 18, "DAI", "DAI Stablecoin");

    private static readonly Token USDC_SEPOLIA = new(11155111, "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48", 18, "USDC", "USD Coin");
    private static readonly Token DAI_SEPOLIA = new(11155111, "0x6B175474E89094C44Da98b954EedeAC495271d0F", 18, "DAI", "DAI Stablecoin");

    private static readonly Token WETH1 = Weth9.Tokens[1];
    private static readonly Token WETH3 = Weth9.Tokens[3];

    // ---- computePairAddress ----

    [Fact]
    public void ComputePairAddress_ComputesCorrectAddress()
    {
        var result = Pair.ComputePairAddress("0x1111111111111111111111111111111111111111", USDC, DAI);
        Assert.Equal("0xb50b5182D6a47EC53a469395AF44e371d7C76ed4", result);
    }

    [Fact]
    public void ComputePairAddress_SameResultRegardlessOfTokenOrder()
    {
        var resultA = Pair.ComputePairAddress("0x1111111111111111111111111111111111111111", USDC, DAI);
        var resultB = Pair.ComputePairAddress("0x1111111111111111111111111111111111111111", DAI, USDC);
        Assert.Equal(resultA, resultB);
    }

    // ---- constructor ----

    [Fact]
    public void Constructor_CannotBeUsedForTokensOnDifferentChains()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new Pair(CurrencyAmount<Token>.FromRawAmount(USDC, 100), CurrencyAmount<Token>.FromRawAmount(Weth9.Tokens[3], 100)));
        Assert.Equal("CHAIN_IDS", ex.Message);
    }

    // ---- getAddress ----

    [Fact]
    public void GetAddress_ReturnsCorrectAddress()
    {
        Assert.Equal("0xAE461cA67B15dc8dc81CE7615e0320dA1A9aB8D5", Pair.GetAddress(USDC, DAI));
    }

    [Fact]
    public void GetAddress_ReturnsDefaultMapAddressForTestnet()
    {
        Assert.Equal(
            Pair.ComputePairAddress(Addresses.V2_FACTORY_ADDRESSES[ChainId.SEPOLIA], USDC_SEPOLIA, DAI_SEPOLIA),
            Pair.GetAddress(USDC_SEPOLIA, DAI_SEPOLIA));
    }

    // ---- token0 / token1 ----

    [Fact]
    public void Token0_IsAlwaysTheTokenThatSortsBefore()
    {
        Assert.Equal(DAI, new Pair(CurrencyAmount<Token>.FromRawAmount(USDC, 100), CurrencyAmount<Token>.FromRawAmount(DAI, 100)).Token0);
        Assert.Equal(DAI, new Pair(CurrencyAmount<Token>.FromRawAmount(DAI, 100), CurrencyAmount<Token>.FromRawAmount(USDC, 100)).Token0);
    }

    [Fact]
    public void Token1_IsAlwaysTheTokenThatSortsAfter()
    {
        Assert.Equal(USDC, new Pair(CurrencyAmount<Token>.FromRawAmount(USDC, 100), CurrencyAmount<Token>.FromRawAmount(DAI, 100)).Token1);
        Assert.Equal(USDC, new Pair(CurrencyAmount<Token>.FromRawAmount(DAI, 100), CurrencyAmount<Token>.FromRawAmount(USDC, 100)).Token1);
    }

    // ---- reserve0 / reserve1 ----

    [Fact]
    public void Reserve0_ComesFromTokenThatSortsBefore()
    {
        AssertAmount(CurrencyAmount<Token>.FromRawAmount(DAI, 101),
            new Pair(CurrencyAmount<Token>.FromRawAmount(USDC, 100), CurrencyAmount<Token>.FromRawAmount(DAI, 101)).Reserve0);
        AssertAmount(CurrencyAmount<Token>.FromRawAmount(DAI, 101),
            new Pair(CurrencyAmount<Token>.FromRawAmount(DAI, 101), CurrencyAmount<Token>.FromRawAmount(USDC, 100)).Reserve0);
    }

    [Fact]
    public void Reserve1_ComesFromTokenThatSortsAfter()
    {
        AssertAmount(CurrencyAmount<Token>.FromRawAmount(USDC, 100),
            new Pair(CurrencyAmount<Token>.FromRawAmount(USDC, 100), CurrencyAmount<Token>.FromRawAmount(DAI, 101)).Reserve1);
        AssertAmount(CurrencyAmount<Token>.FromRawAmount(USDC, 100),
            new Pair(CurrencyAmount<Token>.FromRawAmount(DAI, 101), CurrencyAmount<Token>.FromRawAmount(USDC, 100)).Reserve1);
    }

    // ---- token0Price / token1Price ----

    [Fact]
    public void Token0Price_ReturnsPriceOfToken0InTermsOfToken1()
    {
        Assert.True(new Price<Token, Token>(DAI, USDC, 100, 101).Equals(
            new Pair(CurrencyAmount<Token>.FromRawAmount(USDC, 101), CurrencyAmount<Token>.FromRawAmount(DAI, 100)).Token0Price));
        Assert.True(new Price<Token, Token>(DAI, USDC, 100, 101).Equals(
            new Pair(CurrencyAmount<Token>.FromRawAmount(DAI, 100), CurrencyAmount<Token>.FromRawAmount(USDC, 101)).Token0Price));
    }

    [Fact]
    public void Token1Price_ReturnsPriceOfToken1InTermsOfToken0()
    {
        Assert.True(new Price<Token, Token>(USDC, DAI, 101, 100).Equals(
            new Pair(CurrencyAmount<Token>.FromRawAmount(USDC, 101), CurrencyAmount<Token>.FromRawAmount(DAI, 100)).Token1Price));
        Assert.True(new Price<Token, Token>(USDC, DAI, 101, 100).Equals(
            new Pair(CurrencyAmount<Token>.FromRawAmount(DAI, 100), CurrencyAmount<Token>.FromRawAmount(USDC, 101)).Token1Price));
    }

    // ---- priceOf ----

    [Fact]
    public void PriceOf_ReturnsPriceOfTokenInTermsOfOtherToken()
    {
        var pair = new Pair(CurrencyAmount<Token>.FromRawAmount(USDC, 101), CurrencyAmount<Token>.FromRawAmount(DAI, 100));
        Assert.True(pair.Token0Price.Equals(pair.PriceOf(DAI)));
        Assert.True(pair.Token1Price.Equals(pair.PriceOf(USDC)));
    }

    [Fact]
    public void PriceOf_ThrowsIfInvalidToken()
    {
        var pair = new Pair(CurrencyAmount<Token>.FromRawAmount(USDC, 101), CurrencyAmount<Token>.FromRawAmount(DAI, 100));
        var ex = Assert.Throws<ArgumentException>(() => pair.PriceOf(WETH1));
        Assert.Equal("TOKEN", ex.Message);
    }

    // ---- reserveOf ----

    [Fact]
    public void ReserveOf_ReturnsReservesOfGivenToken()
    {
        AssertAmount(CurrencyAmount<Token>.FromRawAmount(USDC, 100),
            new Pair(CurrencyAmount<Token>.FromRawAmount(USDC, 100), CurrencyAmount<Token>.FromRawAmount(DAI, 101)).ReserveOf(USDC));
        AssertAmount(CurrencyAmount<Token>.FromRawAmount(USDC, 100),
            new Pair(CurrencyAmount<Token>.FromRawAmount(DAI, 101), CurrencyAmount<Token>.FromRawAmount(USDC, 100)).ReserveOf(USDC));
    }

    [Fact]
    public void ReserveOf_ThrowsIfNotInThePair()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new Pair(CurrencyAmount<Token>.FromRawAmount(DAI, 101), CurrencyAmount<Token>.FromRawAmount(USDC, 100)).ReserveOf(WETH1));
        Assert.Equal("TOKEN", ex.Message);
    }

    // ---- chainId ----

    [Fact]
    public void ChainId_ReturnsToken0ChainId()
    {
        Assert.Equal(1, new Pair(CurrencyAmount<Token>.FromRawAmount(USDC, 100), CurrencyAmount<Token>.FromRawAmount(DAI, 100)).ChainId);
        Assert.Equal(1, new Pair(CurrencyAmount<Token>.FromRawAmount(DAI, 100), CurrencyAmount<Token>.FromRawAmount(USDC, 100)).ChainId);
    }

    // ---- involvesToken ----

    [Fact]
    public void InvolvesToken()
    {
        var pair = new Pair(CurrencyAmount<Token>.FromRawAmount(USDC, 100), CurrencyAmount<Token>.FromRawAmount(DAI, 100));
        Assert.True(pair.InvolvesToken(USDC));
        Assert.True(pair.InvolvesToken(DAI));
        Assert.False(pair.InvolvesToken(WETH1));
    }

    // ---- getInputAmount / getOutputAmount (fee on transfer) ----

    private static readonly Token BLAST = new(1, "0x3ed643e9032230f01c6c36060e305ab53ad3b482", 18, "BLAST", "BLAST",
        false, new BigInteger(400), new BigInteger(10000));
    private static readonly Token BLAST_WITHOUT_TAX = new(1, "0x3ed643e9032230f01c6c36060e305ab53ad3b482", 18, "BLAST", "BLAST", false);
    private static readonly Token BLASTERS = new(1, "0xab98093C7232E98A47D7270CE0c1c2106f61C73b", 9, "BLAST", "BLASTERS",
        false, new BigInteger(300), new BigInteger(350));
    private static readonly Token BLASTERS_WITHOUT_TAX = new(1, "0xab98093C7232E98A47D7270CE0c1c2106f61C73b", 9, "BLAST", "BLASTERS", false);

    [Fact]
    public void GetOutputAmount_WithFotFees_BlastersToBlast()
    {
        var pair = new Pair(CurrencyAmount<Token>.FromRawAmount(BLASTERS, 10000), CurrencyAmount<Token>.FromRawAmount(BLAST, 10000));
        var (output, _) = pair.GetOutputAmount(CurrencyAmount<Token>.FromRawAmount(BLASTERS_WITHOUT_TAX, 100), true);
        Assert.Equal("0.00000000000000009", output.ToExact());
    }

    [Fact]
    public void GetInputAmount_WithFotFees_BlastersToBlast()
    {
        var pair = new Pair(CurrencyAmount<Token>.FromRawAmount(BLASTERS, 10000), CurrencyAmount<Token>.FromRawAmount(BLAST, 10000));
        var (input, _) = pair.GetInputAmount(CurrencyAmount<Token>.FromRawAmount(BLAST_WITHOUT_TAX, 91), true);
        Assert.Equal("0.000000101", input.ToExact());
    }

    [Fact]
    public void GetOutputAmount_WithoutFotFees_BlastersToBlast()
    {
        var pair = new Pair(CurrencyAmount<Token>.FromRawAmount(BLASTERS, 10000), CurrencyAmount<Token>.FromRawAmount(BLAST, 10000));
        var (output, _) = pair.GetOutputAmount(CurrencyAmount<Token>.FromRawAmount(BLASTERS_WITHOUT_TAX, 100), false);
        Assert.Equal("0.000000000000000098", output.ToExact());
    }

    [Fact]
    public void GetInputAmount_WithoutFotFees_BlastersToBlast()
    {
        var pair = new Pair(CurrencyAmount<Token>.FromRawAmount(BLASTERS, 10000), CurrencyAmount<Token>.FromRawAmount(BLAST, 10000));
        var (input, _) = pair.GetInputAmount(CurrencyAmount<Token>.FromRawAmount(BLAST_WITHOUT_TAX, 91), false);
        Assert.Equal("0.000000093", input.ToExact());
    }

    // ---- miscellaneous: getLiquidityMinted / getLiquidityValue ----

    private static readonly Token LiqA = new(3, "0x0000000000000000000000000000000000000001", 18);
    private static readonly Token LiqB = new(3, "0x0000000000000000000000000000000000000002", 18);

    [Fact]
    public void GetLiquidityMinted_Zero()
    {
        var pair = new Pair(CurrencyAmount<Token>.FromRawAmount(LiqA, 0), CurrencyAmount<Token>.FromRawAmount(LiqB, 0));

        Assert.Throws<InsufficientInputAmountError>(() => pair.GetLiquidityMinted(
            CurrencyAmount<Token>.FromRawAmount(pair.LiquidityToken, 0),
            CurrencyAmount<Token>.FromRawAmount(LiqA, 1000),
            CurrencyAmount<Token>.FromRawAmount(LiqB, 1000)));

        Assert.Throws<InsufficientInputAmountError>(() => pair.GetLiquidityMinted(
            CurrencyAmount<Token>.FromRawAmount(pair.LiquidityToken, 0),
            CurrencyAmount<Token>.FromRawAmount(LiqA, 1000000),
            CurrencyAmount<Token>.FromRawAmount(LiqB, 1)));

        var liquidity = pair.GetLiquidityMinted(
            CurrencyAmount<Token>.FromRawAmount(pair.LiquidityToken, 0),
            CurrencyAmount<Token>.FromRawAmount(LiqA, 1001),
            CurrencyAmount<Token>.FromRawAmount(LiqB, 1001));
        Assert.Equal("1", liquidity.Quotient.ToString());
    }

    [Fact]
    public void GetLiquidityMinted_NonZero()
    {
        var pair = new Pair(CurrencyAmount<Token>.FromRawAmount(LiqA, 10000), CurrencyAmount<Token>.FromRawAmount(LiqB, 10000));
        var liquidity = pair.GetLiquidityMinted(
            CurrencyAmount<Token>.FromRawAmount(pair.LiquidityToken, 10000),
            CurrencyAmount<Token>.FromRawAmount(LiqA, 2000),
            CurrencyAmount<Token>.FromRawAmount(LiqB, 2000));
        Assert.Equal("2000", liquidity.Quotient.ToString());
    }

    [Fact]
    public void GetLiquidityValue_FeeOff()
    {
        var pair = new Pair(CurrencyAmount<Token>.FromRawAmount(LiqA, 1000), CurrencyAmount<Token>.FromRawAmount(LiqB, 1000));

        var v1 = pair.GetLiquidityValue(LiqA,
            CurrencyAmount<Token>.FromRawAmount(pair.LiquidityToken, 1000),
            CurrencyAmount<Token>.FromRawAmount(pair.LiquidityToken, 1000), false);
        Assert.True(v1.Currency.Equals(LiqA));
        Assert.Equal("1000", v1.Quotient.ToString());

        var v2 = pair.GetLiquidityValue(LiqA,
            CurrencyAmount<Token>.FromRawAmount(pair.LiquidityToken, 1000),
            CurrencyAmount<Token>.FromRawAmount(pair.LiquidityToken, 500), false);
        Assert.True(v2.Currency.Equals(LiqA));
        Assert.Equal("500", v2.Quotient.ToString());

        var v3 = pair.GetLiquidityValue(LiqB,
            CurrencyAmount<Token>.FromRawAmount(pair.LiquidityToken, 1000),
            CurrencyAmount<Token>.FromRawAmount(pair.LiquidityToken, 1000), false);
        Assert.True(v3.Currency.Equals(LiqB));
        Assert.Equal("1000", v3.Quotient.ToString());
    }

    [Fact]
    public void GetLiquidityValue_FeeOn()
    {
        var pair = new Pair(CurrencyAmount<Token>.FromRawAmount(LiqA, 1000), CurrencyAmount<Token>.FromRawAmount(LiqB, 1000));
        var liquidityValue = pair.GetLiquidityValue(LiqA,
            CurrencyAmount<Token>.FromRawAmount(pair.LiquidityToken, 500),
            CurrencyAmount<Token>.FromRawAmount(pair.LiquidityToken, 500),
            true, BigInteger.Parse("250000")); // 500 ** 2
        Assert.True(liquidityValue.Currency.Equals(LiqA));
        Assert.Equal("917", liquidityValue.Quotient.ToString()); // ceiling(1000 - (500 * (1 / 6)))
    }

    private static void AssertAmount(CurrencyAmount<Token> expected, CurrencyAmount<Token> actual)
    {
        Assert.True(expected.Currency.Equals(actual.Currency));
        Assert.Equal(expected.Quotient, actual.Quotient);
    }
}
