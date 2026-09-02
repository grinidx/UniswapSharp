using UniswapSharp.Core.Utils;
using UniswapSharp.LiquidityLauncher;

namespace UniswapSharp.Testing.LiquidityLauncher;

// Ported from the Instant Launch sections of
// sdks/liquidity-launcher-sdk/src/addresses.test.ts
public class InstantLaunchRegistryTests
{
    private static string GetAddress(string a) => AddressValidator.GetAddress(a);

    private const int ROBINHOOD = (int)SupportedChainId.ROBINHOOD;
    private const int ARC = (int)SupportedChainId.ARC;
    private const int MAINNET = (int)SupportedChainId.MAINNET;

    // Current (2026-08-05 full redeploy) Robinhood splitters, pinned as registry literals.
    private static readonly string FEES_ON_SPLITTER = GetAddress("0xeff166aaf189323c58dc27ed1206eb2c37faacdf");
    private static readonly string FEES_OFF_SPLITTER = GetAddress("0x222d6d4f1ce59b0d48d5505114ec8addc90a4359");
    // The superseded c3f9506 fees-off splitter, still classifiable (append-only registry).
    private static readonly string FEES_OFF_SPLITTER_C3F9506 = GetAddress("0xdf50f4ea2207f9d2a753a3dae729b36fdef13b23");

    // ---- deployment registry ----

    [Fact]
    public void GetInstantLaunchDeployments_CarriesAllFiveRobinhoodGenerationsAsFeeOnOffPairs()
    {
        var deployments = Addresses.GetInstantLaunchDeployments(ROBINHOOD);
        Assert.Equal(10, deployments.Count);

        Assert.Equal(5, deployments.Count(d => d.CreatorFeesEnabled));
        Assert.Equal(5, deployments.Count(d => !d.CreatorFeesEnabled));

        foreach (var on in deployments.Where(d => d.CreatorFeesEnabled))
        {
            Assert.Equal(4000, on.CreatorFeeNativeBps);
            Assert.Equal(0, on.CreatorFeeTokenBps);
        }
        foreach (var off in deployments.Where(d => !d.CreatorFeesEnabled))
        {
            Assert.Equal(0, off.CreatorFeeNativeBps);
            Assert.Equal(0, off.CreatorFeeTokenBps);
        }
    }

    [Fact]
    public void GetInstantLaunchDeployments_IsEmptyForChainsWithoutADeployment() =>
        Assert.Empty(Addresses.GetInstantLaunchDeployments(MAINNET));

    [Fact]
    public void GetInstantLaunchStrategy_ReturnsTheLastMatchingEntryPerVariant()
    {
        // the registry is append-only, so the newest deployment of a variant wins
        Assert.Equal(FEES_ON_SPLITTER, Addresses.GetInstantLaunchStrategy(ROBINHOOD, true)!.FeeSplitter);
        Assert.Equal(FEES_OFF_SPLITTER, Addresses.GetInstantLaunchStrategy(ROBINHOOD, false)!.FeeSplitter);
        Assert.Null(Addresses.GetInstantLaunchStrategy(MAINNET, true));
    }

    [Fact]
    public void CurrentGenerations_CarryThe2026_08_05PoolShape()
    {
        foreach (bool feesOn in new[] { true, false })
        {
            var robinhood = Addresses.GetInstantLaunchStrategy(ROBINHOOD, feesOn)!;
            Assert.Equal(25, robinhood.TickSpacing);
            Assert.Equal(198_050, robinhood.InitialTick);
            Assert.Equal(-160_100, robinhood.MinLaunchTick);

            // Arc's strategy redeploy shares the shape but opens at a different tick
            var arc = Addresses.GetInstantLaunchStrategy(ARC, feesOn)!;
            Assert.Equal(25, arc.TickSpacing);
            Assert.Equal(122_050, arc.InitialTick);
            Assert.Equal(-160_100, arc.MinLaunchTick);
        }
    }

    [Fact]
    public void EarlierGenerations_KeepTheirOriginalPoolShape()
    {
        var earliest = Addresses.GetInstantLaunchDeployments(ROBINHOOD)[0];
        Assert.Equal(60, earliest.TickSpacing);
        Assert.Equal(198_060, earliest.InitialTick);
        Assert.Equal(-208_980, earliest.MinLaunchTick);
    }

