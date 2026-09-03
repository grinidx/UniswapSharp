using System.Numerics;
using UniswapSharp.Core.Utils;
using UniswapSharp.LiquidityLauncher;

namespace UniswapSharp.Testing.LiquidityLauncher;

// Ported from sdks/liquidity-launcher-sdk/src/instantLaunch.test.ts
public class InstantLaunchTests
{
    private static string GetAddress(string a) => AddressValidator.GetAddress(a);

    private const int ROBINHOOD = (int)SupportedChainId.ROBINHOOD;
    private const int ARC = (int)SupportedChainId.ARC;
    private const int MAINNET = (int)SupportedChainId.MAINNET;

    private static readonly string SALT = "0x" + string.Concat(Enumerable.Repeat("11", 32));

    // Golden vectors: real launched tokens and their on-chain pool ids.
    private static readonly string LAUNCHED_TOKEN = GetAddress("0xFb12A16F5842bA4886130cAA6664aB5db2D2F2fb");
    private const string LAUNCHED_TOKEN_POOL_ID = "0xacab50a30661df2dd6bff53c7ba773a20a0efe0eea8b4216efd08caf557c73a3";
    private static readonly string REDEPLOY_LAUNCHED_TOKEN = GetAddress("0x91F1c022645602ca83Fd1adcBa5a5019F54D5f1f");
    private const string REDEPLOY_LAUNCHED_TOKEN_POOL_ID = "0x2d5e17e0b164b9b3401c124a3aa58da2ba71695e4e6e85b16e280497835e2bea";

    private static readonly Uerc20Metadata METADATA = new("desc", "https://example.com", "ipfs://img", "0x");

    // ---- getInstantLaunchAddresses ----

    [Fact]
    public void GetInstantLaunchAddresses_ResolvesTheFeesOnRobinhoodStack()
    {
        var stack = InstantLaunch.GetInstantLaunchAddresses(ROBINHOOD, true)!;

        Assert.True(stack.CreatorFeesEnabled);
        Assert.Equal(Addresses.GetInstantLaunchStrategy(ROBINHOOD, true)!.Strategy, stack.Strategy);
        Assert.Equal(Addresses.GetInstantLaunchStrategy(ROBINHOOD, true)!.FeeSplitter, stack.FeeSplitter);
        Assert.Equal(Addresses.GetInstantLaunchContracts(ROBINHOOD)!.BeneficiaryVault, stack.BeneficiaryVault);
        Assert.Equal(Addresses.GetInstantLaunchContracts(ROBINHOOD)!.CompoundingClaimRecipient,
            stack.CompoundingClaimRecipient);
    }

    [Fact]
    public void GetInstantLaunchAddresses_ResolvesTheFeesOffStackToItsOwnStrategyAndSplitterSameSingletons()
    {
        var on = InstantLaunch.GetInstantLaunchAddresses(ROBINHOOD, true)!;
        var off = InstantLaunch.GetInstantLaunchAddresses(ROBINHOOD, false)!;

        Assert.NotEqual(on.Strategy, off.Strategy);
        Assert.NotEqual(on.FeeSplitter, off.FeeSplitter);
        Assert.False(off.CreatorFeesEnabled);

        // the chain singletons are shared by both variants
        Assert.Equal(on.BeneficiaryVault, off.BeneficiaryVault);
        Assert.Equal(on.CompoundingClaimRecipient, off.CompoundingClaimRecipient);
    }

    [Fact]
    public void GetInstantLaunchAddresses_ResolvesLauncherSideContractsFromTheSingleLauncherRegistry()
    {
        var stack = InstantLaunch.GetInstantLaunchAddresses(ROBINHOOD, true)!;
        var launcher = Addresses.GetLauncherAddresses(ROBINHOOD)!;

        Assert.Equal(launcher.LiquidityLauncher, stack.LiquidityLauncher);
        Assert.Equal(launcher.Uerc20Factory, stack.Uerc20Factory);
    }

    [Fact]
    public void GetInstantLaunchAddresses_IsNullWhereTheStackIsNotDeployed() =>
        Assert.Null(InstantLaunch.GetInstantLaunchAddresses(MAINNET, true));

    [Fact]
    public void GetInstantLaunchAddresses_ResolvesOnArc()
    {
        var stack = InstantLaunch.GetInstantLaunchAddresses(ARC, true)!;
        Assert.Equal(Addresses.GetInstantLaunchStrategy(ARC, true)!.Strategy, stack.Strategy);
    }

