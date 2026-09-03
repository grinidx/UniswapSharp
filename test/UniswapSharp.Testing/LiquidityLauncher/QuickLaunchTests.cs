using System.Numerics;
using UniswapSharp.LiquidityLauncher;
using UniswapSharp.LiquidityLauncher.Config;

namespace UniswapSharp.Testing.LiquidityLauncher;

// Ported from sdks/liquidity-launcher-sdk/src/quickLaunch.test.ts
public class QuickLaunchTests
{
    private const int CHAIN = (int)SupportedChainId.BASE; // 2s blocks
    private static readonly BigInteger START = 1_000_000;
    private static readonly BigInteger END = START + QuickLaunch.GetDurationBlocks(CHAIN);
    private const string ZERO = Constants.ZERO_ADDRESS;

    /// <summary>An auction built straight from the preset.</summary>
    private static QuickLaunchMatchParams PresetAuction(
        int? chainId = null,
        string? currency = null,
        BigInteger? startBlock = null,
        BigInteger? endBlock = null,
        BigInteger? totalSupplyRaw = null,
        BigInteger? reservedTokenAmountForLP = null,
        bool omitReserve = false,
        QuickLaunchLockDescriptor? @lock = null,
        bool omitLock = false,
        double? graduationFdvUsd = null) => new(
            ChainId: chainId ?? CHAIN,
            Currency: currency ?? ZERO,
            StartBlock: startBlock ?? START,
            EndBlock: endBlock ?? END,
            TotalSupplyRaw: totalSupplyRaw ?? QuickLaunch.TOTAL_SUPPLY_RAW,
            ReservedTokenAmountForLP: omitReserve ? null : reservedTokenAmountForLP ?? QuickLaunch.RESERVED_FOR_LP_RAW,
            Lock: omitLock ? null : @lock ?? new QuickLaunchLockDescriptor(QuickLaunchLockMode.BuybackBurn, true),
            GraduationFdvUsd: graduationFdvUsd);

    // ---- preset ----

    [Fact]
    public void Preset_EncodesTheCanonicalDefiningValues()
    {
        Assert.Equal("CCA", QuickLaunch.PRESET.AuctionType);
        Assert.Equal(14_400, QuickLaunch.PRESET.DurationSeconds);
        Assert.Equal(14_400, QuickLaunch.DURATION_SECONDS);
        Assert.Equal(BigInteger.Pow(10, 27), QuickLaunch.PRESET.TotalSupplyRaw);
        Assert.Equal(5 * BigInteger.Pow(10, 26), QuickLaunch.PRESET.AuctionSupplyRaw);
        Assert.Equal(5 * BigInteger.Pow(10, 26), QuickLaunch.PRESET.ReservedForLpRaw);
        Assert.Equal(ZERO, QuickLaunch.PRESET.RaiseCurrency);
        Assert.Equal(2_500, QuickLaunch.PRESET.Lp.Fee);
        Assert.Equal(25, QuickLaunch.PRESET.Lp.TickSpacing);
        Assert.Equal(PriceRangeKind.CONCENTRATED_FULL_RANGE, QuickLaunch.PRESET.Lp.Range);
        Assert.Equal(QuickLaunchLockMode.BuybackBurn, QuickLaunch.PRESET.Lp.LockMode);
        Assert.True(QuickLaunch.PRESET.Lp.PermanentTimelock);
    }

    [Fact]
    public void AllowedPoolTickSpacings_ContainsTheDerivedSpacing()
    {
        // forces an APPEND rather than a silent replacement if the fee tier ever changes
        Assert.Contains(Fees.ResolveNewPoolTickSpacing(QuickLaunch.LP_FEE), QuickLaunch.ALLOWED_POOL_TICK_SPACINGS);
        Assert.Equal(new[] { 25, 50 }, QuickLaunch.ALLOWED_POOL_TICK_SPACINGS);
    }

    [Fact]
    public void GraduationRaiseUsd_IsFdvTimesSoldShareNotTheFdvItself()
    {
        Assert.Equal(0.5, QuickLaunch.SOLD_SUPPLY_SHARE);
        Assert.Equal(5_000, QuickLaunch.GRADUATION_RAISE_USD);
    }

