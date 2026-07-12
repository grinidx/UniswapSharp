using UniswapSharp.Flashtestations.Types;

namespace UniswapSharp.Flashtestations.Config;

/// <summary>
/// Chain configuration for supported chains, plus lookup helpers.
/// Port of upstream <c>config/chains.ts</c>.
/// </summary>
public static class Chains
{
    // Mirrors upstream `process.env.RPC_URL || <default>`. Evaluated once, matching the
    // module-level constant. An unset or empty RPC_URL falls through to the default.
    private static string RpcUrlOrDefault(string @default)
    {
        var envUrl = Environment.GetEnvironmentVariable("RPC_URL");
        return string.IsNullOrEmpty(envUrl) ? @default : envUrl;
    }

    /// <summary>
    /// Chain configuration for supported chains, keyed by chain ID.
    /// Port of upstream <c>CHAIN_CONFIGS</c>.
    /// </summary>
    public static IReadOnlyDictionary<int, ChainConfig> ChainConfigs { get; } = new Dictionary<int, ChainConfig>
    {
        // Unichain Mainnet
        [130] = new ChainConfig
        {
            ChainId = 130,
            Name = "Unichain Mainnet",
            Slug = "unichain-mainnet",
            ContractAddress = "0xd44f9d1331659F417a3E22C9e29529D498B66A29",
            DefaultRpcUrl = RpcUrlOrDefault("https://mainnet.unichain.org"),
            BlockExplorerUrl = "https://uniscan.xyz",
        },

        // Unichain Sepolia (Testnet)
        [1301] = new ChainConfig
        {
            ChainId = 1301,
            Name = "Unichain Sepolia",
            Slug = "unichain-sepolia",
            ContractAddress = "0x3b03b3caabd49ca12de9eba46a6a2950700b1db4",
            DefaultRpcUrl = RpcUrlOrDefault("https://sepolia.unichain.org"),
            BlockExplorerUrl = "https://sepolia.uniscan.xyz",
        },

        // Unichain Alphanet (Testnet)
        // Note: no default RPC URL for alphanet because Unichain doesn't want to expose it publicly.
        [22444422] = new ChainConfig
        {
            ChainId = 22444422,
            Name = "Unichain Alphanet",
            Slug = "unichain-alphanet",
            ContractAddress = "0x8d0e3f57052f33CEF1e6BE98B65aad1794dc95a5",
            DefaultRpcUrl = RpcUrlOrDefault(string.Empty),
            BlockExplorerUrl = string.Empty,
        },

        // Unichain Experimental (Testnet)
        // Note: no default RPC URL for experimental because Unichain doesn't want to expose it publicly.
        [420120005] = new ChainConfig
        {
            ChainId = 420120005,
            Name = "Unichain Experimental",
            Slug = "unichain-experimental",
            ContractAddress = "0x80dcdE10eE31E0A32B8944b39e8AE21d47984b4e",
            DefaultRpcUrl = RpcUrlOrDefault(string.Empty),
            BlockExplorerUrl = string.Empty,
        },
    };

    /// <summary>
    /// Gets the BlockBuilderPolicy contract address for a chain ID.
    /// Port of upstream <c>getContractAddress</c>.
    /// </summary>
    /// <exception cref="ChainNotSupportedError">If the chain is not supported.</exception>
    public static string GetContractAddress(int chainId)
    {
        if (!ChainConfigs.TryGetValue(chainId, out var config))
        {
            throw new ChainNotSupportedError(chainId, GetSupportedChains());
        }

        return config.ContractAddress;
    }

    /// <summary>
    /// Gets the RPC URL for a chain ID.
    /// Port of upstream <c>getRpcUrl</c>.
    /// </summary>
    /// <exception cref="ChainNotSupportedError">If the chain is not supported.</exception>
    public static string GetRpcUrl(int chainId)
    {
        if (!ChainConfigs.TryGetValue(chainId, out var config))
        {
            throw new ChainNotSupportedError(chainId, GetSupportedChains());
        }

        return config.DefaultRpcUrl;
    }

    /// <summary>
    /// Gets the block explorer base URL for a chain ID.
    /// Port of upstream <c>getBlockExplorerUrl</c>.
    /// </summary>
    /// <exception cref="ChainNotSupportedError">If the chain is not supported.</exception>
    public static string GetBlockExplorerUrl(int chainId)
    {
        if (!ChainConfigs.TryGetValue(chainId, out var config))
        {
            throw new ChainNotSupportedError(chainId, GetSupportedChains());
        }

        return config.BlockExplorerUrl;
    }

    /// <summary>
    /// Gets the complete chain configuration for a chain ID.
    /// Port of upstream <c>getChainConfig</c>.
    /// </summary>
    /// <exception cref="ChainNotSupportedError">If the chain is not supported.</exception>
    public static ChainConfig GetChainConfig(int chainId)
    {
        if (!ChainConfigs.TryGetValue(chainId, out var config))
        {
            throw new ChainNotSupportedError(chainId, GetSupportedChains());
        }

        return config;
    }

    /// <summary>
    /// Gets the list of all supported chain IDs.
    /// Port of upstream <c>getSupportedChains</c>.
    /// </summary>
    public static IReadOnlyList<int> GetSupportedChains() => ChainConfigs.Keys.ToList();

    /// <summary>
    /// Checks whether a chain ID is supported.
    /// Port of upstream <c>isChainSupported</c>.
    /// </summary>
    public static bool IsChainSupported(int chainId) => ChainConfigs.ContainsKey(chainId);

    /// <summary>
    /// Gets a chain configuration by slug, or <c>null</c> if not found.
    /// Port of upstream <c>getChainBySlug</c>.
    /// </summary>
    public static ChainConfig? GetChainBySlug(string slug) =>
        ChainConfigs.Values.FirstOrDefault(config => config.Slug == slug);

    /// <summary>
    /// Gets the list of all supported chain slugs.
    /// Port of upstream <c>getSupportedChainSlugs</c>.
    /// </summary>
    public static IReadOnlyList<string> GetSupportedChainSlugs() =>
        ChainConfigs.Values.Select(config => config.Slug).ToList();

    /// <summary>
    /// Checks whether a chain slug is valid.
    /// Port of upstream <c>isValidChainSlug</c>.
    /// </summary>
    public static bool IsValidChainSlug(string slug) =>
        ChainConfigs.Values.Any(config => config.Slug == slug);

    /// <summary>
    /// Gets the default chain slug (unichain-mainnet).
    /// Port of upstream <c>getDefaultChainSlug</c>.
    /// </summary>
    public static string GetDefaultChainSlug() => "unichain-mainnet";
}
