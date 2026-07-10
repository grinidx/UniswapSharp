using System.Numerics;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.V3.Utils;

namespace UniswapSharp.Testing.V3.Utils;

// Ported from sdks/v3-sdk/src/utils/priceTickConversions.test.ts
public class PriceTickTests
{
    // Upstream helper: address is the sort-order digit repeated 40 times.
    private static Token Token(int sortOrder, int decimals = 18) =>
        new(1, "0x" + new string((char)('0' + sortOrder), 40), decimals, $"T{sortOrder}", $"token{sortOrder}");

    private static readonly Token token0 = Token(0);
    private static readonly Token token1 = Token(1);
    private static readonly Token token2_6decimals = Token(2, 6);

    // ---- #tickToPrice ----

    [Fact]
    public void TickToPrice_1800_t0_per_t1() =>
        Assert.Equal("1800", PriceTick.TickToPrice(token1, token0, -74959).ToSignificant(5));

    [Fact]
    public void TickToPrice_1_t1_per_1800_t0() =>
        Assert.Equal("0.00055556", PriceTick.TickToPrice(token0, token1, -74959).ToSignificant(5));

    [Fact]
    public void TickToPrice_1800_t1_per_t0() =>
        Assert.Equal("1800", PriceTick.TickToPrice(token0, token1, 74959).ToSignificant(5));

    [Fact]
    public void TickToPrice_1_t0_per_1800_t1() =>
        Assert.Equal("0.00055556", PriceTick.TickToPrice(token1, token0, 74959).ToSignificant(5));

    [Fact]
    public void TickToPrice_101_t2_per_t0() =>
        Assert.Equal("1.01", PriceTick.TickToPrice(token0, token2_6decimals, -276225).ToSignificant(5));

    [Fact]
    public void TickToPrice_1_t0_per_101_t2() =>
        Assert.Equal("0.99015", PriceTick.TickToPrice(token2_6decimals, token0, -276225).ToSignificant(5));

    [Fact]
    public void TickToPrice_1_t2_per_101_t0() =>
        Assert.Equal("0.99015", PriceTick.TickToPrice(token0, token2_6decimals, -276423).ToSignificant(5));

    [Fact]
    public void TickToPrice_101_t0_per_t2() =>
        Assert.Equal("1.0099", PriceTick.TickToPrice(token2_6decimals, token0, -276423).ToSignificant(5));

    // ---- #priceToClosestTick ----

    [Fact]
    public void PriceToClosestTick_1800_t0_per_t1() =>
        Assert.Equal(-74960, PriceTick.PriceToClosestTick(new Price<Token, Token>(token1, token0, 1, 1800)));

    [Fact]
    public void PriceToClosestTick_1_t1_per_1800_t0() =>
        Assert.Equal(-74960, PriceTick.PriceToClosestTick(new Price<Token, Token>(token0, token1, 1800, 1)));

    [Fact]
    public void PriceToClosestTick_101_t2_per_t0() =>
        Assert.Equal(-276225, PriceTick.PriceToClosestTick(
            new Price<Token, Token>(token0, token2_6decimals, BigInteger.Pow(10, 20), 101_000_000)));

    [Fact]
    public void PriceToClosestTick_1_t0_per_101_t2() =>
        Assert.Equal(-276225, PriceTick.PriceToClosestTick(
            new Price<Token, Token>(token2_6decimals, token0, 101_000_000, BigInteger.Pow(10, 20))));

    // ---- reciprocal with tickToPrice ----

    [Theory]
    [InlineData(-74960)]
    [InlineData(74960)]
    public void PriceToClosestTick_Reciprocal_t1_t0(int tick) =>
        Assert.Equal(tick, PriceTick.PriceToClosestTick(PriceTick.TickToPrice(token1, token0, tick)));

    [Theory]
    [InlineData(-74960)]
    [InlineData(74960)]
    public void PriceToClosestTick_Reciprocal_t0_t1(int tick) =>
        Assert.Equal(tick, PriceTick.PriceToClosestTick(PriceTick.TickToPrice(token0, token1, tick)));

    [Fact]
    public void PriceToClosestTick_Reciprocal_t0_t2() =>
        Assert.Equal(-276225, PriceTick.PriceToClosestTick(PriceTick.TickToPrice(token0, token2_6decimals, -276225)));

    [Fact]
    public void PriceToClosestTick_Reciprocal_t2_t0() =>
        Assert.Equal(-276225, PriceTick.PriceToClosestTick(PriceTick.TickToPrice(token2_6decimals, token0, -276225)));
}
