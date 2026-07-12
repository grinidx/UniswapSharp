using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;

namespace UniswapSharp.Flashtestations.Rpc;

/// <summary>
/// Default <see cref="IEvmRpcClient"/> implementation backed by <see cref="Nethereum.Web3.Web3"/>.
///
/// This wraps a live JSON-RPC endpoint. It is provided so the SDK works end-to-end against a real
/// node, but it is <b>not exercised by the test suite</b> — live verification is deferred, and all
/// unit tests inject a fake <see cref="IEvmRpcClient"/> instead. Construction is lazy (no network
/// I/O), so this type never touches the network unless one of its methods is awaited.
/// </summary>
public sealed class NethereumEvmRpcClient : IEvmRpcClient
{
    private readonly Web3 _web3;

    public NethereumEvmRpcClient(string rpcUrl)
    {
        _web3 = new Web3(rpcUrl);
    }

    /// <inheritdoc />
    public async Task<EvmBlock?> GetBlockAsync(BlockSelector selector)
    {
        BlockWithTransactionHashes? block;

        if (selector.BlockHash is not null)
        {
            block = await _web3.Eth.Blocks.GetBlockWithTransactionsHashesByHash
                .SendRequestAsync(selector.BlockHash);
        }
        else
        {
            var blockParameter = ToNethereumBlockParameter(selector);
            block = await _web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber
                .SendRequestAsync(blockParameter);
        }

        if (block is null)
        {
            return null;
        }

        return new EvmBlock
        {
            Number = block.Number?.Value ?? BigInteger.Zero,
            Hash = block.BlockHash,
            Transactions = block.TransactionHashes ?? Array.Empty<string>(),
        };
    }

    /// <inheritdoc />
    public async Task<EvmTransactionReceipt?> GetTransactionReceiptAsync(string hash)
    {
        var receipt = await _web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(hash);
        if (receipt is null)
        {
            return null;
        }

        var logs = receipt.DecodeAllEvents<BlockBuilderProofVerifiedEventDTO>()
            .Select(evt => new EvmLog
            {
                Address = evt.Log.Address,
                Topics = evt.Log.Topics?.Select(t => t?.ToString() ?? string.Empty).ToArray() ?? Array.Empty<string>(),
                Data = evt.Log.Data,
            })
            .ToArray();

        return new EvmTransactionReceipt
        {
            TransactionHash = receipt.TransactionHash,
            BlockNumber = receipt.BlockNumber?.Value,
            To = receipt.To,
            Status = receipt.Status?.Value == 1 ? "success" : "reverted",
            Logs = logs,
        };
    }

    /// <inheritdoc />
    public async Task<ContractWorkloadMetadata> ReadContractAsync(ReadContractRequest request)
    {
        var handler = _web3.Eth.GetContractQueryHandler<GetWorkloadMetadataFunction>();
        var function = new GetWorkloadMetadataFunction
        {
            WorkloadId = request.Args.Count > 0 ? request.Args[0] : string.Empty,
        };

        var result = await handler.QueryDeserializingToObjectAsync<WorkloadMetadataOutputDto>(
            function,
            request.Address);

        return new ContractWorkloadMetadata
        {
            CommitHash = result.CommitHash ?? string.Empty,
            SourceLocators = result.SourceLocators ?? new List<string>(),
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<ParsedFlashtestationLog> ParseEventLogs(ParseEventLogsRequest request)
    {
        var filterLogs = request.Logs
            .Select(log => new FilterLog
            {
                Address = log.Address,
                Data = log.Data,
                Topics = log.Topics.Cast<object>().ToArray(),
            })
            .ToArray();

        return filterLogs
            .DecodeAllEvents<BlockBuilderProofVerifiedEventDTO>()
            .Select(evt => new ParsedFlashtestationLog
            {
                Args = new BlockBuilderProofVerifiedArgs
                {
                    Caller = evt.Event.Caller ?? string.Empty,
                    WorkloadId = evt.Event.WorkloadId ?? string.Empty,
                    Version = evt.Event.Version,
                    BlockContentHash = evt.Event.BlockContentHash ?? string.Empty,
                    CommitHash = evt.Event.CommitHash ?? string.Empty,
                },
            })
            .ToArray();
    }

    private static BlockParameter ToNethereumBlockParameter(BlockSelector selector)
    {
        if (selector.BlockNumber is { } number)
        {
            return new BlockParameter(new HexBigInteger(number));
        }

        return selector.BlockTag switch
        {
            "earliest" => BlockParameter.CreateEarliest(),
            "pending" => BlockParameter.CreatePending(),
            // Nethereum 6.1.0 has no dedicated 'safe' / 'finalized' factory; fall back to 'latest'.
            // (This live path is deferred and not exercised by tests.)
            _ => BlockParameter.CreateLatest(),
        };
    }
}

/// <summary>
/// Default <see cref="IEvmRpcClientFactory"/> backed by <see cref="NethereumEvmRpcClient"/>.
/// </summary>
public sealed class NethereumEvmRpcClientFactory : IEvmRpcClientFactory
{
    /// <inheritdoc />
    public IEvmRpcClient Create(int chainId, string rpcUrl) => new NethereumEvmRpcClient(rpcUrl);
}

/// <summary>
/// Nethereum event DTO for <c>BlockBuilderProofVerified</c> (all parameters non-indexed).
/// </summary>
[Event("BlockBuilderProofVerified")]
public sealed class BlockBuilderProofVerifiedEventDTO : IEventDTO
{
    [Parameter("address", "caller", 1, false)]
    public string? Caller { get; set; }

    [Parameter("bytes32", "workloadId", 2, false)]
    public string? WorkloadId { get; set; }

    [Parameter("uint8", "version", 3, false)]
    public int Version { get; set; }

    [Parameter("bytes32", "blockContentHash", 4, false)]
    public string? BlockContentHash { get; set; }

    [Parameter("string", "commitHash", 5, false)]
    public string? CommitHash { get; set; }
}

/// <summary>
/// Nethereum function message for the <c>getWorkloadMetadata</c> view function.
/// </summary>
[Function("getWorkloadMetadata", typeof(WorkloadMetadataOutputDto))]
public sealed class GetWorkloadMetadataFunction : FunctionMessage
{
    [Parameter("bytes32", "workloadId", 1)]
    public string WorkloadId { get; set; } = string.Empty;
}

/// <summary>
/// Nethereum output DTO for the <c>getWorkloadMetadata</c> tuple return value.
/// </summary>
[FunctionOutput]
public sealed class WorkloadMetadataOutputDto : IFunctionOutputDTO
{
    [Parameter("string", "commitHash", 1)]
    public string? CommitHash { get; set; }

    [Parameter("string[]", "sourceLocators", 2)]
    public List<string>? SourceLocators { get; set; }
}
