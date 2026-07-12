using System.Collections.Concurrent;
using System.Globalization;
using System.Numerics;
using UniswapSharp.Flashtestations.Config;
using UniswapSharp.Flashtestations.Types;

namespace UniswapSharp.Flashtestations.Rpc;

/// <summary>
/// Configuration options for the RPC client.
/// Port of upstream <c>RpcClientConfig</c> (rpc/client.ts).
/// </summary>
public sealed record RpcClientConfig
{
    /// <summary>Chain ID to connect to.</summary>
    public required int ChainId { get; init; }

    /// <summary>Optional custom RPC URL (overrides default).</summary>
    public string? RpcUrl { get; init; }

    /// <summary>Number of retry attempts for failed requests (default: 3).</summary>
    public int? MaxRetries { get; init; }

    /// <summary>Initial retry delay in milliseconds (default: 1000).</summary>
    public int? InitialRetryDelay { get; init; }
}

/// <summary>
/// Higher-level RPC client surface used by the verification service.
/// Implemented by <see cref="RpcClient"/>; abstracted so the service can be tested with a fake.
/// </summary>
public interface IRpcClient
{
    /// <summary>Fetch the flashtestation event from the given block (defaults to 'latest').</summary>
    Task<FlashtestationEvent?> GetFlashtestationEventAsync(BlockParameter blockParameter);

    /// <summary>Fetch a block by block parameter.</summary>
    Task<EvmBlock> GetBlockAsync(BlockParameter blockParameter);

    /// <summary>Fetch a transaction receipt by transaction hash.</summary>
    Task<EvmTransactionReceipt> GetTransactionReceiptAsync(string txHash);

    /// <summary>Fetch source locators for a workload ID from the BlockBuilderPolicy contract.</summary>
    Task<IReadOnlyList<string>> GetSourceLocatorsAsync(string workloadId);

    /// <summary>Get the underlying EVM RPC client.</summary>
    IEvmRpcClient GetClient();
}

/// <summary>
/// Factory that creates an <see cref="IRpcClient"/> from configuration.
/// Abstracts the <c>new RpcClient(config)</c> construction so the service can inject a fake.
/// </summary>
public interface IRpcClientFactory
{
    /// <summary>Create an <see cref="IRpcClient"/> for the given configuration.</summary>
    IRpcClient Create(RpcClientConfig config);
}

/// <summary>
/// RPC client for blockchain interactions with retry logic and connection reuse.
/// Port of upstream <c>RpcClient</c> (rpc/client.ts).
/// </summary>
public sealed class RpcClient : IRpcClient
{
    /// <summary>Default EVM client factory (Nethereum-backed). Not exercised by tests.</summary>
    private static readonly IEvmRpcClientFactory DefaultEvmFactory = new NethereumEvmRpcClientFactory();

    /// <summary>Cache of EVM clients keyed by "chainId:rpcUrl" (mirrors upstream module-level cache).</summary>
    private static readonly ConcurrentDictionary<string, IEvmRpcClient> ClientCache = new();

    private readonly IEvmRpcClient _client;
    private readonly ResolvedConfig _config;

    /// <summary>Create a new RPC client.</summary>
    /// <param name="config">Configuration for the RPC client.</param>
    /// <param name="evmFactory">
    /// Optional EVM client factory; defaults to the Nethereum-backed implementation.
    /// Tests inject a fake to avoid network access.
    /// </param>
    public RpcClient(RpcClientConfig config, IEvmRpcClientFactory? evmFactory = null)
    {
        evmFactory ??= DefaultEvmFactory;

        // Resolve config with defaults. Mirrors upstream `config.rpcUrl || getRpcUrl(...)`,
        // where an empty string is treated as "not provided".
        _config = new ResolvedConfig(
            config.ChainId,
            string.IsNullOrEmpty(config.RpcUrl) ? Chains.GetRpcUrl(config.ChainId) : config.RpcUrl,
            config.MaxRetries ?? 3,
            config.InitialRetryDelay ?? 1000);

        // This only fires for alphanet / experimental (no default RPC URL in ChainConfig).
        if (string.IsNullOrEmpty(_config.RpcUrl))
        {
            throw new Exception("rpcUrl argument is required in RpcClient constructor, but was not provided");
        }

        var cacheKey = GetClientKey(_config.ChainId, _config.RpcUrl);
        _client = ClientCache.GetOrAdd(cacheKey, _ => evmFactory.Create(_config.ChainId, _config.RpcUrl));
    }

