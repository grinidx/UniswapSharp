using System.Numerics;
using UniswapSharp.LiquidityLauncher.Config;

namespace UniswapSharp.LiquidityLauncher;

/// <summary>
/// Lock-recipient modes plus two modes with no per-launch recipient contract at all.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Burn"/> — the LP position minted straight to the burn address. Not a buildable
/// <c>LockRecipientInput</c> mode (there is nothing to deploy), but a first-class lock mode for
/// classification: a burned position is irrecoverable, i.e. <i>structurally</i> permanent, and such
/// rows carry <c>unlock_block = 0</c>, so permanence for it must never be derived from an unlock
/// horizon.
/// </para>
/// <para>
/// <see cref="CreatorFees"/> — the LP position sent to the chain's fees-enabled FeeSplitter, which
/// routes the creator's share of native fees to the BeneficiaryVault and auto-compounds the rest.
/// Also not buildable here (the splitter is a pre-deployed singleton) and likewise structurally
/// permanent: the splitter has no code path that transfers positions out. Callers derive this mode by
/// matching the decoded <c>MigratorParameters.positionRecipient</c> with
/// <see cref="Addresses.IsCreatorFeesPositionRecipient"/>; the matcher itself stays address-free.
/// </para>
/// </remarks>
public enum QuickLaunchLockMode
{
    Timelock,
    FeesForwarder,
    BuybackBurn,
    Burn,
    CreatorFees,
}

/// <summary>
/// Decoded liquidity-lock descriptor for the matcher: the lock mode plus whether the timelock is
/// permanent.
/// </summary>
public sealed record QuickLaunchLockDescriptor(QuickLaunchLockMode Mode, bool PermanentTimelock);

/// <summary>The fixed, non-configurable server-side convex emission curve (anti-snipe fairness backbone).</summary>
public sealed record QuickLaunchEmission(int NumSteps, double FinalBlockPct, double Alpha);

/// <summary>The LP half of <see cref="QuickLaunch.PRESET"/>.</summary>
public sealed record QuickLaunchLp(
    int Fee,
    int TickSpacing,
    PriceRangeKind Range,
    QuickLaunchLockMode LockMode,
    bool PermanentTimelock,
    double SearcherBurnThresholdPercent);

/// <summary>
/// The canonical quick-launch parameter set. Every field is chain-independent (factory tokens are
/// always 18 decimals, native raise is <c>address(0)</c> on every chain, the duration is a fixed
/// real-time window), so the preset is a frozen constant rather than a per-chain function. The two
/// values that ARE chain-dependent — the duration in blocks and the floor price — are derived at build
/// time from the chain block time / live native-currency USD price.
/// </summary>
public sealed record QuickLaunchPreset(
    string AuctionType,
    bool InstantStart,
    int DurationSeconds,
    int TokenDecimals,
    BigInteger TotalSupplyRaw,
    BigInteger AuctionSupplyRaw,
    BigInteger ReservedForLpRaw,
    int SupplyAuctionedPercent,
    string RaiseCurrency,
    double FloorFdvUsd,
    double GraduationFdvUsd,
    QuickLaunchLp Lp,
    QuickLaunchEmission Emission);

/// <summary>The structural, address-free fields <see cref="QuickLaunch.IsQuickLaunch"/> compares against the preset.</summary>
/// <param name="ChainId">Launch chain id — needed to convert the block window into real seconds.</param>
/// <param name="Currency">CCA raise currency (<c>AuctionParameters.currency</c>); <c>address(0)</c> = native.</param>
/// <param name="StartBlock">CCA <c>AuctionParameters.startBlock</c>.</param>
/// <param name="EndBlock">CCA <c>AuctionParameters.endBlock</c>.</param>
/// <param name="TotalSupplyRaw">The token's total supply in raw base units (18dp).</param>
/// <param name="ReservedTokenAmountForLP">
/// <c>MigratorParameters.reservedTokenAmountForLP</c>, if decoded. When present it must equal the
/// preset's 50% LP reserve; <c>null</c> means <i>unknown</i> and leaves the 50/50 split unasserted.
/// The strategy getter returns a zeroed struct for an unset entry — callers must map such a read to
/// <c>null</c>, never pass the raw <c>0</c>, which is a real (failing) value.
/// </param>
/// <param name="Lock">
/// The liquidity lock decoded from <c>MigratorParameters.positionRecipient</c>. Use
/// <see cref="QuickLaunch.NoLockResolved"/> to say <i>resolved: this auction has no lock</i>, which
/// fails the match; leave <c>null</c> for <i>not resolved yet</i>, which leaves the lock unasserted.
/// </param>
/// <param name="GraduationFdvUsd">
/// The auction's graduation threshold as a target FDV in USD, frozen at ingest by the caller.
/// USD-denominated so the gate is chain-agnostic — a raw-native threshold would wrongly demote every
/// non-ETH chain. <c>null</c> and any non-finite value both mean <i>unresolved</i> and leave the
/// assertion off; <c>0</c>, being finite, is a real value and is rejected.
/// </param>
public sealed record QuickLaunchMatchParams(
    int ChainId,
    string Currency,
    BigInteger StartBlock,
    BigInteger EndBlock,
    BigInteger TotalSupplyRaw,
    BigInteger? ReservedTokenAmountForLP = null,
    QuickLaunchLockDescriptor? Lock = null,
    double? GraduationFdvUsd = null);

