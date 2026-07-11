using System.Numerics;

namespace UniswapSharp.Core;

public static class Constants
{
    public static readonly IReadOnlyList<ChainId> SUPPORTED_CHAINS = new List<ChainId>
    {
        ChainId.MAINNET,
        ChainId.OPTIMISM,
        ChainId.OPTIMISM_GOERLI,
        ChainId.OPTIMISM_SEPOLIA,
        ChainId.ARBITRUM_ONE,
        ChainId.ARBITRUM_GOERLI,
        ChainId.ARBITRUM_SEPOLIA,
        ChainId.POLYGON,
        ChainId.POLYGON_MUMBAI,
        ChainId.GOERLI,
        ChainId.SEPOLIA,
        ChainId.CELO_ALFAJORES,
        ChainId.CELO,
        ChainId.BNB,
        ChainId.AVALANCHE,
        ChainId.BASE,
        ChainId.BASE_GOERLI,
        ChainId.BASE_SEPOLIA,
        ChainId.ZORA,
        ChainId.ZORA_SEPOLIA,
        ChainId.ROOTSTOCK,
        ChainId.BLAST,
        ChainId.ZKSYNC,
        ChainId.WORLDCHAIN,
        ChainId.UNICHAIN_SEPOLIA,
        ChainId.UNICHAIN,
        ChainId.MONAD_TESTNET,
        ChainId.SONEIUM,
        ChainId.MONAD,
        ChainId.XLAYER,
        ChainId.LINEA,
        ChainId.TEMPO,
        ChainId.MEGAETH,
        ChainId.ARC,
        ChainId.ROBINHOOD,
        ChainId.INK,
    };

    public static readonly Dictionary<string, string> NativeCurrencyName =
    new()
    {
        // Strings match input for CLI
        { "ETHER", "ETH" },
        { "MATIC", "MATIC" },
        { "CELO", "CELO" },
        { "GNOSIS", "XDAI" },
        { "MOONBEAM", "GLMR" },
        { "BNB", "BNB" },
        { "AVAX", "AVAX" },
        { "ROOTSTOCK", "RBTC" },
    };

    /// <summary>
    /// Average block time in seconds, per chain. Fractional values are intentional for sub-second
    /// chains. This is block-cadence metadata, not protocol math: upstream keys it on a JS number,
    /// so <see cref="SecondsToBlocks"/> reproduces the same IEEE-double <c>ceil</c> division exactly.
    /// </summary>
    public static readonly IReadOnlyDictionary<ChainId, double> AVERAGE_BLOCK_TIMES_SECONDS = new Dictionary<ChainId, double>
    {
        { ChainId.MAINNET, 12 },
        { ChainId.OPTIMISM, 2 },
        { ChainId.ARBITRUM_ONE, 0.25 },
        { ChainId.POLYGON, 1.75 },
        { ChainId.CELO, 1 },
        { ChainId.BNB, 0.45 }, // post-Maxwell hardfork
        { ChainId.AVALANCHE, 1 },
        { ChainId.BASE, 2 },
        { ChainId.ZORA, 2 },
        { ChainId.BLAST, 2 },
        { ChainId.WORLDCHAIN, 2 },
        { ChainId.UNICHAIN, 1 },
        { ChainId.SONEIUM, 2 },
        { ChainId.MONAD, 0.4 },
        { ChainId.XLAYER, 1 },
        { ChainId.TEMPO, 0.5 },
        { ChainId.MEGAETH, 1 },
        { ChainId.ARC, 0.48 },
        { ChainId.ROBINHOOD, 0.1 },
        { ChainId.INK, 1 },
    };

    /// <summary>
    /// Returns the average block time in seconds for a chain, throwing (rather than falling back
    /// to a mainnet-shaped default) if the chain is not registered.
    /// </summary>
    public static double GetAverageBlockTimeSecs(ChainId chainId)
    {
        if (!AVERAGE_BLOCK_TIMES_SECONDS.TryGetValue(chainId, out double value))
        {
            throw new ArgumentException($"getAverageBlockTimeSecs: unsupported chainId {(int)chainId}; register it in chains.ts before use");
        }
        return value;
    }

    /// <summary>
    /// Converts a wallclock duration in seconds to a block count for the given chain, rounding up
    /// so the resulting window fully covers the requested time.
    /// </summary>
    public static int SecondsToBlocks(int seconds, ChainId chainId)
    {
        return (int)Math.Ceiling(seconds / GetAverageBlockTimeSecs(chainId));
    }



    public static readonly BigInteger MaxUint256 = BigInteger.Parse("115792089237316195423570985008687907853269984665640564039457584007913129639935");


}