    // ---- isQuickLaunch: a preset-built auction matches ----

    [Fact]
    public void IsQuickLaunch_MatchesWithTheFullFingerprint() =>
        Assert.True(QuickLaunch.IsQuickLaunch(PresetAuction()));

    [Fact]
    public void IsQuickLaunch_MatchesOnTheCoreFingerprintAlone() =>
        Assert.True(QuickLaunch.IsQuickLaunch(PresetAuction(omitReserve: true, omitLock: true)));

    [Fact]
    public void IsQuickLaunch_MatchesAcrossChainsWithDifferentBlockTimes()
    {
        int chain = (int)SupportedChainId.MAINNET; // 12s blocks
        BigInteger start = 20_000;
        BigInteger end = start + QuickLaunch.GetDurationBlocks(chain);

        Assert.True(QuickLaunch.IsQuickLaunch(PresetAuction(chainId: chain, startBlock: start, endBlock: end)));
    }

    // ---- isQuickLaunch: near-misses do NOT match ----

    [Fact]
    public void IsQuickLaunch_RejectsWrongSupply() =>
        Assert.False(QuickLaunch.IsQuickLaunch(PresetAuction(totalSupplyRaw: BigInteger.Pow(10, 26))));

    [Fact]
    public void IsQuickLaunch_RejectsWrongDuration()
    {
        // 2h instead of 4h, on a 2s chain
        BigInteger end = START + 3_600;
        Assert.False(QuickLaunch.IsQuickLaunch(PresetAuction(endBlock: end)));
    }

    [Fact]
    public void IsQuickLaunch_RejectsANonNativeRaiseDenomination() =>
        Assert.False(QuickLaunch.IsQuickLaunch(
            PresetAuction(currency: "0xa0b86991c6218b36c1d19d4a2e9eb0ce3606eb48")));

    [Fact]
    public void IsQuickLaunch_RejectsAFiniteLock() =>
        Assert.False(QuickLaunch.IsQuickLaunch(
            PresetAuction(@lock: new QuickLaunchLockDescriptor(QuickLaunchLockMode.BuybackBurn, false))));

    [Fact]
    public void IsQuickLaunch_RejectsTheWrongLockMode() =>
        Assert.False(QuickLaunch.IsQuickLaunch(
            PresetAuction(@lock: new QuickLaunchLockDescriptor(QuickLaunchLockMode.Timelock, true))));

    [Fact]
    public void IsQuickLaunch_RejectsAPermanentFeesForwarderLock() =>
        Assert.False(QuickLaunch.IsQuickLaunch(
            PresetAuction(@lock: new QuickLaunchLockDescriptor(QuickLaunchLockMode.FeesForwarder, true))));

    [Fact]
    public void IsQuickLaunch_RejectsANonFiftyFiftySplit() =>
        Assert.False(QuickLaunch.IsQuickLaunch(
            PresetAuction(reservedTokenAmountForLP: BigInteger.Pow(10, 26))));

    [Fact]
    public void IsQuickLaunch_RejectsADegenerateWindow()
    {
        Assert.False(QuickLaunch.IsQuickLaunch(PresetAuction(endBlock: START)));
        Assert.False(QuickLaunch.IsQuickLaunch(PresetAuction(endBlock: START - 1)));
    }

    // ---- duration overrides ----

    [Fact]
    public void IsQuickLaunch_RejectsHistoricalOneHourAuctionsByDefault()
    {
        BigInteger end = START + 1_800; // 1h on a 2s chain
        Assert.False(QuickLaunch.IsQuickLaunch(PresetAuction(endBlock: end)));
    }

    [Fact]
    public void IsQuickLaunch_RecognizesHistoricalWindowsWhenExplicitlyOptedIn()
    {
        BigInteger end = START + 1_800; // 1h on a 2s chain
        var options = new QuickLaunchMatchOptions(
            AllowedDurationsSeconds: new[] { 1_800, 3_600, 14_400 });

        Assert.True(QuickLaunch.IsQuickLaunch(PresetAuction(endBlock: end), options));
    }

    // ---- reserve / lock resolution semantics ----

