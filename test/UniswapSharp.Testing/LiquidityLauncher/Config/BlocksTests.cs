using UniswapSharp.LiquidityLauncher;
using UniswapSharp.LiquidityLauncher.Config;

namespace UniswapSharp.Testing.LiquidityLauncher.Config;

// Ported from sdks/liquidity-launcher-sdk/src/config/blocks.test.ts.
public class BlocksTests
{
    // ---- getBlockTimeSeconds ----

    [Fact]
    public void GetBlockTimeSeconds_UsesSubSecondL2CadenceForArbitrumFamilyChains()
    {
        Assert.Equal(0.25, Blocks.GetBlockTimeSeconds((int)SupportedChainId.ARBITRUM_ONE));
        Assert.Equal(0.1, Blocks.GetBlockTimeSeconds((int)SupportedChainId.ROBINHOOD));
        Assert.True(Blocks.GetBlockTimeSeconds((int)SupportedChainId.ARBITRUM_ONE) < Constants.DEFAULT_BLOCK_TIME_SECONDS);
        Assert.True(Blocks.GetBlockTimeSeconds((int)SupportedChainId.ROBINHOOD) < Constants.DEFAULT_BLOCK_TIME_SECONDS);
    }

    [Fact]
    public void GetBlockTimeSeconds_KeepsExpectedCadenceForOtherChainsAndDefaultsUnknown()
    {
        Assert.Equal(12, Blocks.GetBlockTimeSeconds((int)SupportedChainId.MAINNET));
        Assert.Equal(2, Blocks.GetBlockTimeSeconds((int)SupportedChainId.BASE));
        Assert.Equal(1, Blocks.GetBlockTimeSeconds((int)SupportedChainId.AVALANCHE));
        Assert.Equal(1, Blocks.GetBlockTimeSeconds((int)SupportedChainId.XLAYER));
        Assert.Equal(Constants.DEFAULT_BLOCK_TIME_SECONDS, Blocks.GetBlockTimeSeconds(999_999));
    }

    // ---- deriveBlocks — real-time auction window is honored ----

    private const long Now = 1_000_000;
    private const long CurrentBlock = 5_000_000;
    private const long Start = Now + 3600; // +1h
    private const long End = Start + 50_400; // 14h window

    [Fact]
    public void DeriveBlocks_SpansA14hAuctionAsBlocksOnRobinhood()
    {
        var derived = Blocks.DeriveBlocks(new DeriveBlocksInput(
            StartTimeUnix: Start,
            EndTimeUnix: End,
            CurrentBlock: CurrentBlock,
            NowUnix: Now,
            BlockTimeSeconds: Blocks.GetBlockTimeSeconds((int)SupportedChainId.ROBINHOOD)));
        // 50400s / 0.1s = 504000 blocks; the 12s default would yield only 4200 (~7min of real time).
        Assert.Equal(504_000, derived.EndBlock - derived.StartBlock);
    }

    [Fact]
    public void DeriveBlocks_SpansA14hAuctionCorrectlyOnArbitrumOne()
    {
        var derived = Blocks.DeriveBlocks(new DeriveBlocksInput(
            StartTimeUnix: Start,
            EndTimeUnix: End,
            CurrentBlock: CurrentBlock,
            NowUnix: Now,
            BlockTimeSeconds: Blocks.GetBlockTimeSeconds((int)SupportedChainId.ARBITRUM_ONE)));
        // 50400s / 0.25s = 201600 blocks; the 12s default would yield only 4200 (~17min of real time).
        Assert.Equal(201_600, derived.EndBlock - derived.StartBlock);
    }
}