/// <summary>Options for <see cref="QuickLaunch.IsQuickLaunch"/>.</summary>
/// <param name="DurationToleranceRatio">Fractional tolerance on the duration comparison.</param>
/// <param name="AllowedDurationsSeconds">
/// Durations (seconds) accepted as quick-launch. Defaults to the current canonical preset (4h only).
/// The earlier POC created 30m/1h/4h auctions; recognizing those historical windows is opt-in via this
/// override so callers make the choice explicitly.
/// </param>
/// <param name="AllowedGraduationFdvUsd">Graduation-FDV values (USD) accepted as quick-launch.</param>
/// <param name="GraduationFdvToleranceRatio">Fractional tolerance on the graduation-FDV comparison.</param>
public sealed record QuickLaunchMatchOptions(
    double? DurationToleranceRatio = null,
    IReadOnlyList<int>? AllowedDurationsSeconds = null,
    IReadOnlyList<double>? AllowedGraduationFdvUsd = null,
    double? GraduationFdvToleranceRatio = null);

/// <summary>Inputs to <see cref="QuickLaunch.IsPermanentTimelock"/>.</summary>
/// <remarks>
/// Three accepted forms, matching the shapes call sites actually hold:
/// <list type="number">
/// <item><b>Block form</b> (<c>ChainId</c>, <c>EndBlock</c>, <c>UnlockBlock</c>) — the canonical,
/// chain-aware check: the block horizon past the auction end, converted to real seconds via the chain
/// block time, must reach <see cref="QuickLaunch.PERMANENT_TIMELOCK_MIN_HORIZON_SECONDS"/>.</item>
/// <item><b>Timestamp form</b> (<c>EndTimeSeconds</c>, <c>UnlockTimeSeconds</c>) — the same horizon
/// rule over real seconds, for the create flow.</item>
/// <item><b>Raw-block sentinel form</b> (<c>UnlockBlock</c> alone) — the chain-agnostic
/// <see cref="QuickLaunch.PERMANENT_UNLOCK_BLOCK_THRESHOLD"/> approximation.</item>
/// </list>
/// <c>LockMode</c> may accompany any form: the structurally permanent modes short-circuit to
/// <c>true</c> before any horizon math.
/// </remarks>
public sealed record PermanentTimelockParams(
    QuickLaunchLockMode? LockMode = null,
    int? ChainId = null,
    BigInteger? EndBlock = null,
    BigInteger? UnlockBlock = null,
    BigInteger? EndTimeSeconds = null,
    BigInteger? UnlockTimeSeconds = null);

/// <summary>
/// The canonical "quick launch" definition — the single source of truth client-side (create flow +
/// discovery badge) and server-side (classification) consumers share. Ported from
/// sdks/liquidity-launcher-sdk/src/quickLaunch.ts.
/// </summary>
/// <remarks>
/// A quick launch is not a separate contract: it is a CCA auction created with this fixed,
/// non-negotiable parameter set, so classification is purely by parameters.
/// <para>
/// SECURITY NOTE: the classifier is a cosmetic / discovery descriptor only. Because the preset is
/// reproducible by construction (anyone can create a CCA matching these exact pure params), a positive
/// match MUST NOT gate suppression of Blockaid / token-protection warnings — it is not a trust signal.
/// </para>
/// </remarks>
public static class QuickLaunch
{
    /// <summary>Quick launches run for 4h only (14400s). Supersedes the earlier 30m/1h/4h set.</summary>
    public const int DURATION_SECONDS = 14_400;