    [Fact]
    public void IsQuickLaunch_LeavesTheSplitUnassertedWhenTheReserveIsUnknown() =>
        Assert.True(QuickLaunch.IsQuickLaunch(PresetAuction(omitReserve: true)));

    [Fact]
    public void IsQuickLaunch_DoesNotTreatAZeroedStrategyReadAsAPassingReserve() =>
        // callers must map an unset strategy entry to null, never pass the raw 0
        Assert.False(QuickLaunch.IsQuickLaunch(PresetAuction(reservedTokenAmountForLP: BigInteger.Zero)));

    [Fact]
    public void IsQuickLaunch_FailsWhenTheAuctionIsKnownToHaveNoLock() =>
        Assert.False(QuickLaunch.IsQuickLaunch(PresetAuction(@lock: QuickLaunch.NoLockResolved)));

    [Fact]
    public void IsQuickLaunch_LeavesTheLockUnassertedWhenItIsNotResolvedYet() =>
        Assert.True(QuickLaunch.IsQuickLaunch(PresetAuction(omitLock: true)));

    [Fact]
    public void IsQuickLaunch_StillFailsTheBaseFingerprintEvenWhenBothRefinementsPass() =>
        Assert.False(QuickLaunch.IsQuickLaunch(PresetAuction(totalSupplyRaw: BigInteger.Pow(10, 26))));

    // ---- structurally permanent lock modes ----

    [Fact]
    public void StructurallyPermanentLockModes_AreBurnAndCreatorFeesOnly()
    {
        Assert.Equal(new[] { QuickLaunchLockMode.Burn, QuickLaunchLockMode.CreatorFees },
            QuickLaunch.STRUCTURALLY_PERMANENT_LOCK_MODES);

        Assert.True(QuickLaunch.IsStructurallyPermanentLockMode(QuickLaunchLockMode.Burn));
        Assert.True(QuickLaunch.IsStructurallyPermanentLockMode(QuickLaunchLockMode.CreatorFees));
        Assert.False(QuickLaunch.IsStructurallyPermanentLockMode(QuickLaunchLockMode.BuybackBurn));
        Assert.False(QuickLaunch.IsStructurallyPermanentLockMode(QuickLaunchLockMode.Timelock));
        Assert.False(QuickLaunch.IsStructurallyPermanentLockMode(QuickLaunchLockMode.FeesForwarder));
    }

    [Fact]
    public void IsQuickLaunch_MatchesABurnLockEvenWithPermanentTimelockFalse() =>
        // burn rows carry unlock_block = 0, so a horizon derivation reports finite
        Assert.True(QuickLaunch.IsQuickLaunch(
            PresetAuction(@lock: new QuickLaunchLockDescriptor(QuickLaunchLockMode.Burn, false))));

    [Fact]
    public void IsQuickLaunch_MatchesACreatorFeesPositionEvenWithPermanentTimelockFalse() =>
        Assert.True(QuickLaunch.IsQuickLaunch(
            PresetAuction(@lock: new QuickLaunchLockDescriptor(QuickLaunchLockMode.CreatorFees, false))));

    // ---- isPermanentTimelock ----

    [Fact]
    public void PermanentTimelock_EncodesAThousandYearHorizon() =>
        Assert.Equal(1000L * 365 * 86_400, QuickLaunch.PERMANENT_TIMELOCK_MIN_HORIZON_SECONDS);

    [Fact]
    public void PermanentTimelock_RequestIsExactlyOneHundredTimesTheThreshold() =>
        Assert.Equal(new BigInteger(QuickLaunch.PERMANENT_TIMELOCK_MIN_HORIZON_SECONDS) * 100,
            QuickLaunch.PERMANENT_TIMELOCK_REQUEST_SECONDS);