    private static string GetClientKey(int chainId, string rpcUrl) => $"{chainId}:{rpcUrl}";

    /// <summary>
    /// Convert a <see cref="BlockParameter"/> to a viem-style <see cref="BlockSelector"/>.
    /// Distinguishes block hashes (66-char hex) from block numbers.
    /// Port of upstream <c>toViemBlockParameter</c>.
    /// </summary>
    internal static BlockSelector ToBlockSelector(BlockParameter blockParam)
    {
        if (blockParam.IsString)
        {
            var value = blockParam.StringValue;

            // Handle block tags
            if (value is "earliest" or "latest" or "safe" or "finalized" or "pending")
            {
                return new BlockSelector { BlockTag = value };
            }

            // Handle hex string
            if (value.StartsWith("0x", StringComparison.Ordinal))
            {
                // Block hashes are 32 bytes = 66 characters (including 0x)
                if (value.Length == 66)
                {
                    return new BlockSelector { BlockHash = value };
                }

                // Otherwise it's a hex block number
                return new BlockSelector { BlockNumber = ParseHex(value) };
            }

            // Convert decimal string to bigint
            return new BlockSelector { BlockNumber = BigInteger.Parse(value, CultureInfo.InvariantCulture) };
        }

        // Handle number / bigint
        return new BlockSelector { BlockNumber = blockParam.NumberValue };
    }

    private static BigInteger ParseHex(string value)
    {
        var hex = value[2..];
        // Prefix with '0' so a leading high-bit nibble is not interpreted as a negative sign.
        return BigInteger.Parse("0" + hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Retry a function with exponential backoff.
    /// Port of upstream <c>retry</c>.
    /// </summary>
    private static async Task<T> RetryAsync<T>(Func<Task<T>> fn, int maxRetries, int initialDelay)
    {
        Exception? lastError = null;
        var delay = initialDelay;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await fn();
            }
            catch (BlockNotFoundError)
            {
                // Don't retry BlockNotFoundError - it's a permanent error
                throw;
            }
            catch (Exception error)
            {
                lastError = error;

                // Don't retry on the last attempt
                if (attempt == maxRetries)
                {
                    break;
                }

                // Wait before retrying with exponential backoff
                await Task.Delay(delay);
                delay *= 2;
            }
        }

        throw new NetworkError(
            $"Failed after {maxRetries + 1} attempts: {lastError?.Message}",
            lastError);
    }

    /// <inheritdoc />
    public Task<EvmBlock> GetBlockAsync(BlockParameter blockParameter)
    {
        return RetryAsync(
            async () =>
            {
                try
                {
                    var selector = ToBlockSelector(blockParameter);
                    var block = await _client.GetBlockAsync(selector);

                    if (block is null)
                    {
                        throw new BlockNotFoundError(blockParameter);
                    }

                    return block;
                }
                catch (BlockNotFoundError)
                {
                    // Don't retry BlockNotFoundError - re-throw immediately
                    throw;
                }
                catch (Exception error)
                {
                    // Wrap errors that indicate a missing block in our custom error type
                    var message = error.Message ?? string.Empty;
                    if (message.Contains("not found", StringComparison.Ordinal) ||
                        message.Contains("does not exist", StringComparison.Ordinal))
                    {
                        throw new BlockNotFoundError(blockParameter);
                    }

                    throw;
                }
            },
            _config.MaxRetries,
            _config.InitialRetryDelay);
    }

