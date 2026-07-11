using System.Numerics;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.V4.Utils;

namespace UniswapSharp.Testing.V4.Utils;

// Ported from sdks/v4-sdk/src/utils/priceTickConversions.ts. There is no dedicated upstream test;
// the token vectors are the canonical v3 priceTickConversions vectors (identical behavior for tokens),
// plus native-currency cases exercising the v4 native-aware ordering.
public class PriceTickTests
{
    private static Token Token(int sortOrder, int decimals = 18) =>
        new(1, "0x" + new string((char)('0' + sortOrder), 40), decimals, $"T{sortOrder}", $"token{sortOrder}");

    private static readonly Token token0 = Token(0);
    private static readonly Token token1 = Token(1);
    private static readonly Token token2_6decimals = Token(2, 6);
    private static readonly Ether ETHER = Ether.OnChain(1);

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
        Assert.Equal(-74960, PriceTick.PriceToClosestTick(new Price<BaseCurrency, BaseCurrency>(token1, token0, 1, 1800)));

    [Fact]
    public void PriceToClosestTick_1_t1_per_1800_t0() =>
        Assert.Equal(-74960, PriceTick.PriceToClosestTick(new Price<BaseCurrency, BaseCurrency>(token0, token1, 1800, 1)));

    [Fact]
    public void PriceToClosestTick_101_t2_per_t0() =>
        Assert.Equal(-276225, PriceTick.PriceToClosestTick(
            new Price<BaseCurrency, BaseCurrency>(token0, token2_6decimals, BigInteger.Pow(10, 20), 101_000_000)));

    [Fact]
    public void PriceToClosestTick_1_t0_per_101_t2() =>
        Assert.Equal(-276225, PriceTick.PriceToClosestTick(
            new Price<BaseCurrency, BaseCurrency>(token2_6decimals, token0, 101_000_000, BigInteger.Pow(10, 20))));

    // ---- reciprocal round-trips ----
    [Theory]
    [InlineData(-74960)]
    [InlineData(74960)]
    public void Reciprocal_t1_t0(int tick) =>
        Assert.Equal(tick, PriceTick.PriceToClosestTick(PriceTick.TickToPrice(token1, token0, tick)));

    [Theory]
    [InlineData(-74960)]
    [InlineData(74960)]
    public void Reciprocal_t0_t1(int tick) =>
        Assert.Equal(tick, PriceTick.PriceToClosestTick(PriceTick.TickToPrice(token0, token1, tick)));

    // ---- native currency (v4-specific ordering: native sorts first) ----
    [Fact]
    public void TickToPrice_NativeSortsBefore_UsesSortedBranch()
    {
        // Native always sorts first, so base=ETHER uses the Q192-numerator branch just like a token0<token1 pair.
        Assert.Equal(
            PriceTick.TickToPrice(token0, token1, 74959).ToSignificant(5),
            PriceTick.TickToPrice(ETHER, token1, 74959).ToSignificant(5));
    }

    [Theory]
    [InlineData(-74960)]
    [InlineData(74960)]
    public void Reciprocal_native_token(int tick) =>
        Assert.Equal(tick, PriceTick.PriceToClosestTick(PriceTick.TickToPrice(ETHER, token1, tick)));

    [Theory]
    [InlineData(-74960)]
    [InlineData(74960)]
    public void Reciprocal_token_native(int tick) =>
        Assert.Equal(tick, PriceTick.PriceToClosestTick(PriceTick.TickToPrice(token1, ETHER, tick)));
}