    /// <summary>Fixed, standardized total supply: 1,000,000,000 (1B) whole tokens.</summary>
    public static readonly BigInteger TOTAL_SUPPLY = 1_000_000_000;

    /// <summary>Total supply in raw base units: 1B @ 18 decimals = 1e27.</summary>
    public static readonly BigInteger TOTAL_SUPPLY_RAW =
        TOTAL_SUPPLY * BigInteger.Pow(10, Constants.NEW_TOKEN_DECIMALS);

    /// <summary>50% of the total supply is auctioned.</summary>
    public const int SUPPLY_AUCTIONED_PERCENT = 50;

    /// <summary>The auctioned half of the supply, in raw base units (5e26).</summary>
    public static readonly BigInteger AUCTION_SUPPLY_RAW = TOTAL_SUPPLY_RAW / 2;

    /// <summary>
    /// The other half, paired with 100% of the raised proceeds to seed the LP — the CCA's
    /// <c>MigratorParameters.reservedTokenAmountForLP</c> (5e26).
    /// </summary>
    public static readonly BigInteger RESERVED_FOR_LP_RAW = TOTAL_SUPPLY_RAW / 2;

    /// <summary>Raise denomination: the chain's native currency only (ETH on most chains, USDC on Arc).</summary>
    public const string RAISE_CURRENCY = Constants.ZERO_ADDRESS;

    /// <summary>Starting clearing price floor, as a target FDV in USD (~$1k, cheap enough to deter spam).</summary>
    public const double FLOOR_FDV_USD = 1_000;

    /// <summary>Fraction of the total supply actually sold in the auction (the other half seeds the LP).</summary>
    public const double SOLD_SUPPLY_SHARE = SUPPLY_AUCTIONED_PERCENT / 100.0;

    /// <summary>
    /// Graduation threshold as a target FDV in USD ($10k FDV, i.e. ~$5k raised at the 50%-sold preset —
    /// the USD raise is always FDV × <see cref="SOLD_SUPPLY_SHARE"/>, never the FDV itself). Decoupled
    /// from <see cref="FLOOR_FDV_USD"/>.
    /// </summary>
    public const double GRADUATION_FDV_USD = 10_000;

    /// <summary>Approximate USD of (time-weighted) committed bids needed to graduate.</summary>
    public const double GRADUATION_RAISE_USD = GRADUATION_FDV_USD * SOLD_SUPPLY_SHARE;

    /// <summary>
    /// The graduation-FDV values (USD) a quick launch may carry. Grandfathers the historical $5k cohort
    /// alongside the current $10k preset. USD-denominated on purpose: the gate is chain-agnostic, so a
    /// legit $5k launch on any chain passes, while a raw-native threshold would wrongly demote every
    /// non-ETH chain.
    /// </summary>
    public static readonly IReadOnlyList<double> ALLOWED_GRADUATION_FDV_USD = new double[] { 5_000, 10_000 };

    /// <summary>
    /// Default fractional tolerance when comparing a resolved graduation FDV (USD) to an allowed preset
    /// value (±10%). Accepted bands: [4500, 5500] and [9000, 11000].
    /// </summary>
    public const double GRADUATION_FDV_TOLERANCE_RATIO = 0.1;

    /// <summary>V4 LP fee tier in hundredths of a bip (2500 = 0.25%).</summary>
    public const int LP_FEE = 2_500;

    /// <summary>
    /// V4 graduation-pool tick spacing, derived from <see cref="Fees.ResolveNewPoolTickSpacing"/> so the
    /// preset and the derivation cannot drift. This is the spacing NEW graduation pools are opened with;
    /// pools minted by an earlier generation keep the spacing they were initialized with — see
    /// <see cref="ALLOWED_POOL_TICK_SPACINGS"/>.
    /// </summary>
    public static readonly int POOL_TICK_SPACING = Fees.ResolveNewPoolTickSpacing(LP_FEE);

    /// <summary>
    /// Every tick spacing a quick-launch graduation pool has ever been minted at, newest first — the
    /// append-only grandfather set. Pools are permanent, so a superseded spacing never leaves this list;
    /// routing/discovery consumers deriving a token's candidate launch pools must race a
    /// <c>(LP_FEE, spacing)</c> key for EVERY entry, because the token address alone cannot say which
    /// generation minted the pool.
    /// </summary>
    /// <remarks>
    /// Every entry is a pinned literal: if the fee tier ever changes, the new spacing must be APPENDED
    /// here rather than a derived entry silently replacing 25.
    /// <list type="bullet">
    /// <item>25: since the 2026-08-05 chain-4663 full redeploy.</item>
    /// <item>50: every earlier generation.</item>
    /// </list>
    /// </remarks>
    public static readonly IReadOnlyList<int> ALLOWED_POOL_TICK_SPACINGS = new[] { 25, 50 };

