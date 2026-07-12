namespace UniswapSharp.Flashtestations.Types;

/// <summary>
/// Chain configuration for multi-chain support.
/// Port of the upstream <c>ChainConfig</c> interface (types/index.ts).
/// </summary>
public sealed record ChainConfig
{
    /// <summary>Chain ID.</summary>
    public required int ChainId { get; init; }

    /// <summary>Human readable chain name.</summary>
    public required string Name { get; init; }

    /// <summary>CLI-friendly slug for the <c>--chain</c> argument (e.g., 'unichain-mainnet').</summary>
    public required string Slug { get; init; }

    /// <summary>BlockBuilderPolicy contract address.</summary>
    public required string ContractAddress { get; init; }

    /// <summary>Default RPC URL for this chain.</summary>
    public required string DefaultRpcUrl { get; init; }

    /// <summary>Block explorer base URL.</summary>
    public required string BlockExplorerUrl { get; init; }
}

/// <summary>
/// Minimal configuration options for the JSON-RPC client to interact with the blockchain.
/// Port of the upstream <c>ClientConfig</c> interface (types/index.ts).
/// </summary>
public sealed record ClientConfig
{
    /// <summary>Chain ID to connect to.</summary>
    public required int ChainId { get; init; }

    /// <summary>Optional custom RPC URL (overrides default).</summary>
    public string? RpcUrl { get; init; }
}
