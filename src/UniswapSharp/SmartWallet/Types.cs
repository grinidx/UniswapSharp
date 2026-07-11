using System.Numerics;

namespace UniswapSharp.SmartWallet;

/// <summary>
/// Contract-specific batched call. Port of the <c>BatchedCall</c> interface in
/// <c>smart-wallet-sdk/src/types.ts</c>.
/// </summary>
public sealed record BatchedCall(IReadOnlyList<Call> Calls, bool RevertOnFailure);

/// <summary>
/// ERC-5792 style call. Port of the <c>Call</c> interface in
/// <c>smart-wallet-sdk/src/types.ts</c>.
/// </summary>
/// <param name="To">The address of the contract to call.</param>
/// <param name="Data">The encoded calldata for the call.</param>
/// <param name="Value">The amount of ETH to send with the call.</param>
/// <param name="ChainId">The chain id for the call (client-side only; never encoded into calldata).</param>
public sealed record Call(string To, string Data, BigInteger Value, object? ChainId = null);

/// <summary>
/// Parameters for method execution. Port of the <c>MethodParameters</c> interface in
/// <c>smart-wallet-sdk/src/types.ts</c>.
/// </summary>
/// <param name="Calldata">Encoded calldata to be sent to the user's delegated account.</param>
/// <param name="Value">The amount of ETH to send with the transaction.</param>
public sealed record MethodParameters(string Calldata, BigInteger Value);

/// <summary>
/// Options for the execute methods. Port of the <c>ExecuteOptions</c> interface in
/// <c>smart-wallet-sdk/src/types.ts</c>. <see cref="RevertOnFailure"/> is <c>null</c> when
/// unset (mirrors the optional TypeScript field).
/// </summary>
/// <param name="RevertOnFailure">When <c>true</c>, the execute call reverts if any call fails.</param>
public sealed record ExecuteOptions(bool? RevertOnFailure = null);