    /// <summary>V4 LP price-range strategy: full-range + concentrated.</summary>
    public const PriceRangeKind LP_RANGE = PriceRangeKind.CONCENTRATED_FULL_RANGE;

    /// <summary>
    /// The migrated LP is locked forever (permanent timelock) via a buyback-&amp;-burn lock recipient.
    /// Launches created before 2026-08-03 carry this lock; since then fees-off quick launches
    /// autocompound instead — the LP position goes to the fees-off FeeSplitter, which is structurally
    /// permanent, not a buyback-&amp;-burn lock.
    /// </summary>
    public const QuickLaunchLockMode LOCK_MODE = QuickLaunchLockMode.BuybackBurn;

    /// <summary>
    /// Minimum lock horizon past the auction end, in real seconds, for a timelock to count as
    /// <i>permanent</i> (1000 years). 1000 years sits in a very wide empty band — exactly 100× under
    /// what the create flow requests, ~100× over the longest plausible real lock — so block-time drift
    /// cannot move an auction across it.
    /// </summary>
    public const long PERMANENT_TIMELOCK_MIN_HORIZON_SECONDS = 1000L * 365 * 86_400;

    /// <summary>
    /// The lock horizon the create flow <i>requests</i> for a permanent lock (~100k years).
    /// Deliberately ~100× over <see cref="PERMANENT_TIMELOCK_MIN_HORIZON_SECONDS"/> so a
    /// requested-permanent lock can never be classified finite, on any plausible block-time table.
    /// </summary>
    public static readonly BigInteger PERMANENT_TIMELOCK_REQUEST_SECONDS =
        new BigInteger(365) * 100_000 * 86_400;

    /// <summary>
    /// Chain-AGNOSTIC approximation: a raw unlock block at or past this threshold counts as permanent
    /// without consulting the chain's block time. Prefer the chain-aware forms of
    /// <see cref="IsPermanentTimelock"/> whenever the chain id and auction end are available — a single
    /// block count cannot express "1000 years" on every chain (on a 0.1s chain this is only ~600 years).
    /// </summary>
    public static readonly BigInteger PERMANENT_UNLOCK_BLOCK_THRESHOLD = 200_000_000_000L;

    /// <summary>
    /// Buyback-&amp;-burn searcher threshold: a searcher burns ~0.05% of supply to claim the accrued ETH
    /// (the token portion is burned in the same call).
    /// </summary>
    public const double SEARCHER_BURN_THRESHOLD_PERCENT = 0.05;

    /// <summary>Default fractional tolerance when comparing a derived auction duration to the 4h target (±10%).</summary>
    public const double DURATION_TOLERANCE_RATIO = 0.1;

    /// <summary>
    /// The lock modes whose permanence is <i>structural</i> — the position can never leave its
    /// custodian, so there is no unlock horizon to check (their rows carry <c>unlock_block = 0</c>).
    /// </summary>
    public static readonly IReadOnlyList<QuickLaunchLockMode> STRUCTURALLY_PERMANENT_LOCK_MODES =
        new[] { QuickLaunchLockMode.Burn, QuickLaunchLockMode.CreatorFees };

    /// <summary>Whether <paramref name="mode"/>'s permanence is structural.</summary>
    public static bool IsStructurallyPermanentLockMode(QuickLaunchLockMode mode) =>
        STRUCTURALLY_PERMANENT_LOCK_MODES.Contains(mode);

