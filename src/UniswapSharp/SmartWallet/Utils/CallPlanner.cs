using System.Numerics;
using UniswapSharp.V4.Utils;

namespace UniswapSharp.SmartWallet.Utils;

/// <summary>
/// Encodes a series of <see cref="Call"/>s. Port of
/// <c>smart-wallet-sdk/src/utils/callPlanner.ts</c>.
/// </summary>
public sealed class CallPlanner
{
    /// <summary>
    /// The ABI parameter type for the call array — a single <c>(address,uint256,bytes)[]</c> tuple
    /// array (the viem <c>CALL_ABI_PARAMS</c>). Note the tuple order is <c>to, value, data</c>.
    /// </summary>
    public const string CallAbiParams = "(address,uint256,bytes)[]";

    /// <summary>The calls held by this planner.</summary>
    public List<Call> Calls { get; }

    /// <summary>Create a new <see cref="CallPlanner"/>, optionally seeded with a list of calls.</summary>
    public CallPlanner(IEnumerable<Call>? calls = null)
    {
        Calls = calls?.ToList() ?? new List<Call>();
    }

    /// <summary>The total ETH value of all calls.</summary>
    public BigInteger Value => Calls.Aggregate(BigInteger.Zero, (acc, call) => acc + call.Value);

    /// <summary>ABI-encodes the <c>Call[]</c>.</summary>
    public string Encode()
    {
        if (Calls.Count == 0)
        {
            throw new InvalidOperationException("No calls to encode");
        }

        return AbiParamEncoder.Encode(new[] { CallAbiParams }, new object?[] { CallsAsAbiTuples() });
    }

    /// <summary>Add a call to execute.</summary>
    /// <param name="to">The target address of the call.</param>
    /// <param name="value">The ETH value to send with the call.</param>
    /// <param name="data">The calldata for the call.</param>
    /// <returns>This planner, for chaining.</returns>
    public CallPlanner Add(string to, BigInteger value, string data)
    {
        Calls.Add(new Call(to, data, value));
        return this;
    }

    /// <summary>The calls as positional ABI tuples in <c>(to, value, data)</c> order.</summary>
    internal object?[] CallsAsAbiTuples() =>
        Calls.Select(call => (object?)new object?[] { call.To, call.Value, call.Data }).ToArray();
}