    /// <inheritdoc />
    public Task<EvmTransactionReceipt> GetTransactionReceiptAsync(string txHash)
    {
        return RetryAsync(
            async () =>
            {
                var receipt = await _client.GetTransactionReceiptAsync(txHash);

                if (receipt is null)
                {
                    throw new Exception($"Transaction receipt not found: {txHash}");
                }

                return receipt;
            },
            _config.MaxRetries,
            _config.InitialRetryDelay);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetSourceLocatorsAsync(string workloadId)
    {
        return RetryAsync<IReadOnlyList<string>>(
            async () =>
            {
                var contractAddress = Chains.GetContractAddress(_config.ChainId);

                var result = await _client.ReadContractAsync(new ReadContractRequest
                {
                    Address = contractAddress,
                    FunctionName = FlashtestationAbi.GetWorkloadMetadataFunctionName,
                    Args = new[] { workloadId },
                });

                // We only need the sourceLocators array.
                return result.SourceLocators;
            },
            _config.MaxRetries,
            _config.InitialRetryDelay);
    }

    /// <summary>Get the flashtestation event from the latest block.</summary>
    public Task<FlashtestationEvent?> GetFlashtestationEventAsync() =>
        GetFlashtestationEventAsync("latest");

    /// <inheritdoc />
    public Task<FlashtestationEvent?> GetFlashtestationEventAsync(BlockParameter blockParameter)
    {
        return RetryAsync<FlashtestationEvent?>(
            async () =>
            {
                // First, get the transaction hash from the block
                var block = await GetBlockAsync(blockParameter);
                if (block.Transactions is null || block.Transactions.Count == 0)
                {
                    return null;
                }

                var txHash = block.Transactions[^1];

                // Then, get the transaction receipt to parse the logs
                var receipt = await _client.GetTransactionReceiptAsync(txHash);
                if (receipt is null)
                {
                    return null;
                }

                // Parse the logs from the receipt to find BlockBuilderProofVerified events
                var parsedLogs = _client.ParseEventLogs(new ParseEventLogsRequest
                {
                    EventName = FlashtestationAbi.BlockBuilderProofVerifiedEventName,
                    Logs = receipt.Logs,
                });

                if (parsedLogs.Count > 0)
                {
                    if (parsedLogs.Count != 1)
                    {
                        throw new Exception("Expected exactly one BlockBuilderProofVerified event");
                    }

                    var args = parsedLogs[0].Args;

                    // Fetch source locators from contract
                    var sourceLocators = await GetSourceLocatorsAsync(args.WorkloadId);

                    return new FlashtestationEvent
                    {
                        Caller = args.Caller,
                        WorkloadId = args.WorkloadId,
                        Version = args.Version,
                        BlockContentHash = args.BlockContentHash,
                        CommitHash = args.CommitHash,
                        SourceLocators = sourceLocators,
                    };
                }

                return null;
            },
            _config.MaxRetries,
            _config.InitialRetryDelay);
    }

    /// <inheritdoc />
    public IEvmRpcClient GetClient() => _client;

    /// <summary>Clear the client cache (useful for testing). Port of upstream <c>clearCache</c>.</summary>
    public static void ClearCache() => ClientCache.Clear();

    private sealed record ResolvedConfig(int ChainId, string RpcUrl, int MaxRetries, int InitialRetryDelay);
}

/// <summary>
/// Default <see cref="IRpcClientFactory"/> that constructs real <see cref="RpcClient"/> instances.
/// </summary>
public sealed class RpcClientFactory : IRpcClientFactory
{
    /// <inheritdoc />
    public IRpcClient Create(RpcClientConfig config) => new RpcClient(config);
}

/// <summary>
/// Static entry points mirroring the upstream module-level function <c>createRpcClient</c>.
/// </summary>
public static class RpcClients
{
    /// <summary>
    /// Create an RPC client with the given configuration.
    /// Port of upstream <c>createRpcClient</c>.
    /// </summary>
    public static RpcClient CreateRpcClient(RpcClientConfig config, IEvmRpcClientFactory? evmFactory = null) =>
        new(config, evmFactory);
}
