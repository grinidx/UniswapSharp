using UniswapSharp.Flashtestations.Rpc;
using UniswapSharp.Flashtestations.Types;

namespace UniswapSharp.Testing.Flashtestations;

/// <summary>
/// A configurable async method mock supporting a default behaviour plus a queue of one-shot
/// behaviours (mirroring bun/jest's <c>mockResolvedValue</c> / <c>mockResolvedValueOnce</c> /
/// <c>mockRejectedValue</c> / <c>mockRejectedValueOnce</c>), while recording every call's argument.
/// </summary>
public sealed class AsyncMethodMock<TArg, TResult>
{
    private readonly Queue<Func<TArg, TResult>> _onceHandlers = new();
    private Func<TArg, TResult>? _default;

    public List<TArg> Calls { get; } = new();

    public int CallCount => Calls.Count;

    public TArg LastCall => Calls[^1];

    public AsyncMethodMock<TArg, TResult> Returns(TResult value)
    {
        _default = _ => value;
        return this;
    }

    public AsyncMethodMock<TArg, TResult> Throws(Exception ex)
    {
        _default = _ => throw ex;
        return this;
    }

    public AsyncMethodMock<TArg, TResult> ReturnsOnce(TResult value)
    {
        _onceHandlers.Enqueue(_ => value);
        return this;
    }

    public AsyncMethodMock<TArg, TResult> ThrowsOnce(Exception ex)
    {
        _onceHandlers.Enqueue(_ => throw ex);
        return this;
    }

    public Task<TResult> InvokeAsync(TArg arg)
    {
        Calls.Add(arg);
        var handler = _onceHandlers.Count > 0 ? _onceHandlers.Dequeue() : _default;
        if (handler is null)
        {
            throw new InvalidOperationException("No behaviour configured for this mock.");
        }

        try
        {
            return Task.FromResult(handler(arg));
        }
        catch (Exception ex)
        {
            return Task.FromException<TResult>(ex);
        }
    }
}

/// <summary>A configurable synchronous method mock recording every call's argument.</summary>
public sealed class SyncMethodMock<TArg, TResult>
{
    private Func<TArg, TResult>? _default;

    public List<TArg> Calls { get; } = new();

    public int CallCount => Calls.Count;

    public TArg LastCall => Calls[^1];

    public SyncMethodMock<TArg, TResult> Returns(TResult value)
    {
        _default = _ => value;
        return this;
    }

    public TResult Invoke(TArg arg)
    {
        Calls.Add(arg);
        if (_default is null)
        {
            throw new InvalidOperationException("No behaviour configured for this mock.");
        }

        return _default(arg);
    }
}

/// <summary>
/// Fake <see cref="IEvmRpcClient"/> that replaces the viem <c>PublicClient</c> +
/// <c>parseEventLogs</c> in the RPC client tests (mirrors the bun <c>mock.module('viem', ...)</c>).
/// </summary>
public sealed class FakeEvmRpcClient : IEvmRpcClient
{
    public FakeEvmRpcClient()
    {
        // Default parseEventLogs behaviour matches the bun test's `mockParseEventLogs.mockReturnValue([])`.
        ParseEventLogsMock.Returns(Array.Empty<ParsedFlashtestationLog>());
    }

    public AsyncMethodMock<BlockSelector, EvmBlock?> GetBlock { get; } = new();

    public AsyncMethodMock<string, EvmTransactionReceipt?> GetTransactionReceipt { get; } = new();

    public AsyncMethodMock<ReadContractRequest, ContractWorkloadMetadata> ReadContract { get; } = new();

    public SyncMethodMock<ParseEventLogsRequest, IReadOnlyList<ParsedFlashtestationLog>> ParseEventLogsMock { get; } = new();

    public Task<EvmBlock?> GetBlockAsync(BlockSelector selector) => GetBlock.InvokeAsync(selector);

    public Task<EvmTransactionReceipt?> GetTransactionReceiptAsync(string hash) => GetTransactionReceipt.InvokeAsync(hash);

    public Task<ContractWorkloadMetadata> ReadContractAsync(ReadContractRequest request) => ReadContract.InvokeAsync(request);

    public IReadOnlyList<ParsedFlashtestationLog> ParseEventLogs(ParseEventLogsRequest request) => ParseEventLogsMock.Invoke(request);
}

/// <summary>
/// Fake <see cref="IEvmRpcClientFactory"/> that returns a shared <see cref="FakeEvmRpcClient"/>
/// and records each create call (mirrors the bun <c>mockCreatePublicClient</c> call counting).
/// </summary>
public sealed class FakeEvmRpcClientFactory : IEvmRpcClientFactory
{
    public FakeEvmRpcClient Client { get; } = new();

    public List<(int ChainId, string RpcUrl)> CreateCalls { get; } = new();

    public int CreateCount => CreateCalls.Count;

    public IEvmRpcClient Create(int chainId, string rpcUrl)
    {
        CreateCalls.Add((chainId, rpcUrl));
        return Client;
    }
}

/// <summary>
/// Fake <see cref="IRpcClient"/> used in the verification-service tests
/// (mirrors <c>jest.spyOn(rpcClientModule, 'RpcClient')</c>).
/// </summary>
public sealed class FakeRpcClient : IRpcClient
{
    public AsyncMethodMock<BlockParameter, FlashtestationEvent?> GetFlashtestationEvent { get; } = new();

    public AsyncMethodMock<BlockParameter, EvmBlock> GetBlock { get; } = new();

    public Task<FlashtestationEvent?> GetFlashtestationEventAsync(BlockParameter blockParameter) =>
        GetFlashtestationEvent.InvokeAsync(blockParameter);

    public Task<EvmBlock> GetBlockAsync(BlockParameter blockParameter) => GetBlock.InvokeAsync(blockParameter);

    public Task<EvmTransactionReceipt> GetTransactionReceiptAsync(string txHash) =>
        throw new NotSupportedException("Not used by the verification-service tests.");

    public Task<IReadOnlyList<string>> GetSourceLocatorsAsync(string workloadId) =>
        throw new NotSupportedException("Not used by the verification-service tests.");

    public IEvmRpcClient GetClient() =>
        throw new NotSupportedException("Not used by the verification-service tests.");
}

/// <summary>
/// Fake <see cref="IRpcClientFactory"/> that records every configuration it is asked to create
/// (mirrors the bun assertion <c>expect(mockRpcClientConstructor).toHaveBeenCalledWith(...)</c>).
/// </summary>
public sealed class FakeRpcClientFactory : IRpcClientFactory
{
    public FakeRpcClient Client { get; } = new();

    public List<RpcClientConfig> CreateCalls { get; } = new();

    public RpcClientConfig? LastConfig => CreateCalls.Count > 0 ? CreateCalls[^1] : null;

    public IRpcClient Create(RpcClientConfig config)
    {
        CreateCalls.Add(config);
        return Client;
    }
}
