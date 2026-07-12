using System.Numerics;

namespace UniswapSharp.LiquidityLauncher;

/// <summary>
/// C# mirrors of the on-chain structs the launch flow encodes. Field order and types match the
/// deployed contracts exactly. Ported from sdks/liquidity-launcher-sdk/src/types.ts.
/// </summary>
public record PoolParameters(
    // uint24, hundredths of a bip; DYNAMIC_FEE_FLAG for a dynamic-fee pool.
    int Fee,
    // int24.
    int TickSpacing,
    // address(0) for a hookless pool.
    string Hook);

public record MigratorParameters(
    string Token,
    // address(0) for native ETH.
    string Currency,
    // uint64.
    BigInteger MigrationBlock,
    // uint128.
    BigInteger ReservedTokenAmountForLP,
    string Recipient,
    string PositionRecipient,
    PoolParameters PoolParameters,
    // abi.encode(PositionDefinition[]) — use Encode.EncodePositionDefinitions.
    string PositionDefinitions,
    // abi.encode(LiquidityAllocationBracket[]) — use Encode.EncodeLpAllocationSchedule.
    string LpAllocationSchedule);

public record AuctionParameters(
    string Currency,
    string TokensRecipient,
    string FundsRecipient,
    // uint64.
    BigInteger StartBlock,
    // uint64.
    BigInteger EndBlock,
    // uint64.
    BigInteger ClaimBlock,
    // uint256 — CCA price-tick granularity (distinct from pool tickSpacing).
    BigInteger TickSpacing,
    string ValidationHook,
    // uint256 — currency-per-token in Q96.
    BigInteger FloorPrice,
    // uint128.
    BigInteger RequiredCurrencyRaised,
    // Packed emission schedule — use Encode.EncodeAuctionSteps.
    string AuctionStepsData);

public record PositionDefinition(
    // int24 tick offset from the final auction tick.
    int OffsetLower,
    // int24.
    int OffsetUpper,
    // uint24 mps (1e7 = 100%).
    int Weight,
    // address(0) defers to MigratorParameters.PositionRecipient.
    string OverridePositionRecipient);

public record LiquidityAllocationBracket(
    // uint128 — inclusive lower bound on cumulative currency raised (first bracket = 0).
    BigInteger LowerThreshold,
    // uint24 mps.
    int Rate);

public record Uerc20Metadata(
    string Description,
    string Website,
    string Image,
    // JSON envelope carrying optional X-verification data ('0x' when absent).
    string ExtraData);

/// <summary>One emission step. The packed form encodes <c>mps</c> and <c>endBlock - startBlock</c>.</summary>
public record AuctionStepInput(
    // uint24 tokens-per-block as mps of auction supply (1e7 = 100%).
    int Mps,
    BigInteger StartBlock,
    BigInteger EndBlock);

/// <summary>A single <c>distributeToken</c> distribution.</summary>
public record Distribution(
    string Strategy,
    // uint128.
    BigInteger Amount,
    string ConfigData);

/// <summary>One TokenSplitter recipient.</summary>
public record Split(
    string Recipient,
    // uint256.
    BigInteger Amount);
