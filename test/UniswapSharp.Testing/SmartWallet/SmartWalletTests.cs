using System.Numerics;
using UniswapSharp.Core;
using UniswapSharp.SmartWallet;
using UniswapSharp.SmartWallet.Utils;
using UniswapSharp.V4.Utils;
using static UniswapSharp.Testing.SmartWallet.SwTestConstants;
using Call = UniswapSharp.SmartWallet.Call;
using Constants = UniswapSharp.SmartWallet.Constants;
using SmartWalletSdk = UniswapSharp.SmartWallet.SmartWallet;

namespace UniswapSharp.Testing.SmartWallet;

// Ported from sdks/smart-wallet-sdk/src/smartWallet.test.ts. viem's decode-and-inspect assertions
// are reproduced with AbiParamDecoder; encodings are also pinned to exact hex where practical.
public class SmartWalletTests
{
    private const string ADDR1 = "0x1111111111111111111111111111111111111111";
    private const string ADDR2 = "0x2222222222222222222222222222222222222222";

    private const string ExecuteUserOpSelector = "0x8dd7712f";
    private const string ExecuteBatchedCallSelector = "0x99e1d016";
    private const string ExecuteSelector = "0xe9ae5c53";

    // ---- encodeUserOp ----

    [Fact]
    public void EncodeUserOp_SingleCall_ExactCalldataAndFields()
    {
        var calls = new List<Call> { new(ADDR1, "0x1234", 0) };

        var result = SmartWalletSdk.EncodeUserOp(calls);

        string expected = ExecuteUserOpSelector
            + Word("20")            // offset to BatchedCall tuple
            + Word("40")            // offset to calls[]
            + Word("1")             // revertOnFailure defaults to true
            + Word("1")             // calls length
            + Word("20")            // offset to element 0
            + AddrWord(ADDR1)       // to
            + Word("0")             // value
            + Word("60")            // offset to bytes
            + Word("2")             // bytes length
            + RightPad("1234");

        Assert.Equal(expected, result.Calldata);
        Assert.StartsWith(ExecuteUserOpSelector, result.Calldata);
        Assert.Equal(BigInteger.Zero, result.Value);

        var (decodedCalls, revert) = DecodeUserOp(result.Calldata);
        Assert.Single(decodedCalls);
        AssertCall(decodedCalls[0], ADDR1, "0x1234", 0);
        Assert.True(revert);
    }

    [Fact]
    public void EncodeUserOp_MultipleCalls_SumsValues()
    {
        var calls = new List<Call>
        {
            new(ADDR1, "0x1234", 100),
            new(ADDR2, "0x5678", 200),
        };

        var result = SmartWalletSdk.EncodeUserOp(calls);
        Assert.Equal(new BigInteger(300), result.Value);

        var (decodedCalls, _) = DecodeUserOp(result.Calldata);
        Assert.Equal(2, decodedCalls.Count);
        AssertCall(decodedCalls[0], ADDR1, "0x1234", 100);
        AssertCall(decodedCalls[1], ADDR2, "0x5678", 200);
    }

    [Fact]
    public void EncodeUserOp_RevertOnFailureFalse()
    {
        var calls = new List<Call> { new(ADDR1, "0x1234", 0) };

        var result = SmartWalletSdk.EncodeUserOp(calls, new ExecuteOptions(RevertOnFailure: false));

        var (_, revert) = DecodeUserOp(result.Calldata);
        Assert.False(revert);
    }

    [Fact]
    public void EncodeUserOp_ExcludesChainIdFromCalldata()
    {
        var withChainId = new List<Call> { new(ADDR1, "0x1234", 50, ChainId: ChainId.SEPOLIA) };
        var withoutChainId = new List<Call> { new(ADDR1, "0x1234", 50) };

        var a = SmartWalletSdk.EncodeUserOp(withChainId);
        var b = SmartWalletSdk.EncodeUserOp(withoutChainId);

        Assert.Equal(b.Calldata, a.Calldata);
        Assert.Equal(new BigInteger(50), a.Value);
    }

    // ---- encodeBatchedCall ----

    [Fact]
    public void EncodeBatchedCall_EncodesCorrectly()
    {
        var calls = new List<Call>
        {
            new(ADDR1, "0x1234", 0, ChainId: ChainId.SEPOLIA),
            new(ADDR2, "0x5678", 1, ChainId: ChainId.SEPOLIA),
        };

        var result = SmartWalletSdk.EncodeBatchedCall(calls, new ExecuteOptions(RevertOnFailure: false));

        Assert.StartsWith(ExecuteBatchedCallSelector, result.Calldata);
        Assert.Equal(BigInteger.One, result.Value);

        var (decodedCalls, revert) = DecodeBatchedCall(result.Calldata);
        Assert.Equal(2, decodedCalls.Count);
        Assert.False(revert);
    }

    // ---- encodeERC7821BatchedCall ----

    [Fact]
    public void EncodeErc7821BatchedCall_DefaultMode_ExactHex()
    {
        var calls = new List<Call> { new(TEST_ADDRESS_1, TEST_DATA_1, TEST_VALUE_1) };

        var result = SmartWalletSdk.EncodeErc7821BatchedCall(calls);

        string executionData =
            Word("20") + Word("1") + Word("20") + Word("0") + Word("64") + Word("60") + Word("3") + RightPad("123456");
        string expected = ExecuteSelector
            + Constants.MODE_BATCHED_CALL_CAN_REVERT[2..]   // default (revertOnFailure unset) → can-revert mode
            + Word("40")                                    // offset to executionData bytes
            + Word("100")                                   // executionData length = 256 bytes
            + executionData;

        Assert.Equal(expected, result.Calldata);
        Assert.Equal(new BigInteger(100), result.Value);
    }