    [Fact]
    public void PermanentTimelock_BlockForm_AcceptsAtOrPastTheHorizonAndRejectsShortOfIt()
    {
        // BASE: 2s blocks, so the 1000-year horizon is 1000*365*86400/2 blocks
        BigInteger endBlock = 1_000_000;
        BigInteger blocksForHorizon = QuickLaunch.PERMANENT_TIMELOCK_MIN_HORIZON_SECONDS / 2;

        Assert.True(QuickLaunch.IsPermanentTimelock(new PermanentTimelockParams(
            ChainId: CHAIN, EndBlock: endBlock, UnlockBlock: endBlock + blocksForHorizon)));

        Assert.False(QuickLaunch.IsPermanentTimelock(new PermanentTimelockParams(
            ChainId: CHAIN, EndBlock: endBlock, UnlockBlock: endBlock + blocksForHorizon - 1)));
    }

    [Fact]
    public void PermanentTimelock_BlockForm_RejectsALockThatUnlocksRightAfterTheAuction() =>
        Assert.False(QuickLaunch.IsPermanentTimelock(new PermanentTimelockParams(
            ChainId: CHAIN, EndBlock: 1_000_000, UnlockBlock: 1_000_100)));

    [Fact]
    public void PermanentTimelock_BlockForm_AcceptsALegacyMaxUint256Sentinel() =>
        Assert.True(QuickLaunch.IsPermanentTimelock(new PermanentTimelockParams(
            ChainId: CHAIN, EndBlock: 1_000_000, UnlockBlock: BigInteger.Pow(2, 256) - 1)));

    [Fact]
    public void PermanentTimelock_TimestampForm_AcceptsAtTheThresholdAndRejectsOneSecondShort()
    {
        BigInteger end = 1_700_000_000;

        Assert.True(QuickLaunch.IsPermanentTimelock(new PermanentTimelockParams(
            EndTimeSeconds: end, UnlockTimeSeconds: end + QuickLaunch.PERMANENT_TIMELOCK_MIN_HORIZON_SECONDS)));

        Assert.False(QuickLaunch.IsPermanentTimelock(new PermanentTimelockParams(
            EndTimeSeconds: end, UnlockTimeSeconds: end + QuickLaunch.PERMANENT_TIMELOCK_MIN_HORIZON_SECONDS - 1)));
    }

    [Fact]
    public void PermanentTimelock_TimestampForm_AcceptsTheHorizonTheCreateFlowRequests()
    {
        BigInteger end = 1_700_000_000;

        Assert.True(QuickLaunch.IsPermanentTimelock(new PermanentTimelockParams(
            EndTimeSeconds: end, UnlockTimeSeconds: end + QuickLaunch.PERMANENT_TIMELOCK_REQUEST_SECONDS)));
    }

    [Fact]
    public void PermanentTimelock_SentinelForm_AcceptsAtTheThresholdAndRejectsBelow()
    {
        Assert.True(QuickLaunch.IsPermanentTimelock(
            new PermanentTimelockParams(UnlockBlock: QuickLaunch.PERMANENT_UNLOCK_BLOCK_THRESHOLD)));

        Assert.False(QuickLaunch.IsPermanentTimelock(
            new PermanentTimelockParams(UnlockBlock: QuickLaunch.PERMANENT_UNLOCK_BLOCK_THRESHOLD - 1)));

        Assert.True(QuickLaunch.IsPermanentTimelock(
            new PermanentTimelockParams(UnlockBlock: BigInteger.Pow(2, 256) - 1)));

        Assert.False(QuickLaunch.IsPermanentTimelock(new PermanentTimelockParams(UnlockBlock: 1_000_100)));
    }

    [Fact]
    public void PermanentTimelock_StructuralModes_ShortCircuitAtUnlockBlockZero()
    {
        foreach (var mode in new[] { QuickLaunchLockMode.Burn, QuickLaunchLockMode.CreatorFees })
        {
            // block form
            Assert.True(QuickLaunch.IsPermanentTimelock(new PermanentTimelockParams(
                LockMode: mode, ChainId: CHAIN, EndBlock: 1_000_000, UnlockBlock: BigInteger.Zero)));
            // sentinel form
            Assert.True(QuickLaunch.IsPermanentTimelock(
                new PermanentTimelockParams(LockMode: mode, UnlockBlock: BigInteger.Zero)));
            // timestamp form
            Assert.True(QuickLaunch.IsPermanentTimelock(new PermanentTimelockParams(
                LockMode: mode, EndTimeSeconds: 1_700_000_000, UnlockTimeSeconds: 1_700_000_001)));
        }

        // other modes are not structurally permanent and fall through to the horizon math
        Assert.False(QuickLaunch.IsPermanentTimelock(new PermanentTimelockParams(
            LockMode: QuickLaunchLockMode.BuybackBurn, ChainId: CHAIN,
            EndBlock: 1_000_000, UnlockBlock: BigInteger.Zero)));
    }