    /// <summary>The canonical quick-launch parameter set.</summary>
    public static readonly QuickLaunchPreset PRESET = new(
        AuctionType: "CCA",
        InstantStart: true,
        DurationSeconds: DURATION_SECONDS,
        TokenDecimals: Constants.NEW_TOKEN_DECIMALS,
        TotalSupplyRaw: TOTAL_SUPPLY_RAW,
        AuctionSupplyRaw: AUCTION_SUPPLY_RAW,
        ReservedForLpRaw: RESERVED_FOR_LP_RAW,
        SupplyAuctionedPercent: SUPPLY_AUCTIONED_PERCENT,
        RaiseCurrency: RAISE_CURRENCY,
        FloorFdvUsd: FLOOR_FDV_USD,
        GraduationFdvUsd: GRADUATION_FDV_USD,
        Lp: new QuickLaunchLp(
            LP_FEE, POOL_TICK_SPACING, LP_RANGE, LOCK_MODE,
            PermanentTimelock: true, SearcherBurnThresholdPercent: SEARCHER_BURN_THRESHOLD_PERCENT),
        Emission: new QuickLaunchEmission(
            Constants.DEFAULT_AUCTION_STEPS, Constants.DEFAULT_FINAL_BLOCK_PCT, Constants.DEFAULT_CONVEXITY_ALPHA));

    /// <summary>
    /// Sentinel for <see cref="QuickLaunchMatchParams.Lock"/> meaning <i>resolved: this auction has no
    /// lock</i>, which fails the match. Distinct from a <c>null</c> <c>Lock</c>, which means
    /// <i>not resolved yet</i> and leaves the lock unasserted.
    /// </summary>
    public static readonly QuickLaunchLockDescriptor NoLockResolved =
        new(QuickLaunchLockMode.Timelock, PermanentTimelock: false);

    /// <summary>The 4h window as a block count on <paramref name="chainId"/> (uses the chain's block time).</summary>
    public static BigInteger GetDurationBlocks(int chainId) =>
        new BigInteger(Math.Round(DURATION_SECONDS / Blocks.GetBlockTimeSeconds(chainId), MidpointRounding.AwayFromZero));

    /// <summary>
    /// The preset floor as the CreateAuction <c>floor_price_raise_per_token</c> decimal:
    /// <see cref="FLOOR_FDV_USD"/> / 1B tokens, converted to the raise currency at
    /// <paramref name="nativeUsdPrice"/>.
    /// </summary>
    public static string GetFloorPricePerToken(double nativeUsdPrice) =>
        Price.FdvUsdToPricePerToken(FLOOR_FDV_USD, TOTAL_SUPPLY, nativeUsdPrice);

    /// <summary>
    /// The preset graduation threshold as the CreateAuction <c>graduation_price_raise_per_token</c>
    /// decimal — the same derivation as the floor, over the FULL supply. The service turns it into
    /// <c>requiredCurrencyRaised = graduationPrice × soldSupply</c>, so the USD raise it demands is
    /// graduation FDV × <see cref="SOLD_SUPPLY_SHARE"/>, never the FDV 1:1.
    /// </summary>
    public static string GetGraduationPricePerToken(double nativeUsdPrice) =>
        Price.FdvUsdToPricePerToken(GRADUATION_FDV_USD, TOTAL_SUPPLY, nativeUsdPrice);

    /// <summary>
    /// The canonical predicate for whether a liquidity lock is <i>permanent</i> — judged past the
    /// auction end, because that is how the create flow derives its unlock time before it is converted
    /// to the block number the recipient stores as an immutable.
    /// </summary>
    public static bool IsPermanentTimelock(PermanentTimelockParams parameters)
    {
        // A burned or splitter-parked position has no timelock to expire — permanence is structural,
        // not derived. Such rows carry unlock_block = 0, so falling through to the horizon math would
        // wrongly report finite.
        if (parameters.LockMode is { } mode && IsStructurallyPermanentLockMode(mode))
        {
            return true;
        }

        // Timestamp form: the create flow's real-seconds horizon past the auction end.
        if (parameters.EndTimeSeconds is { } endTime && parameters.UnlockTimeSeconds is { } unlockTime)
        {
            return (double)(unlockTime - endTime) >= PERMANENT_TIMELOCK_MIN_HORIZON_SECONDS;
        }

        // Block form: chain-aware horizon via the chain block time.
        if (parameters.ChainId is { } chainId && parameters.EndBlock is { } endBlock &&
            parameters.UnlockBlock is { } blockFormUnlock)
        {
            double horizonSeconds = (double)(blockFormUnlock - endBlock) * Blocks.GetBlockTimeSeconds(chainId);
            return horizonSeconds >= PERMANENT_TIMELOCK_MIN_HORIZON_SECONDS;
        }

        // Sentinel form: chain-agnostic raw-block approximation.
        return parameters.UnlockBlock is { } unlockBlock && unlockBlock >= PERMANENT_UNLOCK_BLOCK_THRESHOLD;
    }

