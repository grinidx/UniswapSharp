using System.Numerics;
using UniswapSharp.SmartWallet.Utils;
using UniswapSharp.V4.Utils;
using static UniswapSharp.Testing.SmartWallet.SwTestConstants;
using Call = UniswapSharp.SmartWallet.Call;

namespace UniswapSharp.Testing.SmartWallet;

// Ported 1:1 from sdks/smart-wallet-sdk/src/utils/batchedCallPlanner.test.ts.
public class BatchedCallPlannerTests
{
    [Fact]
    public void Constructor_DefaultsRevertOnFailureToTrue()
    {
        var callPlanner = new CallPlanner();
        var batched = new BatchedCallPlanner(callPlanner);

        Assert.Same(callPlanner, batched.CallPlanner);
        Assert.True(batched.RevertOnFailure);
    }

    [Fact]
    public void Constructor_UsesProvidedRevertOnFailure()
    {
        var callPlanner = new CallPlanner();
        var batched = new BatchedCallPlanner(callPlanner, false);

        Assert.Same(callPlanner, batched.CallPlanner);
        Assert.False(batched.RevertOnFailure);
    }

    [Fact]
    public void Value_ReturnsUnderlyingCallPlannerValue()
    {
        var callPlanner = new CallPlanner(new List<Call>
        {
            new(TEST_ADDRESS_1, TEST_DATA_1, TEST_VALUE_1),
            new(TEST_ADDRESS_1, TEST_DATA_2, TEST_VALUE_2),
        });
        var batched = new BatchedCallPlanner(callPlanner);
        Assert.Equal(new BigInteger(300), batched.Value);
    }

    [Fact]
    public void Value_ReturnsZeroWhenNoCalls()
    {
        var batched = new BatchedCallPlanner(new CallPlanner());
        Assert.Equal(BigInteger.Zero, batched.Value);
    }

    [Fact]
    public void Add_AddsToUnderlyingCallPlanner()
    {
        var callPlanner = new CallPlanner();
        var batched = new BatchedCallPlanner(callPlanner);
        batched.Add(TEST_ADDRESS_1, TEST_VALUE_1, TEST_DATA_1);

        Assert.Equal(new List<Call> { new(TEST_ADDRESS_1, TEST_DATA_1, TEST_VALUE_1) }, callPlanner.Calls);
    }

    [Fact]
    public void Add_ReturnsBatchedPlannerForChaining()
    {
        var batched = new BatchedCallPlanner(new CallPlanner());
        var result = batched.Add(TEST_ADDRESS_1, TEST_VALUE_1, TEST_DATA_1);
        Assert.Same(batched, result);
    }

    [Fact]
    public void Add_AllowsChainingMultipleAddCalls()
    {
        var callPlanner = new CallPlanner();
        var batched = new BatchedCallPlanner(callPlanner);
        batched
            .Add(TEST_ADDRESS_1, TEST_VALUE_1, TEST_DATA_1)
            .Add(TEST_ADDRESS_1, TEST_VALUE_2, TEST_DATA_2);

        Assert.Equal(new List<Call>
        {
            new(TEST_ADDRESS_1, TEST_DATA_1, TEST_VALUE_1),
            new(TEST_ADDRESS_1, TEST_DATA_2, TEST_VALUE_2),
        }, callPlanner.Calls);
    }

    [Fact]
    public void Encode_RevertOnFailureTrue_ExactHex()
    {
        var callPlanner = new CallPlanner();
        callPlanner.Add(TEST_ADDRESS_1, TEST_VALUE_1, TEST_DATA_1);
        var batched = new BatchedCallPlanner(callPlanner);

        string expected = "0x"
            + Word("20")            // offset to the BatchedCall tuple
            + Word("40")            // offset to calls[] within the tuple
            + Word("1")             // revertOnFailure = true
            + Word("1")             // calls length
            + Word("20")            // offset to element 0
            + Word("0")             // to = zeroAddress
            + Word("64")            // value = 100
            + Word("60")            // offset to bytes
            + Word("3")             // bytes length
            + RightPad("123456");

        Assert.Equal(expected, batched.Encode());

        var decoded = DecodeBatched(batched.Encode());
        Assert.True(decoded.Revert);
        Assert.Single(decoded.Calls);
    }

    [Fact]
    public void Encode_RevertOnFailureFalse_ExactHex()
    {
        var callPlanner = new CallPlanner();
        callPlanner.Add(TEST_ADDRESS_1, TEST_VALUE_1, TEST_DATA_1);
        var batched = new BatchedCallPlanner(callPlanner, false);

        string expected = "0x"
            + Word("20")
            + Word("40")
            + Word("0")             // revertOnFailure = false
            + Word("1")
            + Word("20")
            + Word("0")
            + Word("64")
            + Word("60")
            + Word("3")
            + RightPad("123456");

        Assert.Equal(expected, batched.Encode());
        Assert.False(DecodeBatched(batched.Encode()).Revert);
    }

    [Fact]
    public void Encode_MultipleCalls_ExactHex()
    {
        var callPlanner = new CallPlanner();
        callPlanner
            .Add(TEST_ADDRESS_1, TEST_VALUE_1, TEST_DATA_1)
            .Add(TEST_ADDRESS_1, TEST_VALUE_2, TEST_DATA_2);
        var batched = new BatchedCallPlanner(callPlanner);

        string expected = "0x"
            + Word("20")            // offset to tuple
            + Word("40")            // offset to calls[]
            + Word("1")             // revertOnFailure = true
            + Word("2")             // calls length
            + Word("40")            // offset to element 0
            + Word("e0")            // offset to element 1 (0x40 + 5 words)
            + Word("0") + Word("64") + Word("60") + Word("3") + RightPad("123456")               // element 0
            + Word("0") + Word("c8") + Word("60") + Word("8") + RightPad("abcdef0123456789");    // element 1

        Assert.Equal(expected, batched.Encode());

        var decoded = DecodeBatched(batched.Encode());
        Assert.True(decoded.Revert);
        Assert.Equal(2, decoded.Calls.Count);
    }

    [Fact]
    public void Encode_EmptyCalls_ExactHex()
    {
        var batched = new BatchedCallPlanner(new CallPlanner());

        string expected = "0x"
            + Word("20")            // offset to tuple
            + Word("40")            // offset to calls[]
            + Word("1")             // revertOnFailure = true
            + Word("0");            // calls length = 0

        Assert.Equal(expected, batched.Encode());

        var decoded = DecodeBatched(batched.Encode());
        Assert.True(decoded.Revert);
        Assert.Empty(decoded.Calls);
    }

    private static (List<object?> Calls, bool Revert) DecodeBatched(string hex)
    {
        var decoded = AbiParamDecoder.Decode(new[] { BatchedCallPlanner.BatchedCallAbiParams }, hex);
        var tuple = (List<object?>)decoded[0]!;
        return ((List<object?>)tuple[0]!, (bool)tuple[1]!);
    }
}
