using UniswapSharp.LiquidityLauncher;
using UniswapSharp.LiquidityLauncher.Config;

namespace UniswapSharp.Testing.LiquidityLauncher.Config;

// Ported from sdks/liquidity-launcher-sdk/src/config/positions.test.ts (upstream #651).
public class PositionsTests
{
    private const string ZERO_ADDRESS = Constants.ZERO_ADDRESS;
    private const string TOKEN_HIGH = "0x" + "ffffffffffffffffffffffffffffffffffffffff";
    private const string TOKEN_LOW = "0x" + "0000000000000000000000000000000000000001";
    private const string CURRENCY_HIGH = TOKEN_HIGH;
    private const int TICK_SPACING = 200;

    private static IReadOnlyList<CustomRangeInput> Range(double min, double max, double liquidity = 100) =>
        new[] { new CustomRangeInput(min, max, liquidity) };

    [Fact]
    public void MirrorsAnAsymmetricCustomRangeOntoTheReciprocalBand_ForANativeCurrency0Launch()
    {
        var range = Range(-10, 40);

        // currency sorts as currency1: raw currency-per-token frame, unchanged
        var currency1 = Positions.BuildPositionDefinitions(
            PriceRangeKind.CUSTOM_RANGE, range, TICK_SPACING, CURRENCY_HIGH, TOKEN_LOW);
        Assert.Equal(-1200, currency1[0].OffsetLower);
        Assert.Equal(3400, currency1[0].OffsetUpper);

        // currency sorts as currency0 (native ETH is ZERO_ADDRESS): negate and swap
        var currency0 = Positions.BuildPositionDefinitions(
            PriceRangeKind.CUSTOM_RANGE, range, TICK_SPACING, ZERO_ADDRESS, TOKEN_HIGH);
        Assert.Equal(-currency1[0].OffsetUpper, currency0[0].OffsetLower);
        Assert.Equal(-currency1[0].OffsetLower, currency0[0].OffsetUpper);
        Assert.Equal(-3400, currency0[0].OffsetLower);
        Assert.Equal(1200, currency0[0].OffsetUpper);
        Assert.Equal(Constants.MPS_TOTAL, currency0[0].Weight);
        Assert.Equal(ZERO_ADDRESS, currency0[0].OverridePositionRecipient);
    }

    [Fact]
    public void LeavesCustomRangeOffsetsUnchanged_WhenTheCurrencySortsAsCurrency1()
    {
        var defs = Positions.BuildPositionDefinitions(
            PriceRangeKind.CUSTOM_RANGE, Range(-10, 40), TICK_SPACING, CURRENCY_HIGH, TOKEN_LOW);

        Assert.Equal(-1200, defs[0].OffsetLower);
        Assert.Equal(3400, defs[0].OffsetUpper);
    }

    [Fact]
    public void IsInvariantUnderOrdering_ForATickSymmetricCustomRange()
    {
        var range = Range(-20, 25);

        var currency1 = Positions.BuildPositionDefinitions(
            PriceRangeKind.CUSTOM_RANGE, range, TICK_SPACING, CURRENCY_HIGH, TOKEN_LOW);
        var currency0 = Positions.BuildPositionDefinitions(
            PriceRangeKind.CUSTOM_RANGE, range, TICK_SPACING, ZERO_ADDRESS, TOKEN_HIGH);

        Assert.Equal(-2400, currency1[0].OffsetLower);
        Assert.Equal(2400, currency1[0].OffsetUpper);
        Assert.Equal(currency1[0].OffsetLower, currency0[0].OffsetLower);
        Assert.Equal(currency1[0].OffsetUpper, currency0[0].OffsetUpper);
    }

    [Fact]
    public void LeavesTheFullRangeSentinelUntouched_RegardlessOfCurrencyOrdering()
    {
        var asCurrency0 = Positions.BuildPositionDefinitions(
            PriceRangeKind.FULL_RANGE, Array.Empty<CustomRangeInput>(), TICK_SPACING, ZERO_ADDRESS, TOKEN_HIGH);
        var asCurrency1 = Positions.BuildPositionDefinitions(
            PriceRangeKind.FULL_RANGE, Array.Empty<CustomRangeInput>(), TICK_SPACING, CURRENCY_HIGH, TOKEN_LOW);

        foreach (var defs in new[] { asCurrency0, asCurrency1 })
        {
            var only = Assert.Single(defs);
            Assert.Equal(UniswapSharp.V3.Utils.TickMath.MIN_TICK, only.OffsetLower);
            Assert.Equal(UniswapSharp.V3.Utils.TickMath.MAX_TICK, only.OffsetUpper);
        }
    }

    [Fact]
    public void ConcentratedFullRange_AlsoResolvesToTheFullRangeSentinel()
    {
        var defs = Positions.BuildPositionDefinitions(
            PriceRangeKind.CONCENTRATED_FULL_RANGE, Array.Empty<CustomRangeInput>(),
            TICK_SPACING, ZERO_ADDRESS, TOKEN_HIGH);

        var only = Assert.Single(defs);
        Assert.Equal(UniswapSharp.V3.Utils.TickMath.MIN_TICK, only.OffsetLower);
        Assert.Equal(UniswapSharp.V3.Utils.TickMath.MAX_TICK, only.OffsetUpper);
    }

    [Fact]
    public void RejectsAnEmptyCustomRangeList()
    {
        var ex = Assert.Throws<LauncherSdkError>(() => Positions.BuildPositionDefinitions(
            PriceRangeKind.CUSTOM_RANGE, Array.Empty<CustomRangeInput>(),
            TICK_SPACING, ZERO_ADDRESS, TOKEN_HIGH));

        Assert.Equal(LauncherErrorCode.INVALID_PRICE_RANGE, ex.Code);
    }

    [Fact]
    public void RejectsARangeThatDoesNotBracketTheClearingPrice()
    {
        var ex = Assert.Throws<LauncherSdkError>(() => Positions.BuildPositionDefinitions(
            PriceRangeKind.CUSTOM_RANGE, Range(10, 40), TICK_SPACING, ZERO_ADDRESS, TOKEN_HIGH));

        Assert.Equal(LauncherErrorCode.INVALID_PRICE_RANGE, ex.Code);
    }
}
