namespace UniswapSharp.Flashtestations.Types;

/// <summary>
/// Raised when an RPC / network interaction fails after exhausting retries.
/// Port of the upstream <c>NetworkError</c> class (types/index.ts).
/// </summary>
public sealed class NetworkError : Exception
{
    public NetworkError(string message, Exception? cause = null)
        : base(message, cause)
    {
    }
}

/// <summary>
/// Raised when a requested block cannot be found.
/// Port of the upstream <c>BlockNotFoundError</c> class (types/index.ts).
/// </summary>
public sealed class BlockNotFoundError : Exception
{
    public BlockNotFoundError(BlockParameter blockParameter)
        : base($"Block not found: {blockParameter}")
    {
        BlockParameter = blockParameter;
    }

    public BlockParameter BlockParameter { get; }
}

/// <summary>
/// Raised when measurement registers or other user input fails validation.
/// Port of the upstream <c>ValidationError</c> class (types/validation.ts / index.ts).
/// </summary>
public sealed class ValidationError : Exception
{
    public ValidationError(string message, string? field = null)
        : base(message)
    {
        Field = field;
    }

    public string? Field { get; }
}

/// <summary>
/// Raised when a chain ID is not present in the supported-chain configuration.
/// Port of the upstream <c>ChainNotSupportedError</c> class (types/index.ts).
/// </summary>
public sealed class ChainNotSupportedError : Exception
{
    public ChainNotSupportedError(int chainId, IReadOnlyList<int> supportedChains)
        : base($"Chain {chainId} not supported. Supported chains: {string.Join(", ", supportedChains)}")
    {
        ChainId = chainId;
        SupportedChains = supportedChains;
    }

    public int ChainId { get; }

    public IReadOnlyList<int> SupportedChains { get; }
}
