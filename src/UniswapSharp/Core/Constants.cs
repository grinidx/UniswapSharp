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
        ChainId.CELO,
        ChainId.BNB,
        ChainId.AVALANCHE,
        ChainId.BASE,
        ChainId.BASE_GOERLI,
        ChainId.ZORA,
        ChainId.ZORA_SEPOLIA,
        ChainId.ROOTSTOCK,
        ChainId.BLAST,
        ChainId.ZKSYNC
    };

    public static readonly Dictionary<string, string> NativeCurrencyName =
    new()
    {
        // Strings match input for CLI
        { "ETHER", "ETH" },
        { "MATIC", "MATIC" },
        { "CELO", "CELO" },
        { "GNOSIS ", "XDAI" },
        { "MOONBEAM", "GLMR" },
        { "BNB", "BNB" },
        { "AVAX", "AVAX" },
        { "ROOTSTOCK ", "RBTC" },
    };



    public static readonly BigInteger MaxUint256 = BigInteger.Parse("115792089237316195423570985008687907853269984665640564039457584007913129639935");


}
