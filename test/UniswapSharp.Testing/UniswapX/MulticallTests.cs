using UniswapSharp.UniswapX.Order;
using UniswapSharp.UniswapX.Utils;

namespace UniswapSharp.Testing.UniswapX;

// Ported from sdks/uniswapx-sdk/src/utils/multicall.test.ts (upstream #685/#686)
public class MulticallTests
{
    private const string QUOTER_ADDRESS = "0x0000000000000000000000000000000000000001";

    /// <summary>Stand-in for a real order: one string id per order, so each call traces back to it.</summary>
    private sealed record TestOrder(string Id, BlockOverrides? BlockOverrides);

    private static TestOrder Plain(string id) => new(id, null);
    private static TestOrder Overridden(string id, string blockNumber) => new(id, new BlockOverrides(blockNumber));

    /// <summary>One dispatched batch, recorded so the tests can assert how calls were grouped.</summary>
    private sealed record SentCall(IReadOnlyList<string> Ids, BlockOverrides? BlockOverrides);

    /// <summary>
    /// Fake client that records each batch and echoes every id straight back as that call's return data.
    /// </summary>
    private sealed class RecordingClient : IMulticallClient
    {
        public List<SentCall> Sent { get; } = new();

        public Task<IReadOnlyList<MulticallResult>> MulticallSameContractManyCallsAsync(
            string address,
            string functionName,
            IReadOnlyList<IReadOnlyList<object?>> functionParams,
            BlockOverrides? blockOverrides = null)
        {
            Assert.Equal(QUOTER_ADDRESS, address);
            Assert.Equal("quote", functionName);

            var ids = functionParams.Select(p => (string)p[0]!).ToList();
            Sent.Add(new SentCall(ids, blockOverrides));

            IReadOnlyList<MulticallResult> results = ids
                .Select(id => new MulticallResult(true, id))
                .ToList();
            return Task.FromResult(results);
        }
    }

    private static async Task<IReadOnlyList<string>> QuoteIdsAsync(
        IReadOnlyList<TestOrder> orders, RecordingClient? client = null)
    {
        client ??= new RecordingClient();

        var results = await Multicall.MulticallOrdersPreservingOrderAsync(
            client,
            QUOTER_ADDRESS,
            "quote",
            orders,
            order => order.BlockOverrides,
            order => new object?[] { order.Id });

        return results.Select(r => r.ReturnData).ToList();
    }

    // ---- results line up with the input orders ----
    //
    // Orders carrying block overrides are dispatched on separate eth_calls, so any batch that
    // interleaves the two kinds used to come back permuted.

    public static TheoryData<string, string[]> OrderingCases() => new()
    {
        { "empty batch", Array.Empty<string>() },
        { "single plain order", new[] { "a" } },
        { "single overridden order", new[] { "A:0x64" } },
        { "all plain", new[] { "a", "b", "c" } },
        { "all overridden", new[] { "A:0x64", "B:0x65", "C:0x66" } },
        { "overridden first", new[] { "A:0x64", "b" } },
        { "plain first", new[] { "a", "B:0x64" } },
        { "overridden last of many", new[] { "a", "b", "C:0x64" } },
        { "overridden first of many", new[] { "A:0x64", "b", "c" } },
        { "overridden sandwiched", new[] { "a", "B:0x64", "c" } },
        { "interleaved", new[] { "a", "B:0x64", "c", "D:0x65", "e" } },
    };

    /// <summary>"a" = plain order a; "A:0x64" = order a overridden at block 0x64.</summary>
    private static TestOrder Parse(string spec)
    {
        int colon = spec.IndexOf(':');
        return colon < 0
            ? Plain(spec)
            : Overridden(spec[..colon].ToLowerInvariant(), spec[(colon + 1)..]);
    }

    [Theory]
    [MemberData(nameof(OrderingCases))]
    public async Task MulticallOrdersPreservingOrder_ResultsLineUpWithTheInputOrders(string name, string[] specs)
    {
        _ = name;
        var orders = specs.Select(Parse).ToList();

        Assert.Equal(orders.Select(o => o.Id).ToList(), await QuoteIdsAsync(orders));
    }

    [Fact]
    public async Task MulticallOrdersPreservingOrder_QuotesEachOverriddenOrderAtItsOwnBlock()
    {
        var client = new RecordingClient();
        await QuoteIdsAsync(new[] { Plain("a"), Overridden("b", "0x64"), Overridden("c", "0x65") }, client);

        var sent = client.Sent.OrderBy(call => call.Ids[0], StringComparer.Ordinal).ToList();

        Assert.Equal(3, sent.Count);
        Assert.Equal(new[] { "a" }, sent[0].Ids);
        Assert.Null(sent[0].BlockOverrides);
        Assert.Equal(new[] { "b" }, sent[1].Ids);
        Assert.Equal("0x64", sent[1].BlockOverrides!.Number);
        Assert.Equal(new[] { "c" }, sent[2].Ids);
        Assert.Equal("0x65", sent[2].BlockOverrides!.Number);
    }

    [Fact]
    public async Task MulticallOrdersPreservingOrder_BatchesEveryOrderWithoutOverridesIntoASingleCall()
    {
        var client = new RecordingClient();
        await QuoteIdsAsync(new[] { Plain("a"), Overridden("b", "0x64"), Plain("c") }, client);

        Assert.Equal(2, client.Sent.Count);
        var plainCall = Assert.Single(client.Sent, call => call.BlockOverrides is null);
        Assert.Equal(new[] { "a", "c" }, plainCall.Ids);
    }

    [Fact]
    public async Task MulticallOrdersPreservingOrder_SkipsTheBatchedCallWhenEveryOrderCarriesAnOverride()
    {
        var client = new RecordingClient();
        await QuoteIdsAsync(new[] { Overridden("a", "0x64"), Overridden("b", "0x65") }, client);

        // no empty plain batch — that would cost a round trip to quote nothing
        Assert.Equal(2, client.Sent.Count);
        Assert.All(client.Sent, call => Assert.Single(call.Ids));
    }

    [Fact]
    public async Task MulticallOrdersPreservingOrder_MakesNoCallsForAnEmptyBatch()
    {
        var client = new RecordingClient();

        Assert.Empty(await QuoteIdsAsync(Array.Empty<TestOrder>(), client));
        Assert.Empty(client.Sent);
    }
}