    // ---- graduation FDV gate ----

    [Fact]
    public void IsQuickLaunch_AcceptsBothAllowedGraduationFdvValues()
    {
        Assert.True(QuickLaunch.IsQuickLaunch(PresetAuction(graduationFdvUsd: 10_000)));
        Assert.True(QuickLaunch.IsQuickLaunch(PresetAuction(graduationFdvUsd: 5_000)));
        // within the +/-10% bands: [4500, 5500] and [9000, 11000]
        Assert.True(QuickLaunch.IsQuickLaunch(PresetAuction(graduationFdvUsd: 4_870)));
        Assert.True(QuickLaunch.IsQuickLaunch(PresetAuction(graduationFdvUsd: 11_000)));
    }

    [Fact]
    public void IsQuickLaunch_RejectsAnOutOfBandGraduationFdv()
    {
        Assert.False(QuickLaunch.IsQuickLaunch(PresetAuction(graduationFdvUsd: 12_500)));
        Assert.False(QuickLaunch.IsQuickLaunch(PresetAuction(graduationFdvUsd: 7_000)));
    }

    [Fact]
    public void IsQuickLaunch_TreatsZeroAsARealValueAndRejectsIt() =>
        // 0 is finite, so it is asserted rather than folded into "unresolved"
        Assert.False(QuickLaunch.IsQuickLaunch(PresetAuction(graduationFdvUsd: 0)));

    [Fact]
    public void IsQuickLaunch_LeavesGraduationUnassertedWhenUnresolved()
    {
        Assert.True(QuickLaunch.IsQuickLaunch(PresetAuction()));                              // null
        Assert.True(QuickLaunch.IsQuickLaunch(PresetAuction(graduationFdvUsd: double.NaN)));  // failed Number()
        Assert.True(QuickLaunch.IsQuickLaunch(PresetAuction(graduationFdvUsd: double.PositiveInfinity)));
    }

    [Fact]
    public void IsQuickLaunch_GraduationGateIsLayeredOnTopOfStructuralChecks() =>
        // a resolved, allowed FDV cannot rescue a broken fingerprint
        Assert.False(QuickLaunch.IsQuickLaunch(
            PresetAuction(totalSupplyRaw: BigInteger.Pow(10, 26), graduationFdvUsd: 10_000)));

    // ---- price derivation ----

    [Fact]
    public void GetFloorAndGraduationPricePerToken_DeriveFromFdvOverTheFullSupply()
    {
        // $1000 FDV / 1e9 tokens / $2000 per ETH = 5e-10
        Assert.Equal("0.0000000005", QuickLaunch.GetFloorPricePerToken(2_000));
        // $10000 FDV / 1e9 tokens / $2000 per ETH = 5e-9
        Assert.Equal("0.000000005", QuickLaunch.GetGraduationPricePerToken(2_000));
    }

    [Fact]
    public void GetFloorPricePerToken_RejectsANonPositiveNativePrice()
    {
        Assert.Throws<LauncherSdkError>(() => QuickLaunch.GetFloorPricePerToken(0));
        Assert.Throws<LauncherSdkError>(() => QuickLaunch.GetFloorPricePerToken(-1));
        Assert.Throws<LauncherSdkError>(() => QuickLaunch.GetFloorPricePerToken(double.NaN));
    }

    [Fact]
    public void GetDurationBlocks_ConvertsTheFourHourWindowUsingTheChainBlockTime()
    {
        Assert.Equal(new BigInteger(7_200), QuickLaunch.GetDurationBlocks((int)SupportedChainId.BASE));    // 2s
        Assert.Equal(new BigInteger(1_200), QuickLaunch.GetDurationBlocks((int)SupportedChainId.MAINNET)); // 12s
    }
}
