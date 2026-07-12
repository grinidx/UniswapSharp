namespace UniswapSharp.LiquidityLauncher;

/// <summary>
/// Chains where the Liquidity Launcher stack is deployed. Values are numeric chain ids so the SDK
/// stays framework-agnostic. Ported from sdks/liquidity-launcher-sdk/src/chains.ts.
/// </summary>
public enum SupportedChainId
{
    MAINNET = 1,
    UNICHAIN = 130,
    BASE = 8453,
    ARBITRUM_ONE = 42161,
    AVALANCHE = 43114,
    XLAYER = 196,
    ROBINHOOD = 4663,
    SEPOLIA = 11155111,
    BASE_SEPOLIA = 84532,
}

/// <summary>Chain-support helpers for the launcher stack.</summary>
public static class Chains
{
    private static readonly HashSet<int> SupportedChainIds =
        Enum.GetValues<SupportedChainId>().Select(v => (int)v).ToHashSet();

    /// <summary>Whether the launcher stack is deployed on <paramref name="chainId"/>.</summary>
    public static bool IsLaunchSupportedChain(int chainId) => SupportedChainIds.Contains(chainId);
}