    [Fact]
    public void IsInstantLaunchSupportedChain_IsTrueOnlyWhereAVariantResolves()
    {
        Assert.True(InstantLaunch.IsInstantLaunchSupportedChain(ROBINHOOD));
        Assert.True(InstantLaunch.IsInstantLaunchSupportedChain(ARC));
        Assert.False(InstantLaunch.IsInstantLaunchSupportedChain(MAINNET));
    }

    // ---- buildInstantLaunchTransaction ----

    private static BuildInstantLaunchParams BuildParams(
        int chainId = ROBINHOOD, bool creatorFeesEnabled = true, string? feeBeneficiary = null) => new(
            ChainId: chainId,
            Name: "Test Token",
            Symbol: "TEST",
            PredictedTokenAddress: LAUNCHED_TOKEN,
            Metadata: METADATA,
            Salt: SALT,
            CreatorFeesEnabled: creatorFeesEnabled,
            FeeBeneficiary: feeBeneficiary ?? (creatorFeesEnabled
                ? GetAddress("0x51b0bad1e2977ad4a256d4863f569923d3a10b1d")
                : null));

    [Fact]
    public void BuildInstantLaunchTransaction_BuildsOneZeroValueLauncherMulticall()
    {
        var stack = InstantLaunch.GetInstantLaunchAddresses(ROBINHOOD, true)!;
        var transaction = InstantLaunch.BuildInstantLaunchTransaction(BuildParams());

        Assert.Equal(stack.LiquidityLauncher, transaction.To);
        Assert.Equal(BigInteger.Zero, transaction.Value);
        Assert.Equal(ROBINHOOD, transaction.ChainId);
        Assert.StartsWith("0x", transaction.Data);

        // the strategy and the encoded beneficiary both appear in the distributeToken subcall
        Assert.Contains(stack.Strategy[2..].ToLowerInvariant(), transaction.Data.ToLowerInvariant());
    }

    [Fact]
    public void BuildInstantLaunchTransaction_SelectsTheFeesOffStrategyAndEncodesThePlaceholderBeneficiary()
    {
        var off = InstantLaunch.GetInstantLaunchAddresses(ROBINHOOD, false)!;
        var transaction = InstantLaunch.BuildInstantLaunchTransaction(
            BuildParams(creatorFeesEnabled: false));

        Assert.Contains(off.Strategy[2..].ToLowerInvariant(), transaction.Data.ToLowerInvariant());
        Assert.Contains(InstantLaunch.DISABLED_CREATOR_FEE_BENEFICIARY[2..].ToLowerInvariant(),
            transaction.Data.ToLowerInvariant());
    }

    [Fact]
    public void BuildInstantLaunchTransaction_RejectsAZeroLauncherOrVaultFeeBeneficiary()
    {
        var stack = InstantLaunch.GetInstantLaunchAddresses(ROBINHOOD, true)!;

        foreach (string bad in new[] { Constants.ZERO_ADDRESS, stack.LiquidityLauncher, stack.BeneficiaryVault })
        {
            var ex = Assert.Throws<LauncherSdkError>(() =>
                InstantLaunch.BuildInstantLaunchTransaction(BuildParams(feeBeneficiary: bad)));
            Assert.Equal(LauncherErrorCode.INVALID_INPUT, ex.Code);
        }
    }

    [Fact]
    public void BuildInstantLaunchTransaction_RejectsAFeeBeneficiaryPassedWithCreatorFeesOff()
    {
        var ex = Assert.Throws<LauncherSdkError>(() => InstantLaunch.BuildInstantLaunchTransaction(
            BuildParams(creatorFeesEnabled: false, feeBeneficiary: LAUNCHED_TOKEN)));

        Assert.Equal(LauncherErrorCode.INVALID_INPUT, ex.Code);
    }

    [Fact]
    public void BuildInstantLaunchTransaction_ThrowsUnsupportedChainWhereNotDeployed()
    {
        var ex = Assert.Throws<LauncherSdkError>(() =>
            InstantLaunch.BuildInstantLaunchTransaction(BuildParams(chainId: MAINNET)));

        Assert.Equal(LauncherErrorCode.UNSUPPORTED_CHAIN, ex.Code);
    }

    // ---- pool shape ----

    [Fact]
    public void PoolShape_StatesThePostRedeployValues()
    {
        Assert.Equal(2500, InstantLaunch.POOL_LP_FEE);
        Assert.Equal(25, InstantLaunch.POOL_TICK_SPACING);
        Assert.Equal(-160_100, InstantLaunch.MIN_LAUNCH_TICK);
        Assert.Equal(198_050, InstantLaunch.INITIAL_TICK);
        Assert.Equal(Constants.ZERO_ADDRESS, InstantLaunch.POOL_HOOKS);
        Assert.Equal(Constants.ZERO_ADDRESS, InstantLaunch.POOL_CURRENCY0);
    }

