using System.Numerics;
using AwesomeAssertions;
using UniswapSharp.Flashtestations.Rpc;
using UniswapSharp.Flashtestations.Types;

namespace UniswapSharp.Testing.Flashtestations;

// Ported 1:1 from sdks/flashtestations-sdk/test/rpc/client.test.ts
// The viem PublicClient + parseEventLogs are replaced by an injected FakeEvmRpcClient
// (see Fakes.cs), mirroring the bun `mock.module('viem', ...)` approach.
public class RpcClientTests
{
    private readonly FakeEvmRpcClientFactory _factory = new();

    public RpcClientTests()
    {
        // Mirrors the upstream `beforeEach(() => { RpcClient.clearCache(); ... })`.
        RpcClient.ClearCache();
    }

    private FakeEvmRpcClient Evm => _factory.Client;

    private RpcClient NewClient(int chainId, string? rpcUrl = null, int? maxRetries = null, int? initialRetryDelay = null) =>
        new(
            new RpcClientConfig
            {
                ChainId = chainId,
                RpcUrl = rpcUrl,
                MaxRetries = maxRetries,
                InitialRetryDelay = initialRetryDelay,
            },
            _factory);

    // ----- constructor -----

    [Fact]
    public void Constructor_DefaultConfiguration()
    {
        var client = NewClient(1301);
        client.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_CustomRpcUrl()
    {
        var client = NewClient(1301, rpcUrl: "https://custom-rpc.example.com");
        client.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_ThrowsForUnsupportedChain()
    {
        ((Action)(() => NewClient(999999))).Should().Throw<ChainNotSupportedError>();
    }

    // ----- getBlock -----

    [Fact]
    public async Task GetBlock_ByTagLatest()
    {
        var mockBlock = new EvmBlock { Number = 100, Hash = "0xabc" };
        Evm.GetBlock.Returns(mockBlock);

        var client = NewClient(1301, maxRetries: 0);
        var block = await client.GetBlockAsync("latest");

        Evm.GetBlock.LastCall.Should().Be(new BlockSelector { BlockTag = "latest" });
        block.Should().Be(mockBlock);
    }

    [Fact]
    public async Task GetBlock_ByTagFinalized()
    {
        var mockBlock = new EvmBlock { Number = 95, Hash = "0xdef" };
        Evm.GetBlock.Returns(mockBlock);

        var client = NewClient(1301, maxRetries: 0);
        var block = await client.GetBlockAsync("finalized");

        Evm.GetBlock.LastCall.Should().Be(new BlockSelector { BlockTag = "finalized" });
        block.Should().Be(mockBlock);
    }

    [Fact]
    public async Task GetBlock_ByNumber()
    {
        var mockBlock = new EvmBlock { Number = 12345, Hash = "0x123" };
        Evm.GetBlock.Returns(mockBlock);

        var client = NewClient(1301, maxRetries: 0);
        var block = await client.GetBlockAsync(12345);

        Evm.GetBlock.LastCall.Should().Be(new BlockSelector { BlockNumber = 12345 });
        block.Should().Be(mockBlock);
    }

    [Fact]
    public async Task GetBlock_ByBigInteger()
    {
        var mockBlock = new EvmBlock { Number = 12345, Hash = "0x123" };
        Evm.GetBlock.Returns(mockBlock);

        var client = NewClient(1301, maxRetries: 0);
        var block = await client.GetBlockAsync(new BigInteger(12345));

        Evm.GetBlock.LastCall.Should().Be(new BlockSelector { BlockNumber = 12345 });
        block.Should().Be(mockBlock);
    }

    [Fact]
    public async Task GetBlock_ByHexNumber()
    {
        var mockBlock = new EvmBlock { Number = 255, Hash = "0x456" };
        Evm.GetBlock.Returns(mockBlock);

        var client = NewClient(1301, maxRetries: 0);
        var block = await client.GetBlockAsync("0xff");

        Evm.GetBlock.LastCall.Should().Be(new BlockSelector { BlockNumber = 255 });
        block.Should().Be(mockBlock);
    }

    [Fact]
    public async Task GetBlock_ByBlockHash()
    {
        const string blockHash = "0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";
        var mockBlock = new EvmBlock { Number = 100, Hash = blockHash };
        Evm.GetBlock.Returns(mockBlock);

        var client = NewClient(1301, maxRetries: 0);
        var block = await client.GetBlockAsync(blockHash);

        Evm.GetBlock.LastCall.Should().Be(new BlockSelector { BlockHash = blockHash });
        block.Should().Be(mockBlock);
    }

    [Fact]
    public async Task GetBlock_ThrowsBlockNotFoundWhenMissing()
    {
        Evm.GetBlock.Throws(new Exception("Block not found"));

        var client = NewClient(1301, maxRetries: 0);
        await ((Func<Task>)(() => client.GetBlockAsync(999999))).Should().ThrowAsync<BlockNotFoundError>();
    }

    [Fact]
    public async Task GetBlock_ThrowsBlockNotFoundWhenNull()
    {
        Evm.GetBlock.Returns(null);

        var client = NewClient(1301, maxRetries: 0);
        await ((Func<Task>)(() => client.GetBlockAsync("latest"))).Should().ThrowAsync<BlockNotFoundError>();
    }

    // ----- getTransactionReceipt -----

    [Fact]
    public async Task GetTransactionReceipt_Fetches()
    {
        const string txHash = "0xabc123";
        var mockReceipt = new EvmTransactionReceipt
        {
            TransactionHash = txHash,
            BlockNumber = 100,
            Status = "success",
        };
        Evm.GetTransactionReceipt.Returns(mockReceipt);

        var client = NewClient(1301, maxRetries: 0);
        var receipt = await client.GetTransactionReceiptAsync(txHash);

        Evm.GetTransactionReceipt.LastCall.Should().Be(txHash);
        receipt.Should().Be(mockReceipt);
    }

    [Fact]
    public async Task GetTransactionReceipt_ThrowsWhenNotFound()
    {
        Evm.GetTransactionReceipt.Returns(null);

        var client = NewClient(1301, maxRetries: 0);
        await ((Func<Task>)(() => client.GetTransactionReceiptAsync("0xinvalid"))).Should()
            .ThrowAsync<NetworkError>()
            .WithMessage("*Transaction receipt not found*");
    }

    // ----- getSourceLocators -----

    private const string SampleWorkloadId = "0x71d62ba17902d590dad932310a7ec12feffa25454d7009c2084aa6f4c488953f";

    [Fact]
    public async Task GetSourceLocators_Fetches()
    {
        var mockMetadata = new ContractWorkloadMetadata
        {
            CommitHash = "490fb2be109f0c2626c347bb3e43e97826c8f844",
            SourceLocators = new[]
            {
                "https://github.com/example/repo1/c41fa4d500f6fb4e4fe46c23b34b26367e10beb4",
                "https://github.com/example/repo2/86ebf9de12466aaae1485eb6fc80ae3c78954edf",
            },
        };
        Evm.ReadContract.Returns(mockMetadata);

        var client = NewClient(1301, maxRetries: 0);
        var result = await client.GetSourceLocatorsAsync(SampleWorkloadId);

        Evm.ReadContract.LastCall.Address.Should().Be("0x3b03b3caabd49ca12de9eba46a6a2950700b1db4");
        Evm.ReadContract.LastCall.FunctionName.Should().Be("getWorkloadMetadata");
        Evm.ReadContract.LastCall.Args.Should().Equal(SampleWorkloadId);
        result.Should().Equal(mockMetadata.SourceLocators);
    }

    [Fact]
    public async Task GetSourceLocators_HandlesEmpty()
    {
        var mockMetadata = new ContractWorkloadMetadata
        {
            CommitHash = "490fb2be109f0c2626c347bb3e43e97826c8f844",
            SourceLocators = Array.Empty<string>(),
        };
        Evm.ReadContract.Returns(mockMetadata);

        var client = NewClient(1301, maxRetries: 0);
        var result = await client.GetSourceLocatorsAsync(SampleWorkloadId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSourceLocators_RetriesOnTransientFailures()
    {
        var mockMetadata = new ContractWorkloadMetadata
        {
            CommitHash = "490fb2be109f0c2626c347bb3e43e97826c8f844",
            SourceLocators = new[] { "https://github.com/example/repo/86ebf9de12466aaae1485eb6fc80ae3c78954edf" },
        };

        var client = NewClient(1301, maxRetries: 2, initialRetryDelay: 10);
        Evm.ReadContract.ThrowsOnce(new Exception("Network error")).ReturnsOnce(mockMetadata);

        var result = await client.GetSourceLocatorsAsync(SampleWorkloadId);

        Evm.ReadContract.CallCount.Should().Be(2);
        result.Should().Equal(mockMetadata.SourceLocators);
    }

    [Fact]
    public async Task GetSourceLocators_ThrowsNetworkErrorAfterMaxRetries()
    {
        var client = NewClient(1301, maxRetries: 1, initialRetryDelay: 10);
        Evm.ReadContract.Throws(new Exception("Network error"));

        await ((Func<Task>)(() => client.GetSourceLocatorsAsync(SampleWorkloadId))).Should().ThrowAsync<NetworkError>();
        Evm.ReadContract.CallCount.Should().Be(2);
    }

    // ----- retry logic -----

    [Fact]
    public async Task Retry_WithExponentialBackoff()
    {
        var client = NewClient(1301, maxRetries: 2, initialRetryDelay: 10);
        Evm.GetBlock
            .ThrowsOnce(new Exception("Network error"))
            .ThrowsOnce(new Exception("Network error"))
            .ReturnsOnce(new EvmBlock { Number = 100, Hash = "0xabc" });

        var block = await client.GetBlockAsync("latest");

        Evm.GetBlock.CallCount.Should().Be(3);
        block.Should().Be(new EvmBlock { Number = 100, Hash = "0xabc" });
    }

    [Fact]
    public async Task Retry_ThrowsNetworkErrorAfterMaxRetries()
    {
        var client = NewClient(1301, maxRetries: 1, initialRetryDelay: 10);
        Evm.GetBlock.Throws(new Exception("Network error"));

        await ((Func<Task>)(() => client.GetBlockAsync("latest"))).Should().ThrowAsync<NetworkError>();
        Evm.GetBlock.CallCount.Should().Be(2); // initial + 1 retry
    }

    // ----- client caching -----

    [Fact]
    public void Caching_ReusesClientForSameChainAndRpcUrl()
    {
        NewClient(1301);
        NewClient(1301);

        _factory.CreateCount.Should().Be(1);
    }

    [Fact]
    public void Caching_SeparateClientsForDifferentChains()
    {
        NewClient(1301);
        NewClient(130);

        _factory.CreateCount.Should().Be(2);
    }

    [Fact]
    public void Caching_SeparateClientsForDifferentRpcUrls()
    {
        NewClient(1301, rpcUrl: "https://rpc1.example.com");
        NewClient(1301, rpcUrl: "https://rpc2.example.com");

        _factory.CreateCount.Should().Be(2);
    }

    // ----- createRpcClient -----

    [Fact]
    public void CreateRpcClient_CreatesInstance()
    {
        var client = RpcClients.CreateRpcClient(new RpcClientConfig { ChainId = 1301 }, _factory);
        client.Should().BeOfType<RpcClient>();
    }

    // ----- getClient -----

    [Fact]
    public void GetClient_ReturnsUnderlyingClient()
    {
        var client = NewClient(1301);
        client.GetClient().Should().BeSameAs(Evm);
    }

    // ----- getFlashtestationEvent -----

    [Fact]
    public async Task GetFlashtestationEvent_ReturnsEventWhenPresent()
    {
        const string txHash = "0xabc123def456";
        const int blockNumber = 100;

        var mockBlock = new EvmBlock
        {
            Number = blockNumber,
            Hash = "0xblockhash123",
            Transactions = new[] { txHash },
        };
        var mockReceipt = new EvmTransactionReceipt
        {
            TransactionHash = txHash,
            BlockNumber = blockNumber,
            To = "0xcontract123",
            Status = "success",
            Logs = Array.Empty<EvmLog>(),
        };
        var mockLog = new ParsedFlashtestationLog
        {
            Args = new BlockBuilderProofVerifiedArgs
            {
                Caller = "0xcaBBa9e7f4b3A885C5aa069f88469ac711Dd4aCC",
                WorkloadId = SampleWorkloadId,
                Version = 1,
                BlockContentHash = "0x846604baa7db2297b9c4058106cc5869bcdbb753760981dbcd6d345d3d5f3e0f",
                CommitHash = "490fb2be109f0c2626c347bb3e43e97826c8f844",
            },
        };
        var mockMetadata = new ContractWorkloadMetadata
        {
            CommitHash = "490fb2be109f0c2626c347bb3e43e97826c8f844",
            SourceLocators = new[]
            {
                "https://github.com/example/repo1/86ebf9de12466aaae1485eb6fc80ae3c78954edf",
                "https://github.com/example/repo2/f6cf154d5a26c632548d85998c2a7dab40d8ef02",
            },
        };

        Evm.GetBlock.Returns(mockBlock);
        Evm.GetTransactionReceipt.Returns(mockReceipt);
        Evm.ParseEventLogsMock.Returns(new[] { mockLog });
        Evm.ReadContract.Returns(mockMetadata);

        var client = NewClient(1301, maxRetries: 0);
        var result = await client.GetFlashtestationEventAsync(blockNumber);

        Evm.GetBlock.LastCall.Should().Be(new BlockSelector { BlockNumber = blockNumber });
        Evm.GetTransactionReceipt.LastCall.Should().Be(txHash);
        Evm.ParseEventLogsMock.LastCall.EventName.Should().Be("BlockBuilderProofVerified");
        Evm.ParseEventLogsMock.LastCall.Logs.Should().BeSameAs(mockReceipt.Logs);
        Evm.ReadContract.LastCall.FunctionName.Should().Be("getWorkloadMetadata");
        Evm.ReadContract.LastCall.Args.Should().Equal(SampleWorkloadId);

        result.Should().BeEquivalentTo(new FlashtestationEvent
        {
            Caller = "0xcaBBa9e7f4b3A885C5aa069f88469ac711Dd4aCC",
            WorkloadId = SampleWorkloadId,
            Version = 1,
            BlockContentHash = "0x846604baa7db2297b9c4058106cc5869bcdbb753760981dbcd6d345d3d5f3e0f",
            CommitHash = "490fb2be109f0c2626c347bb3e43e97826c8f844",
            SourceLocators = new[]
            {
                "https://github.com/example/repo1/86ebf9de12466aaae1485eb6fc80ae3c78954edf",
                "https://github.com/example/repo2/f6cf154d5a26c632548d85998c2a7dab40d8ef02",
            },
        });
    }

    [Fact]
    public async Task GetFlashtestationEvent_ReturnsNullWhenReceiptNotFound()
    {
        const string txHash = "0xabc123def456";
        const int blockNumber = 100;

        Evm.GetBlock.Returns(new EvmBlock { Number = blockNumber, Hash = "0xblockhash123", Transactions = new[] { txHash } });
        Evm.GetTransactionReceipt.Returns(null);

        var client = NewClient(1301, maxRetries: 0);
        var result = await client.GetFlashtestationEventAsync(blockNumber);

        Evm.GetBlock.LastCall.Should().Be(new BlockSelector { BlockNumber = blockNumber });
        Evm.GetTransactionReceipt.LastCall.Should().Be(txHash);
        Evm.ParseEventLogsMock.CallCount.Should().Be(0);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetFlashtestationEvent_ReturnsNullWhenEventNotFound()
    {
        const string txHash = "0xabc123def456";
        const int blockNumber = 100;

        Evm.GetBlock.Returns(new EvmBlock { Number = blockNumber, Hash = "0xblockhash123", Transactions = new[] { txHash } });
        Evm.GetTransactionReceipt.Returns(new EvmTransactionReceipt
        {
            TransactionHash = txHash,
            BlockNumber = blockNumber,
            To = "0xcontract123",
            Status = "success",
            Logs = Array.Empty<EvmLog>(),
        });
        Evm.ParseEventLogsMock.Returns(Array.Empty<ParsedFlashtestationLog>());

        var client = NewClient(1301, maxRetries: 0);
        var result = await client.GetFlashtestationEventAsync(blockNumber);

        Evm.ParseEventLogsMock.CallCount.Should().BeGreaterThan(0);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetFlashtestationEvent_HandlesReceiptWithNoToAddress()
    {
        const string txHash = "0xabc123def456";
        const int blockNumber = 100;

        var mockReceipt = new EvmTransactionReceipt
        {
            TransactionHash = txHash,
            BlockNumber = blockNumber,
            To = null, // Contract creation transaction
            Status = "success",
            Logs = Array.Empty<EvmLog>(),
        };

        Evm.GetBlock.Returns(new EvmBlock { Number = blockNumber, Hash = "0xblockhash123", Transactions = new[] { txHash } });
        Evm.GetTransactionReceipt.Returns(mockReceipt);
        Evm.ParseEventLogsMock.Returns(Array.Empty<ParsedFlashtestationLog>());

        var client = NewClient(1301, maxRetries: 0);
        var result = await client.GetFlashtestationEventAsync(blockNumber);

        Evm.ParseEventLogsMock.LastCall.EventName.Should().Be("BlockBuilderProofVerified");
        Evm.ParseEventLogsMock.LastCall.Logs.Should().BeSameAs(mockReceipt.Logs);
        result.Should().BeNull();
    }
}
