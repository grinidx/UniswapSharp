using System.Numerics;
using UniswapSharp.Core.Utils;

namespace UniswapSharp.LiquidityLauncher;

/// <summary>
/// The fully-resolved Instant Launch stack for one (chain, creator-fee variant) pair.
/// </summary>
/// <param name="LiquidityLauncher">LiquidityLauncher singleton — the <c>multicall</c> entrypoint the wallet calls.</param>
/// <param name="Uerc20Factory">uERC20 token factory <c>createToken</c> targets.</param>
/// <param name="Strategy">The variant's hookless InstantLaunchStrategy (<c>Distribution.strategy</c>).</param>
/// <param name="FeeSplitter">The strategy's immutable FeeSplitter — permanent LP-NFT custodian and fee distributor.</param>
/// <param name="BeneficiaryVault">
/// UERC20BeneficiaryVault singleton — the fee-beneficiary ERC721 registry and the creator share's vault.
/// Only the fees-on strategy registers beneficiaries with it, but it is a chain singleton.
/// </param>
/// <param name="CompoundingClaimRecipient">CompoundingClaimRecipient singleton — the autocompound recipient of every FeeSplitter.</param>
/// <param name="CreatorFeesEnabled">Which variant this stack is.</param>
public sealed record InstantLaunchAddresses(
    string LiquidityLauncher,
    string Uerc20Factory,
    string Strategy,
    string FeeSplitter,
    string BeneficiaryVault,
    string CompoundingClaimRecipient,
    bool CreatorFeesEnabled);

/// <summary>A Uniswap v4 <c>PoolKey</c> struct mirror (currencies sorted ascending, as the PoolManager requires).</summary>
public sealed record V4PoolKey(
    string Currency0,
    string Currency1,
    int Fee,
    int TickSpacing,
    string Hooks);

/// <summary>A signable Instant Launch transaction — the single launcher <c>multicall</c>, tagged with its chain.</summary>
public sealed record InstantLaunchTransaction(string To, string Data, BigInteger Value, int ChainId);

/// <summary>Inputs to <see cref="InstantLaunch.BuildInstantLaunchTransaction"/>.</summary>
/// <param name="ChainId">Launch chain.</param>
/// <param name="Name">Token name.</param>
/// <param name="Symbol">Token symbol.</param>
/// <param name="PredictedTokenAddress">Deterministic new-token address.</param>
/// <param name="Metadata">On-chain uERC20 metadata.</param>
/// <param name="Salt">bytes32 user salt (the singleton strategies ignore it, but the launcher call carries it).</param>
/// <param name="CreatorFeesEnabled">Selects the strategy instance.</param>
/// <param name="FeeBeneficiary">
/// Required when <paramref name="CreatorFeesEnabled"/> is <c>true</c>; must be <c>null</c> otherwise —
/// the builder encodes <see cref="InstantLaunch.DISABLED_CREATOR_FEE_BENEFICIARY"/> internally.
/// </param>
public sealed record BuildInstantLaunchParams(
    int ChainId,
    string Name,
    string Symbol,
    string PredictedTokenAddress,
    Uerc20Metadata Metadata,
    string Salt,
    bool CreatorFeesEnabled,
    string? FeeBeneficiary = null);

/// <summary>
/// Instant Launch — the canonical preset plus transaction assembler, mirroring <see cref="QuickLaunch"/>'s
/// role for the CCA path. Launch params in, one signable transaction out. Ported from
/// sdks/liquidity-launcher-sdk/src/instantLaunch.ts.
/// </summary>
/// <remarks>
/// <para>
/// On-chain flow (hookless InstantLaunchStrategy): one <c>LiquidityLauncher.multicall</c> wrapping
/// <c>createToken</c> — which mints the fixed 1B supply straight to the launcher — then
/// <c>distributeToken</c>, where the strategy pulls the full supply, initializes the hookless
/// native-ETH v4 pool, optionally registers the fee beneficiary with the vault, and parks the
/// single-sided LP NFT in its FeeSplitter forever. <c>msg.value</c> is always 0.
/// </para>
/// <para>
/// Creator fees are a <b>deployment variant, not a launch parameter</b>: each chain deploys two
/// strategy instances — one whose immutable <c>beneficiaryVault</c> is set (fees on) and one where it
/// is zero (fees off) — and the builder selects between them.
/// </para>
/// </remarks>
public static class InstantLaunch
{
    /// <summary>Factory tokens are fixed at 18 decimals (the strategy reverts otherwise).</summary>
    public const int TOKEN_DECIMALS = Constants.NEW_TOKEN_DECIMALS;

