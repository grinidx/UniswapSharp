using System.Numerics;

namespace UniswapSharp.Flashtestations.Rpc;

/// <summary>
/// Selector identifying a block for <see cref="IEvmRpcClient.GetBlockAsync"/>.
/// Port of the viem <c>getBlock</c> argument union
/// (<c>{ blockTag } | { blockNumber } | { blockHash }</c>).
/// Exactly one property is populated.
/// </summary>
public sealed record BlockSelector
{
    /// <summary>Block tag: 'earliest' | 'latest' | 'safe' | 'finalized' | 'pending'.</summary>
    public string? BlockTag { get; init; }

    /// <summary>Block number.</summary>
    public BigInteger? BlockNumber { get; init; }

    /// <summary>Block hash (32-byte hex, 66 chars including '0x').</summary>
    public string? BlockHash { get; init; }
}

/// <summary>
/// Minimal block shape used by the SDK (mirrors the fields of a viem <c>Block</c> that
/// flashtestations reads).
/// </summary>
public sealed record EvmBlock
{
    /// <summary>Block number.</summary>
    public BigInteger Number { get; init; }

    /// <summary>Block hash.</summary>
    public string? Hash { get; init; }

    /// <summary>Ordered list of transaction hashes in the block.</summary>
    public IReadOnlyList<string>? Transactions { get; init; }
}

/// <summary>
/// Minimal transaction-log shape (mirrors a viem <c>Log</c>) passed to the event-log parser.
/// </summary>
public sealed record EvmLog
{
    /// <summary>Address that emitted the log.</summary>
    public string? Address { get; init; }

    /// <summary>Indexed log topics (topic0 is the event signature hash).</summary>
    public IReadOnlyList<string> Topics { get; init; } = Array.Empty<string>();

    /// <summary>Non-indexed log data (hex).</summary>
    public string? Data { get; init; }
}

/// <summary>
/// Minimal transaction-receipt shape used by the SDK (mirrors a viem <c>TransactionReceipt</c>).
/// </summary>
public sealed record EvmTransactionReceipt
{
    /// <summary>Transaction hash.</summary>
    public string? TransactionHash { get; init; }

    /// <summary>Block number containing the transaction.</summary>
    public BigInteger? BlockNumber { get; init; }

    /// <summary>Destination address (null for contract-creation transactions).</summary>
    public string? To { get; init; }

    /// <summary>Receipt status ('success' / 'reverted').</summary>
    public string? Status { get; init; }

    /// <summary>Logs emitted by the transaction.</summary>
    public IReadOnlyList<EvmLog> Logs { get; init; } = Array.Empty<EvmLog>();
}

/// <summary>
/// Request for reading a contract view function.
/// Port of the viem <c>readContract</c> argument object (the ABI itself is implicit).
/// </summary>
public sealed record ReadContractRequest
{
    /// <summary>Contract address.</summary>
    public required string Address { get; init; }

    /// <summary>Name of the function to call.</summary>
    public required string FunctionName { get; init; }

    /// <summary>Function arguments.</summary>
    public required IReadOnlyList<string> Args { get; init; }
}

/// <summary>
/// Decoded <c>getWorkloadMetadata</c> result (the tuple returned by the BlockBuilderPolicy contract).
/// </summary>
public sealed record ContractWorkloadMetadata
{
    /// <summary>Commit hash of the workload source code.</summary>
    public required string CommitHash { get; init; }

    /// <summary>Source locators (e.g., GitHub URLs) for the workload source code.</summary>
    public required IReadOnlyList<string> SourceLocators { get; init; }
}

/// <summary>
/// Request for parsing event logs.
/// Port of the viem <c>parseEventLogs</c> argument object (the ABI is implicit).
/// </summary>
public sealed record ParseEventLogsRequest
{
    /// <summary>Name of the event to extract.</summary>
    public required string EventName { get; init; }

    /// <summary>Logs to parse.</summary>
    public required IReadOnlyList<EvmLog> Logs { get; init; }
}

/// <summary>
/// Decoded arguments of a <c>BlockBuilderProofVerified</c> event.
/// </summary>
public sealed record BlockBuilderProofVerifiedArgs
{
    /// <summary>Address of the block builder.</summary>
    public required string Caller { get; init; }

    /// <summary>Workload ID (bytes32 hex).</summary>
    public required string WorkloadId { get; init; }

    /// <summary>Protocol version.</summary>
    public required int Version { get; init; }

    /// <summary>Block content hash (bytes32 hex).</summary>
    public required string BlockContentHash { get; init; }

    /// <summary>Commit hash string.</summary>
    public required string CommitHash { get; init; }
}

/// <summary>
/// A parsed <c>BlockBuilderProofVerified</c> log (mirrors a viem parsed-log entry with <c>args</c>).
/// </summary>
public sealed record ParsedFlashtestationLog
{
    /// <summary>Decoded event arguments.</summary>
    public required BlockBuilderProofVerifiedArgs Args { get; init; }
}

/// <summary>
/// Injectable abstraction over the low-level EVM JSON-RPC operations the SDK needs
/// (block / receipt fetching, contract reads, event-log parsing).
///
/// This replaces the viem <c>PublicClient</c> (<c>getBlock</c> / <c>getTransactionReceipt</c> /
/// <c>readContract</c>) plus the standalone <c>parseEventLogs</c> utility, so tests can supply a
/// fake without any network access. A default Nethereum-backed implementation is provided but is
/// not exercised by tests (live verification is deferred).
/// </summary>
public interface IEvmRpcClient
{
    /// <summary>Fetch a block. Returns <c>null</c> when the block is absent.</summary>
    Task<EvmBlock?> GetBlockAsync(BlockSelector selector);

    /// <summary>Fetch a transaction receipt. Returns <c>null</c> when absent.</summary>
    Task<EvmTransactionReceipt?> GetTransactionReceiptAsync(string hash);

    /// <summary>Read the <c>getWorkloadMetadata</c> contract view function.</summary>
    Task<ContractWorkloadMetadata> ReadContractAsync(ReadContractRequest request);

    /// <summary>Parse <c>BlockBuilderProofVerified</c> logs from a receipt's logs.</summary>
    IReadOnlyList<ParsedFlashtestationLog> ParseEventLogs(ParseEventLogsRequest request);
}

/// <summary>
/// Factory that creates an <see cref="IEvmRpcClient"/> for a given chain / RPC URL.
/// Replaces viem's <c>createPublicClient</c> so the client-cache behaviour of
/// <see cref="RpcClient"/> can be observed (and faked) in tests.
/// </summary>
public interface IEvmRpcClientFactory
{
    /// <summary>Create an EVM RPC client for the given chain ID and RPC URL.</summary>
    IEvmRpcClient Create(int chainId, string rpcUrl);
}
