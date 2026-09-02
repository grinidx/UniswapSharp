using UniswapSharp.LiquidityLauncher;
using UniswapSharp.LiquidityLauncher.Config;

namespace UniswapSharp.Testing.LiquidityLauncher.Config;

// Ported from sdks/liquidity-launcher-sdk/src/config/fees.test.ts (upstream #699).
public class FeesTests
{
    [Fact]
    public void ResolveNewPoolTickSpacing_ResolvesThe025PercentTierTo25() =>
        Assert.Equal(25, Fees.ResolveNewPoolTickSpacing(2_500));

    [Theory]
    [InlineData(100, 1)]
    [InlineData(500, 5)]
    [InlineData(1_234, 12)]
    [InlineData(2_500, 25)]
    [InlineData(3_000, 30)]
    [InlineData(10_000, 100)]
    [InlineData(50_000, 500)]
    public void ResolveNewPoolTickSpacing_DerivesOneTickOfSpacingPerBipOfFee(int fee, int expected) =>
        Assert.Equal(expected, Fees.ResolveNewPoolTickSpacing(fee));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(49)]
    public void ResolveNewPoolTickSpacing_FloorsTheSpacingAt1ForTinyFees(int fee) =>
        Assert.Equal(1, Fees.ResolveNewPoolTickSpacing(fee));

    [Fact]
    public void ResolveNewPoolTickSpacing_RejectsAFeeWhoseSpacingExceedsTheV4Maximum()
    {
        // exactly at the maximum is fine
        Assert.Equal(Constants.MAX_TICK_SPACING, Fees.ResolveNewPoolTickSpacing(Constants.MAX_TICK_SPACING * 100));

        var ex = Assert.Throws<LauncherSdkError>(
            () => Fees.ResolveNewPoolTickSpacing((Constants.MAX_TICK_SPACING + 1) * 100));
        Assert.Equal(LauncherErrorCode.INVALID_FEE, ex.Code);
    }

    [Fact]
    public void FeeToTickSpacing_IsTheDeprecatedAliasOfResolveNewPoolTickSpacing()
    {
        foreach (int fee in new[] { 0, 100, 500, 2_500, 3_000, 10_000 })
        {
#pragma warning disable CS0618 // exercising the deprecated alias is the point of this test
            Assert.Equal(Fees.ResolveNewPoolTickSpacing(fee), Fees.FeeToTickSpacing(fee));
#pragma warning restore CS0618
        }
    }
}