    [Fact]
    public void GetInstantLaunchDeployment_ResolvesAStoredStrategyCaseInsensitively()
    {
        var current = Addresses.GetInstantLaunchStrategy(ROBINHOOD, true)!;

        Assert.Equal(current, Addresses.GetInstantLaunchDeployment(current.Strategy));
        Assert.Equal(current, Addresses.GetInstantLaunchDeployment(current.Strategy.ToLowerInvariant()));
        Assert.Null(Addresses.GetInstantLaunchDeployment("0x0000000000000000000000000000000000000000"));
    }

    [Fact]
    public void GetInstantLaunchContracts_ScopesTheRedeployedLauncherToRobinhoodAndArc()
    {
        string redeployed = GetAddress("0x0000ffffbe8efe702c8703ae3477ff5de3d319c0");

        foreach (int chainId in new[] { ROBINHOOD, ARC })
        {
            Assert.Equal(redeployed, Addresses.GetLauncherAddresses(chainId)?.LiquidityLauncher);
            Assert.Equal(redeployed, Addresses.GetInstantLaunchContracts(chainId)?.LiquidityLauncher);
        }

        Assert.Null(Addresses.GetInstantLaunchContracts(MAINNET));
    }

    // ---- creator-fees position recipient ----

    [Fact]
    public void GetCreatorFeesPositionRecipient_ResolvesToTheFeesOnSplitter()
    {
        Assert.Equal(FEES_ON_SPLITTER, Addresses.GetCreatorFeesPositionRecipient(ROBINHOOD));
        Assert.NotEqual(FEES_OFF_SPLITTER, Addresses.GetCreatorFeesPositionRecipient(ROBINHOOD));
        Assert.Null(Addresses.GetCreatorFeesPositionRecipient(MAINNET));
    }

    [Fact]
    public void IsCreatorFeesPositionRecipient_RecognizesTheFeesOnSplitterCaseInsensitively()
    {
        Assert.True(Addresses.IsCreatorFeesPositionRecipient(ROBINHOOD, FEES_ON_SPLITTER));
        Assert.True(Addresses.IsCreatorFeesPositionRecipient(ROBINHOOD, FEES_ON_SPLITTER.ToLowerInvariant()));

        // the fees-off splitter is deliberately NOT a creator-fees recipient
        Assert.False(Addresses.IsCreatorFeesPositionRecipient(ROBINHOOD, FEES_OFF_SPLITTER));
        Assert.False(Addresses.IsCreatorFeesPositionRecipient(MAINNET, FEES_ON_SPLITTER));
    }

    // ---- autocompound position recipient ----

    [Fact]
    public void GetAutocompoundPositionRecipient_ResolvesToTheFeesOffSplitter()
    {
        Assert.Equal(FEES_OFF_SPLITTER, Addresses.GetAutocompoundPositionRecipient(ROBINHOOD));
        Assert.NotEqual(FEES_ON_SPLITTER, Addresses.GetAutocompoundPositionRecipient(ROBINHOOD));
        Assert.Null(Addresses.GetAutocompoundPositionRecipient(MAINNET));
    }

    [Fact]
    public void GetAutocompoundPositionRecipient_AgreesWithTheCurrentFeesOffRegistryEntry() =>
        Assert.Equal(Addresses.GetInstantLaunchStrategy(ROBINHOOD, false)!.FeeSplitter,
            Addresses.GetAutocompoundPositionRecipient(ROBINHOOD));

    [Fact]
    public void IsAutocompoundPositionRecipient_StillClassifiesSupersededSplitters()
    {
        // append-only classifier: indexed launches permanently reference the splitter they migrated to
        Assert.True(Addresses.IsAutocompoundPositionRecipient(ROBINHOOD, FEES_OFF_SPLITTER_C3F9506));
        Assert.NotEqual(FEES_OFF_SPLITTER_C3F9506, Addresses.GetAutocompoundPositionRecipient(ROBINHOOD));
    }

    [Fact]
    public void PositionRecipientClassifiers_StayDisjointOnBothSplitters()
    {
        Assert.False(Addresses.IsCreatorFeesPositionRecipient(ROBINHOOD, FEES_OFF_SPLITTER));
        Assert.False(Addresses.IsAutocompoundPositionRecipient(ROBINHOOD, FEES_ON_SPLITTER));
    }

    [Fact]
    public void PositionRecipientClassifiers_RejectUnknownRecipientsAndWrongChains()
    {
        Assert.False(Addresses.IsAutocompoundPositionRecipient(
            ROBINHOOD, "0x0000000000000000000000000000000000000000"));
        Assert.False(Addresses.IsAutocompoundPositionRecipient(MAINNET, FEES_OFF_SPLITTER));
    }
}