    [Fact]
    public void EncodeErc7821BatchedCall_RevertOnFailureTrue_UsesBatchedCallMode()
    {
        var calls = new List<Call> { new(TEST_ADDRESS_1, TEST_DATA_1, TEST_VALUE_1) };

        var result = SmartWalletSdk.EncodeErc7821BatchedCall(calls, new ExecuteOptions(RevertOnFailure: true));

        string executionData =
            Word("20") + Word("1") + Word("20") + Word("0") + Word("64") + Word("60") + Word("3") + RightPad("123456");
        string expected = ExecuteSelector
            + Constants.MODE_BATCHED_CALL[2..]              // revertOnFailure → batched-call mode
            + Word("40")
            + Word("100")
            + executionData;

        Assert.Equal(expected, result.Calldata);
        Assert.Equal(new BigInteger(100), result.Value);
    }

    [Fact]
    public void EncodeErc7821BatchedCall_ThrowsForUnsupportedMode()
    {
        // Upstream exercises this by mocking getModeFromOptions to return an invalid mode; here the
        // internal explicit-mode overload lets the same guard be reached with an out-of-range enum.
        var calls = new List<Call> { new(ADDR1, "0x1234", 0) };
        Assert.Throws<ArgumentException>(() => SmartWalletSdk.EncodeErc7821BatchedCall(calls, (ModeType)99));
    }

    // ---- createExecute ----

    [Fact]
    public void CreateExecute_CreatesExecuteCallForChain()
    {
        var methodParams = new MethodParameters(ExecuteSelector, 0);

        var call = SmartWalletSdk.CreateExecute(methodParams, ChainId.SEPOLIA);

        Assert.Equal(Constants.SmartWalletAddresses[(int)ChainId.SEPOLIA], call.To);
        Assert.Equal(ExecuteSelector, call.Data);
        Assert.Equal(BigInteger.Zero, call.Value);
    }

    [Theory]
    [InlineData("__proto__")]
    [InlineData("constructor")]
    [InlineData("forty-two")]
    public void CreateExecute_ThrowsForDangerousOrNonNumericStringChainId(string chainId)
    {
        var methodParams = new MethodParameters(ExecuteSelector, 0);
        var ex = Assert.Throws<ArgumentException>(() => SmartWalletSdk.CreateExecute(methodParams, chainId));
        Assert.Contains($"Smart wallet not found for chainId: {chainId}", ex.Message);
    }

    [Fact]
    public void CreateExecute_ThrowsForUnsupportedNumericChainId()
    {
        var methodParams = new MethodParameters(ExecuteSelector, 0);
        var ex = Assert.Throws<ArgumentException>(() => SmartWalletSdk.CreateExecute(methodParams, 1337));
        Assert.Contains("Smart wallet not found for chainId: 1337", ex.Message);
    }

    // ---- getModeFromOptions ----

    [Fact]
    public void GetModeFromOptions_RevertOnFailureTrue_ReturnsBatchedCall()
    {
        Assert.Equal(ModeType.BATCHED_CALL, SmartWalletSdk.GetModeFromOptions(new ExecuteOptions(RevertOnFailure: true)));
    }

    [Fact]
    public void GetModeFromOptions_RevertOnFailureFalse_ReturnsBatchedCallCanRevert()
    {
        Assert.Equal(
            ModeType.BATCHED_CALL_CAN_REVERT,
            SmartWalletSdk.GetModeFromOptions(new ExecuteOptions(RevertOnFailure: false)));
    }

    [Fact]
    public void GetModeFromOptions_Unset_ReturnsBatchedCallCanRevert()
    {
        Assert.Equal(ModeType.BATCHED_CALL_CAN_REVERT, SmartWalletSdk.GetModeFromOptions(new ExecuteOptions()));
    }

    // ---- helpers ----

    private static (List<object?> Calls, bool Revert) DecodeUserOp(string calldata)
    {
        string argsData = "0x" + calldata[10..]; // strip the executeUserOp selector
        return DecodeBatchedArgs(argsData);
    }

    private static (List<object?> Calls, bool Revert) DecodeBatchedCall(string calldata)
    {
        string argsData = "0x" + calldata[10..]; // strip the execute selector
        return DecodeBatchedArgs(argsData);
    }

    private static (List<object?> Calls, bool Revert) DecodeBatchedArgs(string argsData)
    {
        var decoded = AbiParamDecoder.Decode(new[] { BatchedCallPlanner.BatchedCallAbiParams }, argsData);
        var tuple = (List<object?>)decoded[0]!;
        return ((List<object?>)tuple[0]!, (bool)tuple[1]!);
    }

    private static void AssertCall(object? decodedCall, string to, string data, BigInteger value)
    {
        var call = (List<object?>)decodedCall!;
        Assert.Equal(to.ToLowerInvariant(), ((string)call[0]!).ToLowerInvariant());
        Assert.Equal(value, call[1]);
        Assert.Equal(data, call[2]);
    }
}
