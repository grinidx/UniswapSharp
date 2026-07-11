using UniswapSharp.Core;

namespace UniswapSharp.Testing.Core;

// Ported 1:1 from sdks/sdk-core/src/chains.test.ts
public class ChainsTests
{
    [Fact]
    public void GetAverageBlockTimeSecs_ReturnsRegisteredValue()
    {
        Assert.Equal(12, Constants.GetAverageBlockTimeSecs(ChainId.MAINNET));
        Assert.Equal(0.25, Constants.GetAverageBlockTimeSecs(ChainId.ARBITRUM_ONE));
        Assert.Equal(0.1, Constants.GetAverageBlockTimeSecs(ChainId.ROBINHOOD));
        Assert.Equal(1, Constants.GetAverageBlockTimeSecs(ChainId.MEGAETH));
        Assert.Equal(0.48, Constants.GetAverageBlockTimeSecs(ChainId.ARC));
        Assert.Equal(1, Constants.GetAverageBlockTimeSecs(ChainId.INK));
    }

    [Fact]
    public void GetAverageBlockTimeSecs_ThrowsOnUnregisteredChain()
    {
        var ex = Assert.Throws<ArgumentException>(() => Constants.GetAverageBlockTimeSecs((ChainId)99999));
        Assert.Contains("unsupported chainId 99999", ex.Message);
    }

    [Fact]
    public void SecondsToBlocks_ConvertsUsingCeil()
    {
        Assert.Equal(1, Constants.SecondsToBlocks(8, ChainId.MAINNET));       // ceil(8/12)
        Assert.Equal(32, Constants.SecondsToBlocks(8, ChainId.ARBITRUM_ONE)); // ceil(8/0.25)
        Assert.Equal(16, Constants.SecondsToBlocks(8, ChainId.TEMPO));        // ceil(8/0.5)
        Assert.Equal(8, Constants.SecondsToBlocks(8, ChainId.MEGAETH));       // ceil(8/1)
        Assert.Equal(17, Constants.SecondsToBlocks(8, ChainId.ARC));          // ceil(8/0.48)
        Assert.Equal(80, Constants.SecondsToBlocks(8, ChainId.ROBINHOOD));    // ceil(8/0.1)
        Assert.Equal(8, Constants.SecondsToBlocks(8, ChainId.INK));           // ceil(8/1)
        Assert.Equal(1, Constants.SecondsToBlocks(1, ChainId.MAINNET));       // ceil(1/12)
    }

    [Fact]
    public void SecondsToBlocks_PropagatesThrowOnUnregisteredChain()
    {
        var ex = Assert.Throws<ArgumentException>(() => Constants.SecondsToBlocks(10, (ChainId)99999));
        Assert.Contains("unsupported chainId 99999", ex.Message);
    }
}
