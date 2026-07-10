using System.Numerics;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.V3.Entities;
using UniswapSharp.V3.Utils;
using static UniswapSharp.V3.Constants;

namespace UniswapSharp.Testing.V3.Entities;

public class PoolTests(ITestOutputHelper output) :
    XunitContextBase(output)
{
    private static readonly BigInteger ONE_ETHER = BigInteger.Pow(10, 18);

    private static readonly Token USDC = new(1, "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48", 6, "USDC", "USD Coin");
    private static readonly Token DAI = new(1, "0x6B175474E89094C44Da98b954EedeAC495271d0F", 18, "DAI", "DAI Stablecoin");

    [Fact]
    public void Constructor_CannotBeUsedForTokensOnDifferentChains()
    {
        Assert.Throws<InvalidOperationException>(() => { new Pool(USDC, Weth9.Tokens[3], FeeAmount.MEDIUM, EncodeSqrtRatioX96.Encode(1, 1), 0, 0, new List<object>()); });
    }

    //[Fact]
    //public void Fee_MustBeInteger()
    //{
    //    Assert.Throws<Exception>(() =>
    //    {
    //        new Pool(USDC, Weth9.Tokens[1], FeeAmount.MEDIUM + 0.5, EncodeSqrtRatioX96.Encode(1, 1), 0, 0, new List<object>());
    //    });
    //}

    //[Fact]
    //public void Fee_CannotBeMoreThan1e6()
    //{
    //    Assert.Throws<Exception>(() =>
    //    {
    //        new Pool(USDC, Weth9.Tokens[1], 1e6, EncodeSqrtRatioX96.Encode(1, 1), 0, 0, new List<object>());
    //    });
    //}

    [Fact]
    public void CannotBeGivenTwoOfTheSameToken()
    {
        Assert.Throws<InvalidOperationException>(() => { new Pool(USDC, USDC, FeeAmount.MEDIUM, EncodeSqrtRatioX96.Encode(1, 1), 0, 0, new List<object>()); });
    }

    [Fact]
    public void Price_MustBeWithinTickPriceBounds()
    {
        Assert.Throws<ArgumentException>(() => { new Pool(USDC, Weth9.Tokens[1], FeeAmount.MEDIUM, EncodeSqrtRatioX96.Encode(1, 1), 0, 1, new List<object>()); });
        Assert.Throws<ArgumentException>(() =>
        {
            new Pool(USDC, Weth9.Tokens[1], FeeAmount.MEDIUM, EncodeSqrtRatioX96.Encode(1, 1) + BigInteger.One, 0, -1, new List<object>());
        });
    }

    [Fact]
    public void WorksWithValidArgumentsForEmptyPoolMediumFee()
    {
        new Pool(USDC, Weth9.Tokens[1], FeeAmount.MEDIUM, EncodeSqrtRatioX96.Encode(1, 1), 0, 0, new List<object>());
    }

    [Fact]
    public void WorksWithValidArgumentsForEmptyPoolLowFee()
    {
        new Pool(USDC, Weth9.Tokens[1], FeeAmount.LOW, EncodeSqrtRatioX96.Encode(1, 1), 0, 0, new List<object>());
    }

    [Fact]
    public void WorksWithValidArgumentsForEmptyPoolLowestFee()
    {
        new Pool(USDC, Weth9.Tokens[1], FeeAmount.LOWEST, EncodeSqrtRatioX96.Encode(1, 1), 0, 0, new List<object>());
    }

    [Fact]
    public void WorksWithValidArgumentsForEmptyPoolHighFee()
    {
        new Pool(USDC, Weth9.Tokens[1], FeeAmount.HIGH, EncodeSqrtRatioX96.Encode(1, 1), 0, 0, new List<object>());
    }

    [Fact]
    public void GetAddress_MatchesAnExample()
    {
        var result = Pool.GetAddress(USDC, DAI, FeeAmount.LOW);
        Assert.Equal("0x6c6Bc977E13Df9b0de53b251522280BB72383700", result);
    }

    [Fact]
    public void Token0_AlwaysIsTheTokenThatSortsBefore()
    {
        var pool = new Pool(USDC, DAI, FeeAmount.LOW, EncodeSqrtRatioX96.Encode(1, 1), 0, 0, new List<object>());
        Assert.Equal(DAI, pool.Token0);
        pool = new Pool(DAI, USDC, FeeAmount.LOW, EncodeSqrtRatioX96.Encode(1, 1), 0, 0, new List<object>());
        Assert.Equal(DAI, pool.Token0);
    }

    [Fact]
    public void Token1_AlwaysIsTheTokenThatSortsAfter()
    {
        var pool = new Pool(USDC, DAI, FeeAmount.LOW, EncodeSqrtRatioX96.Encode(1, 1), 0, 0, new List<object>());
        Assert.Equal(USDC, pool.Token1);
        pool = new Pool(DAI, USDC, FeeAmount.LOW, EncodeSqrtRatioX96.Encode(1, 1), 0, 0, new List<object>());
        Assert.Equal(USDC, pool.Token1);
    }

    //[Fact]
    //public void Token0Price_ReturnsPriceOfToken0InTermsOfToken1()
    //{
    //    Assert.Equal("1.01",
    //        new Pool(USDC, DAI, FeeAmount.LOW, EncodeSqrtRatioX96.Encode(101e6, 100e18), 0,
    //            TickMath.GetTickAtSqrtRatio(EncodeSqrtRatioX96.Encode(101e6, 100e18)), new List<object>()).Token0Price.ToSignificant(5));
    //    Assert.Equal("1.01",
    //        new Pool(DAI, USDC, FeeAmount.LOW, EncodeSqrtRatioX96.Encode(101e6, 100e18), 0,
    //            TickMath.GetTickAtSqrtRatio(EncodeSqrtRatioX96.Encode(101e6, 100e18)), new List<object>()).Token0Price.ToSignificant(5));
    //}

    //[Fact]
    //public void Token1Price_ReturnsPriceOfToken1InTermsOfToken0()
    //{
    //    Assert.Equal("0.9901",
    //        new Pool(USDC, DAI, FeeAmount.LOW, EncodeSqrtRatioX96.Encode(101e6, 100e18), 0,
    //            TickMath.GetTickAtSqrtRatio(EncodeSqrtRatioX96.Encode(101e6, 100e18)), new List<object>()).Token1Price.ToSignificant(5));
    //    Assert.Equal("0.9901",
    //        new Pool(DAI, USDC, FeeAmount.LOW, EncodeSqrtRatioX96.Encode(101e6, 100e18), 0,
    //            TickMath.GetTickAtSqrtRatio(EncodeSqrtRatioX96.Encode(101e6, 100e18)), new List<object>()).Token1Price.ToSignificant(5));
    //}

    [Fact]
    public void PriceOf_ReturnsPriceOfTokenInTermsOfOtherToken()
    {
        var pool = new Pool(USDC, DAI, FeeAmount.LOW, EncodeSqrtRatioX96.Encode(1, 1), 0, 0, new List<object>());
        Assert.Equal(pool.Token0Price, pool.PriceOf(DAI));
        Assert.Equal(pool.Token1Price, pool.PriceOf(USDC));
    }

    [Fact]
    public void PriceOf_ThrowsIfInvalidToken()
    {
        var pool = new Pool(USDC, DAI, FeeAmount.LOW, EncodeSqrtRatioX96.Encode(1, 1), 0, 0, new List<object>());
        Assert.Throws<ArgumentException>(() => pool.PriceOf(Weth9.Tokens[1]));
    }

    [Fact]
    public void ChainId_ReturnsTheToken0ChainId()
    {
        var pool = new Pool(USDC, DAI, FeeAmount.LOW, EncodeSqrtRatioX96.Encode(1, 1), 0, 0, new List<object>());
        Assert.Equal(1, pool.ChainId);
        pool = new Pool(DAI, USDC, FeeAmount.LOW, EncodeSqrtRatioX96.Encode(1, 1), 0, 0, new List<object>());
        Assert.Equal(1, pool.ChainId);
    }

    [Fact]
    public void InvolvesToken_ReturnsTrueForInvolvedTokens()
    {
        var pool = new Pool(USDC, DAI, FeeAmount.LOW, EncodeSqrtRatioX96.Encode(1, 1), 0, 0, new List<object>());
        Assert.True(pool.InvolvesToken(USDC));
        Assert.True(pool.InvolvesToken(DAI));
        Assert.False(pool.InvolvesToken(Weth9.Tokens[1]));
    }


    [Fact]
    public async Task Swaps_GetOutputAmount_USDCToDAI()
    {
        var pool = new Pool(USDC, DAI, FeeAmount.LOW, EncodeSqrtRatioX96.Encode(1, 1), ONE_ETHER, 0, new List<Tick>
        {
            new(
                NearestUsableTick.Find(TickMath.MIN_TICK, TICK_SPACINGS[FeeAmount.LOW]),
                ONE_ETHER,
                ONE_ETHER
            ),
            new(
                NearestUsableTick.Find(TickMath.MAX_TICK, TICK_SPACINGS[FeeAmount.LOW]),
                ONE_ETHER * NEGATIVE_ONE,
                ONE_ETHER
            )
        });

        var inputAmount = CurrencyAmount<Token>.FromRawAmount(USDC, 100);
        var outputAmount = (await pool.GetOutputAmount(inputAmount)).outputAmount;
        Assert.True(outputAmount.Currency.Equals(DAI));
        Assert.Equal(98, outputAmount.Quotient);
    }

    [Fact]
    public async Task Swaps_GetOutputAmount_DAIToUSDC()
    {
        var pool = new Pool(USDC, DAI, FeeAmount.LOW, EncodeSqrtRatioX96.Encode(1, 1), ONE_ETHER, 0, new List<Tick>
        {
            new(
                NearestUsableTick.Find(TickMath.MIN_TICK, TICK_SPACINGS[FeeAmount.LOW]),
                ONE_ETHER,
                ONE_ETHER
            ),

            new(
                NearestUsableTick.Find(TickMath.MAX_TICK, TICK_SPACINGS[FeeAmount.LOW]),
                ONE_ETHER * NEGATIVE_ONE,
                ONE_ETHER
            )
        });

        var inputAmount = CurrencyAmount<Token>.FromRawAmount(DAI, 100);
        var outputAmount = (await pool.GetOutputAmount(inputAmount)).outputAmount;
        Assert.True(outputAmount.Currency.Equals(USDC));
        Assert.Equal(98, outputAmount.Quotient);
    }

    [Fact]
    public async Task Swaps_GetInputAmount_USDCToDAI()
    {
        var pool = new Pool(USDC, DAI, FeeAmount.LOW, EncodeSqrtRatioX96.Encode(1, 1), ONE_ETHER, 0, new List<Tick>
        {
            new(
                NearestUsableTick.Find(TickMath.MIN_TICK, TICK_SPACINGS[FeeAmount.LOW]),
                ONE_ETHER,
                ONE_ETHER
            ),
            new(
                NearestUsableTick.Find(TickMath.MAX_TICK, TICK_SPACINGS[FeeAmount.LOW]),
                ONE_ETHER * NEGATIVE_ONE,
                ONE_ETHER
            )
        });

        var outputAmount = CurrencyAmount<Token>.FromRawAmount(DAI, 98);
        var inputAmount = (await pool.GetInputAmount(outputAmount)).inputAmount;
        Assert.True(inputAmount.Currency.Equals(USDC));
        Assert.Equal(100, inputAmount.Quotient);
    }

    [Fact]
    public async Task Swaps_GetInputAmount_DAIToUSDC()
    {
        var pool = new Pool(USDC, DAI, FeeAmount.LOW, EncodeSqrtRatioX96.Encode(1, 1), ONE_ETHER, 0, new List<Tick>
        {
            new(
                NearestUsableTick.Find(TickMath.MIN_TICK, TICK_SPACINGS[FeeAmount.LOW]),
                ONE_ETHER,
                ONE_ETHER
            ),
            new(
                NearestUsableTick.Find(TickMath.MAX_TICK, TICK_SPACINGS[FeeAmount.LOW]),
                ONE_ETHER * NEGATIVE_ONE,
                ONE_ETHER
            )
        });

        var outputAmount = CurrencyAmount<Token>.FromRawAmount(USDC, 98);
        var inputAmount = (await pool.GetInputAmount(outputAmount)).inputAmount;
        Assert.True(inputAmount.Currency.Equals(DAI));
        Assert.Equal(100, inputAmount.Quotient);
    }

    [Fact]
    public void BigNums_PriceLimit_CorrectlyComparesTwoBigIntegers()
    {
        var bigNum1 = BigInteger.Parse(long.MaxValue.ToString()) + BigInteger.One;
        var bigNum2 = BigInteger.Parse(long.MaxValue.ToString()) + BigInteger.One;
        Assert.Equal(bigNum1, bigNum2);
    }

    [Fact]
    public async Task BigNums_CorrectlyHandlesTwoBigIntegers()
    {
        var bigNum1 = BigInteger.Parse(long.MaxValue.ToString()) + BigInteger.One;
        var bigNum2 = BigInteger.Parse(long.MaxValue.ToString()) + BigInteger.One;

        var pool = new Pool(USDC, DAI, FeeAmount.LOW, EncodeSqrtRatioX96.Encode(bigNum1, bigNum2), ONE_ETHER, 0, new List<Tick>
        {
            new(
                NearestUsableTick.Find(TickMath.MIN_TICK, TICK_SPACINGS[FeeAmount.LOW]),
                ONE_ETHER,
                ONE_ETHER
            ),
            new(
                NearestUsableTick.Find(TickMath.MAX_TICK, TICK_SPACINGS[FeeAmount.LOW]),
                ONE_ETHER * NEGATIVE_ONE,
                ONE_ETHER
            )
        });

        var inputAmount = CurrencyAmount<Token>.FromRawAmount(USDC, 100);
        var outputAmount = (await pool.GetOutputAmount(inputAmount)).outputAmount;
        pool.GetInputAmount(outputAmount);
        Assert.True(outputAmount.Currency.Equals(DAI));
    }
}