    /// <summary>
    /// Pure, deterministic matcher: whether a CCA auction's on-chain parameters match
    /// <see cref="PRESET"/>. No I/O and no comparisons against specific contract/migrator addresses —
    /// classification stays address-independent. Checking the raise currency against the native
    /// zero-address sentinel is a denomination check, not an address-identity comparison.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Presumes a CCA (v2) auction — gate on the auction version first. The floor / clearing price is
    /// intentionally NOT matched: it is derived from the live native-currency USD price and so is not a
    /// stable structural field.
    /// </para>
    /// <para>
    /// Required fingerprint (always available from indexed data): native raise currency, 1B total
    /// supply, and the 4h duration. The 50/50 LP reserve and the permanent lock are matched only when
    /// supplied — with one asymmetry: a resolved "no lock" fails, while an unknown reserve stays
    /// unasserted. Since a refinement can only turn a match into a non-match, classifying without them
    /// is a safe over-approximation a later pass can tighten.
    /// </para>
    /// <para>
    /// SECURITY NOTE: the preset is reproducible by construction, so a positive match — even with the
    /// graduation gate — is a cosmetic / discovery descriptor and MUST NOT gate Blockaid /
    /// token-protection warnings.
    /// </para>
    /// </remarks>
    public static bool IsQuickLaunch(QuickLaunchMatchParams parameters, QuickLaunchMatchOptions? options = null)
    {
        options ??= new QuickLaunchMatchOptions();
        double durationToleranceRatio = options.DurationToleranceRatio ?? DURATION_TOLERANCE_RATIO;
        IReadOnlyList<int> allowedDurationsSeconds = options.AllowedDurationsSeconds ?? new[] { DURATION_SECONDS };
        IReadOnlyList<double> allowedGraduationFdvUsd = options.AllowedGraduationFdvUsd ?? ALLOWED_GRADUATION_FDV_USD;
        double graduationFdvToleranceRatio =
            options.GraduationFdvToleranceRatio ?? GRADUATION_FDV_TOLERANCE_RATIO;

        // Raise denomination: native only.
        if (!parameters.Currency.Equals(RAISE_CURRENCY, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Total supply: exactly 1B @ 18dp.
        if (parameters.TotalSupplyRaw != TOTAL_SUPPLY_RAW)
        {
            return false;
        }

        // Duration: the block window, converted to real seconds, must match an allowed duration within
        // tolerance.
        if (parameters.EndBlock <= parameters.StartBlock)
        {
            return false;
        }
        double durationSeconds =
            (double)(parameters.EndBlock - parameters.StartBlock) * Blocks.GetBlockTimeSeconds(parameters.ChainId);
        if (!allowedDurationsSeconds.Any(target => Math.Abs(durationSeconds - target) <= target * durationToleranceRatio))
        {
            return false;
        }

        // 50/50 supply split — asserted only when the LP reserve is known.
        if (parameters.ReservedTokenAmountForLP is { } reserve && reserve != RESERVED_FOR_LP_RAW)
        {
            return false;
        }

        // Permanent buyback-&-burn LP lock (decoded from MigratorParameters.positionRecipient).
        // NoLockResolved is a resolved answer — the auction is known to have no lock, which the preset
        // forbids. A null Lock is "not resolved yet" and stays unasserted.
        if (ReferenceEquals(parameters.Lock, NoLockResolved))
        {
            return false;
        }
        if (parameters.Lock is { } lockDescriptor && !IsStructurallyPermanentLockMode(lockDescriptor.Mode))
        {
            // The structurally permanent modes pass regardless of the caller-derived permanentTimelock
            // flag (their rows carry unlock_block = 0, from which a horizon derivation reports finite).
            if (lockDescriptor.Mode != LOCK_MODE || !lockDescriptor.PermanentTimelock)
            {
                return false;
            }
        }

        // Graduation FDV (USD) — an ADDITIONAL gate on top of the structural checks, never a
        // replacement. Asserted only when the caller supplies a RESOLVED, finite USD number: null and
        // non-finite both mean "the price did not resolve" and leave it unasserted, because demoting an
        // otherwise-legit launch on a price-resolution miss is the worse error. 0 is finite and a real
        // mismatch — it is asserted and rejected, not folded into unresolved.
        if (parameters.GraduationFdvUsd is { } fdv && double.IsFinite(fdv))
        {
            if (!allowedGraduationFdvUsd.Any(allowed => Math.Abs(fdv - allowed) <= allowed * graduationFdvToleranceRatio))
            {
                return false;
            }
        }

        return true;
    }
}
