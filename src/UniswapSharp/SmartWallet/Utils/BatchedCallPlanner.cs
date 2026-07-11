using System.Numerics;
using UniswapSharp.V4.Utils;

namespace UniswapSharp.SmartWallet.Utils;

/// <summary>
/// Encodes a <see cref="BatchedCall"/> (the <c>calls</c> plus <c>revertOnFailure</c>). Port of
/// <c>smart-wallet-sdk/src/utils/batchedCallPlanner.ts</c>.
/// </summary>
public sealed class BatchedCallPlanner
{
    /// <summary>
    /// The ABI parameter type for the batched call — a single
    /// <c>((address,uint256,bytes)[],bool)</c> tuple (the viem <c>BATCHED_CALL_ABI_PARAMS</c>).
    /// </summary>
    public const string BatchedCallAbiParams = "((address,uint256,bytes)[],bool)";

    /// <summary>The underlying <see cref="CallPlanner"/>.</summary>
    public CallPlanner CallPlanner { get; }

    /// <summary>Whether the batched execution reverts if any call fails.</summary>
    public bool RevertOnFailure { get; }

    /// <summary>Create a new <see cref="BatchedCallPlanner"/>.</summary>
    /// <param name="callPlanner">The call planner to wrap.</param>
    /// <param name="revertOnFailure">Whether to revert on the first failing call (default <c>true</c>).</param>
    public BatchedCallPlanner(CallPlanner callPlanner, bool revertOnFailure = true)
    {
        CallPlanner = callPlanner;
        RevertOnFailure = revertOnFailure;
    }

    /// <summary>The total ETH value of the underlying calls.</summary>
    public BigInteger Value => CallPlanner.Value;

    /// <summary>Add a call to the underlying <see cref="CallPlanner"/>.</summary>
    /// <returns>This planner, for chaining.</returns>
    public BatchedCallPlanner Add(string to, BigInteger value, string data)
    {
        CallPlanner.Add(to, value, data);
        return this;
    }

    /// <summary>ABI-encodes the <see cref="BatchedCall"/>.</summary>
    public string Encode() =>
        AbiParamEncoder.Encode(
            new[] { BatchedCallAbiParams },
            new object?[] { new object?[] { CallPlanner.CallsAsAbiTuples(), RevertOnFailure } });

    /// <summary>The batched call as a <see cref="BatchedCall"/> record.</summary>
    public BatchedCall ToBatchedCall() => new(CallPlanner.Calls, RevertOnFailure);
}