    /// <summary>Fixed, standardized total supply: 1,000,000,000 (1B) whole tokens.</summary>
    public static readonly BigInteger TOTAL_SUPPLY = 1_000_000_000;

    /// <summary>Total supply in raw base units, required exactly by InstantLaunchStrategy: 1B @ 18 decimals = 1e27.</summary>
    public static readonly BigInteger TOTAL_SUPPLY_RAW = TOTAL_SUPPLY * BigInteger.Pow(10, TOKEN_DECIMALS);

    /// <summary>
    /// The <c>feeBeneficiary</c> the builder encodes when creator fees are disabled.
    /// </summary>
    /// <remarks>
    /// The strategy's <c>InstantLaunchConfig{feeBeneficiary}</c> is mandatory on <b>every</b> instance —
    /// including the fees-off one, where the value goes unused because its <c>beneficiaryVault</c>
    /// immutable is zero, so registration is skipped entirely — and the strategy reverts on a zero or
    /// launcher beneficiary either way. So the placeholder must be a non-zero address that is not the
    /// LiquidityLauncher; it is deliberately a protocol-owned contract rather than a user address, since
    /// it must never be mistaken for a creator claim.
    /// </remarks>
    public static readonly string DISABLED_CREATOR_FEE_BENEFICIARY =
        AddressValidator.GetAddress("0xf9526Dd3361fe0ba6b7a99533ed471D3E808E99a");

    /// <summary>InstantLaunchStrategy's compile-time pool LP fee (pips) — unchanged across deploys.</summary>
    public const int POOL_LP_FEE = 2500;

    /// <summary>
    /// InstantLaunchStrategy's compile-time pool tick spacing, per-generation since the 2026-08-05
    /// chain-4663 full redeploy recompiled the strategy at 25 (every earlier generation is 60). This
    /// constant is the CURRENT generation's value — the authoritative value for any strategy instance is
    /// <see cref="InstantLaunchDeployment.TickSpacing"/>, and a token's launch pool keeps its minting
    /// generation's spacing forever.
    /// </summary>
    public const int POOL_TICK_SPACING = 25;

    /// <summary>
    /// Every tick spacing an Instant Launch pool has ever been minted at, newest first — the append-only
    /// grandfather set. Pools are permanent, so a superseded spacing never leaves this list; consumers
    /// deriving a token's candidate launch pools must race a <c>(POOL_LP_FEE, spacing)</c> key for EVERY
    /// entry, because the token address alone cannot say which generation minted the pool.
    /// </summary>
    /// <remarks>
    /// Every entry is a pinned literal: if a future generation changes <see cref="POOL_TICK_SPACING"/>,
    /// the new spacing must be APPENDED rather than a derived entry silently replacing 25.
    /// </remarks>
    public static readonly IReadOnlyList<int> ALLOWED_POOL_TICK_SPACINGS = new[] { 25, 60 };

    /// <summary>The launch pool is hookless.</summary>
    public const string POOL_HOOKS = Constants.ZERO_ADDRESS;

    /// <summary>The launch pool's raise currency: native ETH, which always sorts as <c>currency0</c>.</summary>
    public const string POOL_CURRENCY0 = Constants.ZERO_ADDRESS;

    /// <summary>
    /// InstantLaunchStrategy's compile-time lower tick of every launch position and the exclusive floor
    /// for <c>initialTick</c>. Per-generation since the 2026-08-05 redeploy recompiled at -160,100
    /// (every earlier generation is -208,980, the OZ H01 floor).
    /// </summary>
    public const int MIN_LAUNCH_TICK = -160_100;

