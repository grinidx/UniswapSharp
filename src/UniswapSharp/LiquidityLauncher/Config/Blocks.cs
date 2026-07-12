using System.Numerics;

namespace UniswapSharp.LiquidityLauncher.Config;

public record DeriveBlocksInput(
    BigInteger StartTimeUnix,
    BigInteger EndTimeUnix,
    BigInteger CurrentBlock,
    BigInteger NowUnix,
    double BlockTimeSeconds,
    BigInteger? MigrationDelayBlocks = null);

public record DerivedBlocks(
    BigInteger StartBlock,
    BigInteger EndBlock,
    BigInteger ClaimBlock,
    BigInteger MigrationBlock);

/// <summary>
/// Time → block conversions for an auction. RPC-dependent inputs are passed in so this module stays
/// pure. Ported from sdks/liquidity-launcher-sdk/src/config/blocks.ts.
/// </summary>
public static class Blocks
{
    /// <summary>Block time (seconds) for a chain, falling back to <c>DEFAULT_BLOCK_TIME_SECONDS</c>.</summary>
    public static double GetBlockTimeSeconds(int chainId) =>
        Constants.BLOCK_TIME_SECONDS_BY_CHAIN.TryGetValue(chainId, out var seconds)
            ? seconds
            : Constants.DEFAULT_BLOCK_TIME_SECONDS;

    /// <summary>Converts a future unix timestamp to a block number using the chain's block time.</summary>
    public static BigInteger TimeToBlock(
        BigInteger targetUnix, BigInteger currentBlock, BigInteger nowUnix, double blockTimeSeconds)
    {
        if (blockTimeSeconds <= 0)
        {
            throw new LauncherSdkError(LauncherErrorCode.INVALID_TIME, "Invalid block time for chain");
        }
        BigInteger deltaSeconds = targetUnix - nowUnix;
        var deltaBlocks = new BigInteger(MathJs.Round((double)deltaSeconds / blockTimeSeconds));
        BigInteger block = currentBlock + deltaBlocks;
        // A target in the past yields negative deltaBlocks; reject rather than silently snapping to now.
        if (block < currentBlock)
        {
            throw new LauncherSdkError(LauncherErrorCode.INVALID_TIME, "Auction time cannot be in the past");
        }
        return block;
    }

    /// <summary>Derives the auction's start / end / claim / migration blocks from its start and end times.</summary>
    public static DerivedBlocks DeriveBlocks(DeriveBlocksInput input)
    {
        if (input.EndTimeUnix <= input.StartTimeUnix)
        {
            throw new LauncherSdkError(
                LauncherErrorCode.INVALID_AUCTION_WINDOW, "Auction end time must be after start time");
        }
        BigInteger startBlock = TimeToBlock(input.StartTimeUnix, input.CurrentBlock, input.NowUnix, input.BlockTimeSeconds);
        BigInteger endBlock = TimeToBlock(input.EndTimeUnix, input.CurrentBlock, input.NowUnix, input.BlockTimeSeconds);
        if (endBlock <= startBlock)
        {
            throw new LauncherSdkError(
                LauncherErrorCode.INVALID_AUCTION_WINDOW, "Auction window is too short for the chain block time");
        }
        // CCA requires claimBlock >= endBlock; the strategy requires migrationBlock > endBlock.
        BigInteger claimBlock = endBlock;
        BigInteger migrationBlock = endBlock + (input.MigrationDelayBlocks ?? Constants.DEFAULT_MIGRATION_DELAY_BLOCKS);
        return new DerivedBlocks(startBlock, endBlock, claimBlock, migrationBlock);
    }
}
