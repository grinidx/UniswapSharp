using UniswapSharp.UniswapX.Order;

namespace UniswapSharp.UniswapX.Utils;

/// <summary>One result of a batched contract call — mirrors upstream <c>MulticallResult</c>.</summary>
/// <param name="Success">Whether the individual call succeeded.</param>
/// <param name="ReturnData">The call's raw return data (or revert data when it failed).</param>
public sealed record MulticallResult(bool Success, string ReturnData);

/// <summary>
/// Dispatches one batch of calls to a single contract in one request. The transport (deployless
/// multicall, a deployed Multicall2, or a plain per-call loop) is the implementer's concern; this
/// SDK only orchestrates <i>which</i> calls go in <i>which</i> batch.
/// </summary>
/// <remarks>
/// Injectable for the same reason <see cref="INonceLookup"/> is: the batching and result-ordering
/// logic is deterministic and worth testing, while the RPC transport is not. Upstream couples this
/// to ethers' <c>StaticJsonRpcProvider</c> plus TypeChain multicall bindings, neither of which this
/// port carries.
/// </remarks>
public interface IMulticallClient
{
    /// <summary>
    /// Calls <paramref name="functionName"/> on <paramref name="address"/> once per entry in
    /// <paramref name="functionParams"/>, in a single request, returning every result <b>in the same
    /// order as the inputs</b> — including failures.
    /// </summary>
    /// <param name="address">The contract every call targets.</param>
    /// <param name="functionName">The function each call invokes.</param>
    /// <param name="functionParams">One argument list per call.</param>
    /// <param name="blockOverrides">
    /// Block overrides applied to the whole request, or <c>null</c> for none. A block override applies
    /// to an entire <c>eth_call</c>, which is why
    /// <see cref="Multicall.MulticallOrdersPreservingOrderAsync{TOrder}"/> never puts two differently
    /// overridden orders in one batch.
    /// </param>
    Task<IReadOnlyList<MulticallResult>> MulticallSameContractManyCallsAsync(
        string address,
        string functionName,
        IReadOnlyList<IReadOnlyList<object?>> functionParams,
        BlockOverrides? blockOverrides = null);
}

/// <summary>
/// Order-preserving batching over <see cref="IMulticallClient"/>. Ported from the deterministic half
/// of uniswapx-sdk <c>utils/multicall.ts</c>.
/// </summary>
public static class Multicall
{
    /// <summary>
    /// Multicalls a batch of orders, returning results in the <b>same order as</b>
    /// <paramref name="orders"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A block override applies to an entire <c>eth_call</c>, so orders carrying one cannot share a
    /// call with orders quoted at a different block. Each is dispatched on its own while the remaining
    /// orders share a single batched call. Results are then scattered back into their original input
    /// positions.
    /// </para>
    /// <para>
    /// That scatter is the point: callers index results by input position, and the quoters
    /// additionally cross-reference <c>orders[i]</c> when classifying validation errors and checking
    /// terminal states. Before upstream #685 the results came back grouped by batch, so any batch
    /// interleaving overridden and plain orders returned them permuted.
    /// </para>
    /// </remarks>
    /// <param name="client">The batch dispatcher.</param>
    /// <param name="address">The contract every call targets.</param>
    /// <param name="functionName">The function each call invokes.</param>
    /// <param name="orders">The orders to quote, in caller order.</param>
    /// <param name="blockOverridesOf">Reads an order's block overrides (<c>null</c> when it has none).</param>
    /// <param name="buildParams">Builds one order's argument list.</param>
    public static async Task<IReadOnlyList<MulticallResult>> MulticallOrdersPreservingOrderAsync<TOrder>(
        IMulticallClient client,
        string address,
        string functionName,
        IReadOnlyList<TOrder> orders,
        Func<TOrder, BlockOverrides?> blockOverridesOf,
        Func<TOrder, IReadOnlyList<object?>> buildParams)
    {
        var overrideIndices = new List<int>();
        var plainIndices = new List<int>();
        for (int i = 0; i < orders.Count; i++)
        {
            (blockOverridesOf(orders[i]) is not null ? overrideIndices : plainIndices).Add(i);
        }

        var batches = new List<Task<(IReadOnlyList<int> Indices, IReadOnlyList<MulticallResult> Results)>>();

        foreach (int i in overrideIndices)
        {
            int index = i;
            batches.Add(DispatchAsync(
                client, address, functionName,
                new[] { buildParams(orders[index]) },
                blockOverridesOf(orders[index]),
                new[] { index }));
        }

        // Skip the batched call entirely when every order carries an override, otherwise it costs a
        // round trip to quote nothing.
        if (plainIndices.Count > 0)
        {
            batches.Add(DispatchAsync(
                client, address, functionName,
                plainIndices.Select(i => buildParams(orders[i])).ToList(),
                blockOverrides: null,
                plainIndices));
        }

        var completed = await Task.WhenAll(batches).ConfigureAwait(false);

        var ordered = new MulticallResult[orders.Count];
        foreach (var (indices, results) in completed)
        {
            for (int j = 0; j < indices.Count; j++)
            {
                ordered[indices[j]] = results[j];
            }
        }
        return ordered;
    }

    private static async Task<(IReadOnlyList<int> Indices, IReadOnlyList<MulticallResult> Results)> DispatchAsync(
        IMulticallClient client,
        string address,
        string functionName,
        IReadOnlyList<IReadOnlyList<object?>> functionParams,
        BlockOverrides? blockOverrides,
        IReadOnlyList<int> indices)
    {
        var results = await client
            .MulticallSameContractManyCallsAsync(address, functionName, functionParams, blockOverrides)
            .ConfigureAwait(false);
        return (indices, results);
    }
}