    /// <summary>
    /// The current 4663 deployments' immutable <c>initialTick</c> — the aligned tick the launch pool
    /// opens at (highest price; the launch position's upper bound). A per-deployment immutable, not a
    /// compile-time constant: the authoritative value for any instance is
    /// <see cref="InstantLaunchDeployment.InitialTick"/>.
    /// </summary>
    public const int INITIAL_TICK = 198_050;

    /// <summary>
    /// Returns the Instant Launch stack for a chain and creator-fee variant, or <c>null</c> where any
    /// piece of it is not deployed. Derived from the deployment registry, never a second copy.
    /// </summary>
    public static InstantLaunchAddresses? GetInstantLaunchAddresses(int chainId, bool creatorFeesEnabled)
    {
        var launcher = Addresses.GetLauncherAddresses(chainId);
        var contracts = Addresses.GetInstantLaunchContracts(chainId);
        var deployment = Addresses.GetInstantLaunchStrategy(chainId, creatorFeesEnabled);

        if (launcher?.Uerc20Factory is null || contracts is null || deployment is null)
        {
            return null;
        }

        return new InstantLaunchAddresses(
            launcher.LiquidityLauncher,
            launcher.Uerc20Factory,
            deployment.Strategy,
            deployment.FeeSplitter,
            contracts.BeneficiaryVault,
            contracts.CompoundingClaimRecipient,
            deployment.CreatorFeesEnabled);
    }

    /// <summary>
    /// Whether Instant Launch is deployed on <paramref name="chainId"/> — i.e.
    /// <see cref="GetInstantLaunchAddresses"/> resolves for at least one creator-fee variant.
    /// </summary>
    public static bool IsInstantLaunchSupportedChain(int chainId) =>
        GetInstantLaunchAddresses(chainId, true) is not null ||
        GetInstantLaunchAddresses(chainId, false) is not null;

    /// <summary>
    /// Pure assembler: builds the one-transaction Instant Launch multicall (createToken then
    /// distributeToken; <c>value</c> is always 0). Mirrors the on-chain guards where they are cheap to
    /// check client-side — a zero or launcher <c>feeBeneficiary</c> reverts in the strategy, and the
    /// vault rejects itself at registration.
    /// </summary>
    public static InstantLaunchTransaction BuildInstantLaunchTransaction(BuildInstantLaunchParams parameters)
    {
        var addresses = RequireInstantLaunchAddresses(parameters.ChainId, parameters.CreatorFeesEnabled);

        string feeBeneficiary;
        if (parameters.CreatorFeesEnabled)
        {
            if (parameters.FeeBeneficiary is null ||
                AddressEquals(parameters.FeeBeneficiary, Constants.ZERO_ADDRESS) ||
                AddressEquals(parameters.FeeBeneficiary, addresses.LiquidityLauncher) ||
                AddressEquals(parameters.FeeBeneficiary, addresses.BeneficiaryVault))
            {
                throw new LauncherSdkError(
                    LauncherErrorCode.INVALID_INPUT,
                    $"Invalid Instant Launch fee beneficiary: {parameters.FeeBeneficiary ?? "null"}");
            }
            feeBeneficiary = parameters.FeeBeneficiary;
        }
        else
        {
            if (parameters.FeeBeneficiary is not null)
            {
                throw new LauncherSdkError(
                    LauncherErrorCode.INVALID_INPUT,
                    "feeBeneficiary must not be set when creator fees are disabled — the fees-off strategy ignores it");
            }
            feeBeneficiary = DISABLED_CREATOR_FEE_BENEFICIARY;
        }

        var transactions = Build.BuildLaunchTransactions(new BuildLaunchParams(
            LiquidityLauncher: addresses.LiquidityLauncher,
            Token: parameters.PredictedTokenAddress,
            Salt: parameters.Salt,
            Acquire: new CreateTokenAcquisition(new CreateTokenArgs(
                Factory: addresses.Uerc20Factory,
                Name: parameters.Name,
                Symbol: parameters.Symbol,
                Decimals: TOKEN_DECIMALS,
                InitialSupply: TOTAL_SUPPLY_RAW,
                // The launcher must hold the mint: distributeToken approves the strategy, which pulls
                // the full supply from the launcher.
                Recipient: addresses.LiquidityLauncher,
                TokenData: Encode.EncodeTokenData(parameters.Metadata))),
            Distributions: new[]
            {
                new Distribution(
                    addresses.Strategy,
                    TOTAL_SUPPLY_RAW,
                    Encode.EncodeInstantLaunchConfig(feeBeneficiary)),
            }));

        if (transactions.Count == 0)
        {
            throw new LauncherSdkError(
                LauncherErrorCode.INVALID_INPUT, "BuildLaunchTransactions returned no transaction");
        }

        var transaction = transactions[0];
        return new InstantLaunchTransaction(
            transaction.To, transaction.Data, transaction.Value, parameters.ChainId);
    }

