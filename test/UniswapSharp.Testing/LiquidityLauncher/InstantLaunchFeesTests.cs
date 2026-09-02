using System.Numerics;
using UniswapSharp.LiquidityLauncher;

namespace UniswapSharp.Testing.LiquidityLauncher;

// Ported from sdks/liquidity-launcher-sdk/src/instantLaunchFees.test.ts
public class InstantLaunchFeesTests
{
    private static CreatorFeeSplitBps Split(bool feesOn)
    {
        var deployment = Addresses.GetInstantLaunchStrategy((int)SupportedChainId.ROBINHOOD, feesOn)!;
        return new CreatorFeeSplitBps(deployment.CreatorFeeNativeBps, deployment.CreatorFeeTokenBps);
    }

    private static readonly CreatorFeeSplitBps FEES_ON = Split(true);
    private static readonly CreatorFeeSplitBps FEES_OFF = Split(false);

    // ---- creatorFeesAccumulated ----

    [Fact]
    public void CreatorFeesAccumulated_SumsTheVaultLegWithPerEventFlooring()
    {
        var events = new[]
        {
            new FeesCollectedAmounts(1_000_000, 500),
            new FeesCollectedAmounts(3, 999_999),
        };

        // floor(1_000_000 * 4000 / 10000) = 400_000; floor(3 * 4000 / 10000) = floor(1.2) = 1.
        var accumulated = InstantLaunchFees.CreatorFeesAccumulated(events, FEES_ON);

        Assert.Equal(new BigInteger(400_001), accumulated.Native);
        Assert.Equal(BigInteger.Zero, accumulated.Token);
    }

    [Fact]
    public void CreatorFeesAccumulated_FloorsPerEventNotOnTheTotal()
    {
        // Two 4-wei events at 40%: per-event floor(1.6) = 1 each → 2; a total-based floor(3.2) would be 3.
        var events = new[] { new FeesCollectedAmounts(4, 0), new FeesCollectedAmounts(4, 0) };

        Assert.Equal(new BigInteger(2), InstantLaunchFees.CreatorFeesAccumulated(events, FEES_ON).Native);
    }

    [Fact]
    public void CreatorFeesAccumulated_IsAlwaysZeroThroughTheFeesOffSplitter()
    {
        var events = new[] { new FeesCollectedAmounts(1_000_000, 1_000_000) };
        var accumulated = InstantLaunchFees.CreatorFeesAccumulated(events, FEES_OFF);

        Assert.Equal(BigInteger.Zero, accumulated.Native);
        Assert.Equal(BigInteger.Zero, accumulated.Token);
    }

    [Fact]
    public void CreatorFeesAccumulated_ReturnsZeroForNoEvents()
    {
        var accumulated = InstantLaunchFees.CreatorFeesAccumulated(Array.Empty<FeesCollectedAmounts>(), FEES_ON);

        Assert.Equal(BigInteger.Zero, accumulated.Native);
        Assert.Equal(BigInteger.Zero, accumulated.Token);
    }

    [Fact]
    public void CreatorFeesAccumulated_RejectsInvalidBpsAndNegativeAmounts()
    {
        Assert.Throws<LauncherSdkError>(() => InstantLaunchFees.CreatorFeesAccumulated(
            Array.Empty<FeesCollectedAmounts>(), new CreatorFeeSplitBps(10_001, 0)));

        Assert.Throws<LauncherSdkError>(() => InstantLaunchFees.CreatorFeesAccumulated(
            Array.Empty<FeesCollectedAmounts>(), new CreatorFeeSplitBps(-1, 0)));

        var ex = Assert.Throws<LauncherSdkError>(() => InstantLaunchFees.CreatorFeesAccumulated(
            new[] { new FeesCollectedAmounts(-1, 0) }, FEES_ON));
        Assert.Contains("negative", ex.Message);
    }

    // ---- creatorFeesClaimable ----

    [Fact]
    public void CreatorFeesClaimable_IsAccumulatedMinusClaimed()
    {
        Assert.Equal(new BigInteger(700), InstantLaunchFees.CreatorFeesClaimable(1_000, 300));
        Assert.Equal(new BigInteger(1_000), InstantLaunchFees.CreatorFeesClaimable(1_000, 0));
        Assert.Equal(BigInteger.Zero, InstantLaunchFees.CreatorFeesClaimable(1_000, 1_000));
    }

    [Fact]
    public void CreatorFeesClaimable_ClampsAtZeroWhenPayoutsExceedAccumulation()
    {
        // balance-backed donation attribution can push on-chain payouts above the event-derived total
        Assert.Equal(BigInteger.Zero, InstantLaunchFees.CreatorFeesClaimable(100, 150));
    }

    [Fact]
    public void CreatorFeesClaimable_RejectsNegativeInputs()
    {
        Assert.Contains("negative", Assert.Throws<LauncherSdkError>(
            () => InstantLaunchFees.CreatorFeesClaimable(-1, 0)).Message);
        Assert.Contains("negative", Assert.Throws<LauncherSdkError>(
            () => InstantLaunchFees.CreatorFeesClaimable(0, -1)).Message);
    }

    // ---- feesCompounded ----

    [Fact]
    public void FeesCompounded_SumsTheCompoundingRecipientClaimedAmountsPerSide()
    {
        var claims = new[] { new ClaimedAmounts(100, 5_000), new ClaimedAmounts(23, 77) };
        var compounded = InstantLaunchFees.FeesCompounded(claims);

        Assert.Equal(new BigInteger(123), compounded.Native);
        Assert.Equal(new BigInteger(5_077), compounded.Token);
    }

    [Fact]
    public void FeesCompounded_ReturnsZeroForNoClaims()
    {
        var compounded = InstantLaunchFees.FeesCompounded(Array.Empty<ClaimedAmounts>());

        Assert.Equal(BigInteger.Zero, compounded.Native);
        Assert.Equal(BigInteger.Zero, compounded.Token);
    }

    [Fact]
    public void FeesCompounded_RejectsNegativeAmounts()
    {
        Assert.Contains("negative", Assert.Throws<LauncherSdkError>(
            () => InstantLaunchFees.FeesCompounded(new[] { new ClaimedAmounts(-1, 0) })).Message);
    }
}
