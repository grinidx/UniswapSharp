using UniswapSharp.SmartWallet.Utils;
using UniswapSharp.V4.Utils;

namespace UniswapSharp.SmartWallet;

/// <summary>
/// Main SDK class for interacting with Uniswap smart wallet contracts. Port of
/// <c>smart-wallet-sdk/src/smartWallet.ts</c>.
/// </summary>
public static class SmartWallet
{
    /// <summary>
    /// EntryPoint <c>executeUserOp</c> selector. The EntryPoint recognizes this selector and calls
    /// <c>executeUserOp(userOp, userOpHash)</c> on the account; the calldata is not a standard
    /// ABI-encoded call, so the selector is concatenated manually (mirrors upstream).
    /// </summary>
    private const string ExecuteUserOpSelector = "0x8dd7712f";

    /// <summary>Selector for <c>execute(((address,uint256,bytes)[],bool))</c>.</summary>
    private const string ExecuteBatchedCallSelector = "0x99e1d016";

    /// <summary>Selector for <c>execute(bytes32,bytes)</c> (ERC-7821 entrypoint).</summary>
    private const string ExecuteSelector = "0xe9ae5c53";

    /// <summary>
    /// Creates method parameters for a UserOperation to be executed through a smart wallet.
    /// Compatible with EntryPoint v0.7.0 and v0.8.0 (not v0.6.0).
    /// </summary>
    /// <param name="calls">Array of calls to encode.</param>
    /// <param name="options">Basic options for the execution.</param>
    /// <returns>Method parameters with the userOp calldata and value.</returns>
    public static MethodParameters EncodeUserOp(IReadOnlyList<Call> calls, ExecuteOptions? options = null)
    {
        options ??= new ExecuteOptions();
        var planner = new CallPlanner(calls);
        var batchedCallPlanner = new BatchedCallPlanner(planner, options.RevertOnFailure ?? true);

        // UserOp callData format: executeUserOp selector + abi.encode(abi.encode(Call[]), revertOnFailure).
        string calldata = ExecuteUserOpSelector + StripHexPrefix(batchedCallPlanner.Encode());
        return new MethodParameters(calldata, planner.Value);
    }

    /// <summary>
    /// Creates method parameters for executing a simple batch of calls through a smart wallet.
    /// </summary>
    /// <param name="calls">Array of calls to encode.</param>
    /// <param name="options">Basic options for the execution.</param>
    /// <returns>Method parameters with the calldata and value.</returns>
    public static MethodParameters EncodeBatchedCall(IReadOnlyList<Call> calls, ExecuteOptions? options = null)
    {
        options ??= new ExecuteOptions();
        var planner = new CallPlanner(calls);
        var batchedCallPlanner = new BatchedCallPlanner(planner, options.RevertOnFailure ?? true);

        // encodeFunctionData({ functionName: '0x99e1d016', args: [batchedCall] }) is the selector
        // followed by the ABI encoding of the single BatchedCall tuple, i.e. the planner encoding.
        string calldata = ExecuteBatchedCallSelector + StripHexPrefix(batchedCallPlanner.Encode());
        return new MethodParameters(calldata, planner.Value);
    }

    /// <summary>
    /// ERC-7821 compatible entrypoint for executing batched calls through the contract.
    /// Prefer <see cref="EncodeBatchedCall"/> unless the ERC-7821 entrypoint is required.
    /// </summary>
    public static MethodParameters EncodeErc7821BatchedCall(IReadOnlyList<Call> calls, ExecuteOptions? options = null)
    {
        ModeType mode = GetModeFromOptions(options ?? new ExecuteOptions());
        return EncodeErc7821BatchedCall(calls, mode);
    }

    /// <summary>
    /// ERC-7821 batched call encoding for an explicit mode. Mirrors the upstream body after the mode
    /// is resolved; exposed internally so the invalid-mode guard remains reachable and testable
    /// (upstream reaches it only by mocking <c>getModeFromOptions</c>).
    /// </summary>
    internal static MethodParameters EncodeErc7821BatchedCall(IReadOnlyList<Call> calls, ModeType mode)
    {
        if (mode != ModeType.BATCHED_CALL && mode != ModeType.BATCHED_CALL_CAN_REVERT)
        {
            throw new ArgumentException($"Invalid mode: {mode}");
        }

        var planner = new CallPlanner(calls);
        string executionData = planner.Encode();
        string encoded = EncodeErc7821Execute(mode, executionData);
        return new MethodParameters(encoded, planner.Value);
    }

    /// <summary>
    /// Creates a call to execute a method through a smart wallet on a given chain.
    /// </summary>
    /// <param name="methodParameters">The method parameters to execute.</param>
    /// <param name="chainId">The chain id for the smart wallet (numeric id, <see cref="Core.ChainId"/>, or numeric string).</param>
    public static Call CreateExecute(MethodParameters methodParameters, object chainId)
    {
        string address = Constants.GetSmartWalletAddress(chainId);
        return new Call(address, methodParameters.Calldata, methodParameters.Value);
    }

    /// <summary>Get the mode type from the options.</summary>
    public static ModeType GetModeFromOptions(ExecuteOptions options) =>
        options.RevertOnFailure == true ? ModeType.BATCHED_CALL : ModeType.BATCHED_CALL_CAN_REVERT;

    private static string EncodeErc7821Execute(ModeType mode, string data)
    {
        string modeHex = Constants.ModeToBytes32(mode);
        string encodedParams = AbiParamEncoder.Encode(
            new[] { "bytes32", "bytes" },
            new object?[] { modeHex, data });
        return ExecuteSelector + StripHexPrefix(encodedParams);
    }

    private static string StripHexPrefix(string hex) =>
        hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? hex[2..] : hex;
}