    /// <summary>
    /// The v4 pool an Instant Launch token trades in: hookless native-ETH pool at the strategy's
    /// fee/spacing. Native ETH sorts below every token, so <c>currency0</c> is always ETH and an
    /// ETH→token swap is always <c>zeroForOne</c>. The token address is EIP-55 normalized.
    /// </summary>
    /// <param name="token">The launched token.</param>
    /// <param name="tickSpacing">
    /// Defaults to the current generation's <see cref="POOL_TICK_SPACING"/>. For a token launched before
    /// the 2026-08-05 redeploy, pass its minting generation's spacing — or race every candidate via
    /// <see cref="GetInstantLaunchPoolKeys"/>.
    /// </param>
    public static V4PoolKey GetInstantLaunchPoolKey(string token, int? tickSpacing = null) => new(
        POOL_CURRENCY0,
        NormalizeInstantLaunchToken(token),
        POOL_LP_FEE,
        tickSpacing ?? POOL_TICK_SPACING,
        POOL_HOOKS);

    /// <summary>
    /// Every launch pool the token COULD trade in, newest generation first — one candidate per
    /// <see cref="ALLOWED_POOL_TICK_SPACINGS"/> entry. Exactly one is initialized on-chain (the minting
    /// generation's); when the minting strategy is unknown, probe all of them and keep the one that
    /// answers.
    /// </summary>
    public static IReadOnlyList<V4PoolKey> GetInstantLaunchPoolKeys(string token) =>
        ALLOWED_POOL_TICK_SPACINGS.Select(spacing => GetInstantLaunchPoolKey(token, spacing)).ToList();

    /// <summary>
    /// The launch pool's v4 PoolId — <c>keccak256(abi.encode(poolKey))</c>, matching the on-chain
    /// <c>PoolKey.toId()</c>. Same tick-spacing caveat as <see cref="GetInstantLaunchPoolKey"/>.
    /// </summary>
    public static string GetInstantLaunchPoolId(string token, int? tickSpacing = null)
    {
        var key = GetInstantLaunchPoolKey(token, tickSpacing);
        return PoolId.ComputeLbpPoolId(key.Currency0, key.Currency1, key.Fee, key.TickSpacing, key.Hooks);
    }

    private static bool AddressEquals(string a, string b) => a.Equals(b, StringComparison.OrdinalIgnoreCase);

    /// <summary>EIP-55 normalizes the token address; rejects malformed input and the native-currency sentinel.</summary>
    private static string NormalizeInstantLaunchToken(string token)
    {
        string normalized;
        try
        {
            normalized = AddressValidator.GetAddress(token);
        }
        catch (Exception)
        {
            throw new LauncherSdkError(
                LauncherErrorCode.INVALID_INPUT, $"Invalid Instant Launch token address: {token}");
        }

        if (AddressEquals(normalized, Constants.ZERO_ADDRESS))
        {
            throw new LauncherSdkError(
                LauncherErrorCode.INVALID_INPUT, "Instant Launch token address must not be the zero address");
        }
        return normalized;
    }

    private static InstantLaunchAddresses RequireInstantLaunchAddresses(int chainId, bool creatorFeesEnabled) =>
        GetInstantLaunchAddresses(chainId, creatorFeesEnabled)
        ?? throw new LauncherSdkError(
            LauncherErrorCode.UNSUPPORTED_CHAIN,
            $"Instant Launch (creator fees {(creatorFeesEnabled ? "enabled" : "disabled")}) is not deployed on chain {chainId}");
}
