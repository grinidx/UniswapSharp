using System.Numerics;

namespace UniswapSharp.LiquidityLauncher;

/// <summary>
/// Constants for the Liquidity Launcher stack. Ported from
/// sdks/liquidity-launcher-sdk/src/constants.ts.
/// </summary>
public static class Constants
{
    /// <summary>address(0). The native-currency sentinel and the hookless-pool hook.</summary>
    public const string ZERO_ADDRESS = "0x0000000000000000000000000000000000000000";

    /// <summary>2^96 — the fixed-point scale used by the CCA price model (currency-per-token in Q96).</summary>
    public static readonly BigInteger Q96 = BigInteger.Pow(2, 96);

    /// <summary>New tokens launched through the factory are fixed at 18 decimals.</summary>
    public const int NEW_TOKEN_DECIMALS = 18;

    /// <summary>Native currency (ETH) decimals; used when the raise currency is the zero address.</summary>
    public const int NATIVE_CURRENCY_DECIMALS = 18;

    /// <summary>
    /// address(1) — v4 <c>ActionConstants.MSG_SENDER</c>. The CCA factory rewrites this sentinel to
    /// <c>msg.sender</c> (the strategy) when it is set as the auction <c>fundsRecipient</c>.
    /// </summary>
    public const string FUNDS_RECIPIENT_SENTINEL = "0x0000000000000000000000000000000000000001";

    /// <summary>
    /// The canonical CREATE2 deployer (same address on every chain) used to deterministically deploy a
    /// per-launch liquidity-lock recipient. Calldata convention: <c>salt(32 bytes) ++ initCode</c>.
    /// </summary>
    public const string CANONICAL_CREATE2_DEPLOYER = "0x4e59b44847b379578588920cA78FbF26c0B4956C";

    /// <summary>1e7 = 100%, the milli-percent (mps) denominator used across the launcher contracts.</summary>
    public const int MPS_TOTAL = 10_000_000;

    /// <summary>Minimum LP allocation percent for each bracket (single schedule or every tiered tier).</summary>
    public const int MIN_LP_ALLOCATION_PERCENT = 25;

    /// <summary>Matches v4-sdk <c>Pool</c> static fee bound (<c>fee &lt; 1_000_000</c> or dynamic).</summary>
    public const int MAX_LP_FEE = 1_000_000;

    /// <summary>v4 PoolManager bound: tick spacing must be <c>&lt;= type(int16).max</c>.</summary>
    public const int MAX_TICK_SPACING = 32_767;

    /// <summary>Sentinel max percent meaning "+Infinity" (unbounded upper price).</summary>
    public const double UNBOUNDED_PERCENT = 1e9;

    /// <summary>
    /// Default gap (in blocks) between auction end and when migration may begin. The LBP strategy
    /// requires <c>migrationBlock &gt; endBlock</c>, so the minimum is 1.
    /// </summary>
    public static readonly BigInteger DEFAULT_MIGRATION_DELAY_BLOCKS = BigInteger.One;

    /// <summary>
    /// Divisor for the CCA price-tick granularity. v1 default: 1% of the floor price (the canonical
    /// CCA configurator value).
    /// </summary>
    public static readonly BigInteger AUCTION_TICK_DIVISOR = new(100);

    /// <summary>Number of equal-token ramp steps before the single large final block.</summary>
    public const int DEFAULT_AUCTION_STEPS = 12;

    /// <summary>Fraction of supply released in the single large final block (anti-manipulation anchor).</summary>
    public const double DEFAULT_FINAL_BLOCK_PCT = 0.3;

    /// <summary>Convexity exponent alpha for the normalized supply curve C(t) = t^alpha (alpha &gt; 1).</summary>
    public const double DEFAULT_CONVEXITY_ALPHA = 1.2;

    /// <summary>Fallback block time (seconds) for chains without an explicit entry.</summary>
    public const double DEFAULT_BLOCK_TIME_SECONDS = 12;

    /// <summary>
    /// Approximate block time (seconds) per chain, used to convert an auction's start/end times into a
    /// block range. Chains without an entry fall back to <see cref="DEFAULT_BLOCK_TIME_SECONDS"/>.
    /// </summary>
    public static readonly IReadOnlyDictionary<int, double> BLOCK_TIME_SECONDS_BY_CHAIN = new Dictionary<int, double>
    {
        [1] = 12, // mainnet
        [130] = 1, // unichain
        [196] = 1, // xlayer
        [1301] = 1, // unichain sepolia
        [4663] = 0.1, // robinhood (arbitrum orbit) — L2 arbBlockNumber cadence
        [8453] = 2, // base
        [42161] = 0.25, // arbitrum one — L2 arbBlockNumber cadence (NOT the L1 block.number ~12s)
        [43114] = 1, // avalanche
        [84532] = 2, // base sepolia
        [11155111] = 12, // sepolia
    };

    /// <summary>
    /// Re-exported so callers building pool params don't need a direct v4-sdk import. Matches
    /// v4-sdk <c>DYNAMIC_FEE_FLAG</c> (<c>0x800000</c>).
    /// </summary>
    public const int DYNAMIC_FEE_FLAG = UniswapSharp.V4.Entities.Pool.DYNAMIC_FEE_FLAG;
}