    [Fact]
    public void AllowedPoolTickSpacings_GrandfathersEverySpacingPoolsWereEverMintedAt()
    {
        Assert.Equal(new[] { 25, 60 }, InstantLaunch.ALLOWED_POOL_TICK_SPACINGS);
        // must contain the spacing new pools are opened at, forcing an append rather than a replace
        Assert.Contains(InstantLaunch.POOL_TICK_SPACING, InstantLaunch.ALLOWED_POOL_TICK_SPACINGS);
    }

    [Fact]
    public void TotalSupply_IsExactlyOneBillionAtEighteenDecimals()
    {
        Assert.Equal(18, InstantLaunch.TOKEN_DECIMALS);
        Assert.Equal(BigInteger.Pow(10, 27), InstantLaunch.TOTAL_SUPPLY_RAW);
    }

    // ---- pool key / pool id ----

    [Fact]
    public void GetInstantLaunchPoolKey_DerivesTheHooklessNativeEthPoolWithEthAsCurrency0()
    {
        var key = InstantLaunch.GetInstantLaunchPoolKey(LAUNCHED_TOKEN);

        Assert.Equal(Constants.ZERO_ADDRESS, key.Currency0);
        Assert.Equal(LAUNCHED_TOKEN, key.Currency1);
        Assert.Equal(2500, key.Fee);
        Assert.Equal(25, key.TickSpacing);
        Assert.Equal(Constants.ZERO_ADDRESS, key.Hooks);
    }

    [Fact]
    public void GetInstantLaunchPoolKey_AcceptsAnExplicitSpacingForEarlierGenerations() =>
        Assert.Equal(60, InstantLaunch.GetInstantLaunchPoolKey(LAUNCHED_TOKEN, 60).TickSpacing);

    [Fact]
    public void GetInstantLaunchPoolKey_Eip55NormalizesALowercaseTokenAddress() =>
        Assert.Equal(LAUNCHED_TOKEN,
            InstantLaunch.GetInstantLaunchPoolKey(LAUNCHED_TOKEN.ToLowerInvariant()).Currency1);

    [Fact]
    public void GetInstantLaunchPoolKey_RejectsAMalformedOrZeroTokenAddress()
    {
        Assert.Equal(LauncherErrorCode.INVALID_INPUT,
            Assert.Throws<LauncherSdkError>(() => InstantLaunch.GetInstantLaunchPoolKey("nonsense")).Code);

        Assert.Equal(LauncherErrorCode.INVALID_INPUT,
            Assert.Throws<LauncherSdkError>(
                () => InstantLaunch.GetInstantLaunchPoolKey(Constants.ZERO_ADDRESS)).Code);
    }

    [Fact]
    public void GetInstantLaunchPoolKeys_DerivesOneCandidatePerGrandfatheredSpacingNewestFirst()
    {
        var keys = InstantLaunch.GetInstantLaunchPoolKeys(LAUNCHED_TOKEN);

        Assert.Equal(2, keys.Count);
        Assert.Equal(25, keys[0].TickSpacing);
        Assert.Equal(60, keys[1].TickSpacing);
        Assert.All(keys, key => Assert.Equal(LAUNCHED_TOKEN, key.Currency1));
    }

    [Fact]
    public void GetInstantLaunchPoolId_MatchesTheOnChainPoolIdOfThePostRedeployToken() =>
        Assert.Equal(REDEPLOY_LAUNCHED_TOKEN_POOL_ID,
            InstantLaunch.GetInstantLaunchPoolId(REDEPLOY_LAUNCHED_TOKEN));

    [Fact]
    public void GetInstantLaunchPoolId_MatchesTheOnChainPoolIdOfThePreRedeployTokenAtItsLegacySpacing() =>
        Assert.Equal(LAUNCHED_TOKEN_POOL_ID, InstantLaunch.GetInstantLaunchPoolId(LAUNCHED_TOKEN, 60));

    [Fact]
    public void GetInstantLaunchPoolId_IsCasingIndependent() =>
        Assert.Equal(LAUNCHED_TOKEN_POOL_ID,
            InstantLaunch.GetInstantLaunchPoolId(LAUNCHED_TOKEN.ToLowerInvariant(), 60));

    [Fact]
    public void GetInstantLaunchPoolId_AgreesWithTheGenericComputeLbpPoolIdDerivation() =>
        Assert.Equal(
            PoolId.ComputeLbpPoolId(Constants.ZERO_ADDRESS, REDEPLOY_LAUNCHED_TOKEN, 2500, 25, Constants.ZERO_ADDRESS),
            InstantLaunch.GetInstantLaunchPoolId(REDEPLOY_LAUNCHED_TOKEN));
}
