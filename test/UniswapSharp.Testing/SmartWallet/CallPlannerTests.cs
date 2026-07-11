using System.Numerics;
using UniswapSharp.SmartWallet.Utils;
using UniswapSharp.V4.Utils;
using static UniswapSharp.Testing.SmartWallet.SwTestConstants;
using Call = UniswapSharp.SmartWallet.Call;

namespace UniswapSharp.Testing.SmartWallet;

// Ported 1:1 from sdks/smart-wallet-sdk/src/utils/callPlanner.test.ts.
public class CallPlannerTests
{
    [Fact]
    public void Constructor_InitializesWithEmptyArray()
    {
        var planner = new CallPlanner();
        Assert.Empty(planner.Calls);
    }

    [Fact]
    public void Constructor_InitializesWithProvidedArray()
    {
        var calls = new List<Call>
        {
            new(TEST_ADDRESS_1, TEST_DATA_1, TEST_VALUE_1),
            new(TEST_ADDRESS_1, TEST_DATA_2, TEST_VALUE_2),
        };
        var planner = new CallPlanner(calls);
        Assert.Equal(calls, planner.Calls);
    }

    [Fact]
    public void Value_ReturnsZeroWhenNoCalls()
    {
        var planner = new CallPlanner();
        Assert.Equal(BigInteger.Zero, planner.Value);
    }

    [Fact]
    public void Value_SumsTheValuesOfAllCalls()
    {
        var planner = new CallPlanner(new List<Call>
        {
            new(TEST_ADDRESS_1, TEST_DATA_1, TEST_VALUE_1),
            new(TEST_ADDRESS_1, TEST_DATA_2, TEST_VALUE_2),
        });
        Assert.Equal(new BigInteger(300), planner.Value);
    }

    // Upstream's "should handle undefined values as 0" case is omitted: Call.Value is a
    // non-nullable BigInteger in C#, so an undefined value is not representable.

    [Fact]
    public void Encode_AbiEncodesTheCalls_ExactHex()
    {
        var planner = new CallPlanner(new List<Call>
        {
            new(TEST_ADDRESS_1, TEST_DATA_1, TEST_VALUE_1),
        });

        // (address,uint256,bytes)[] with one dynamic tuple element.
        string expected = "0x"
            + Word("20")            // offset to the array
            + Word("1")             // array length
            + Word("20")            // offset to element 0 (within the array body)
            + Word("0")             // to = zeroAddress
            + Word("64")            // value = 100
            + Word("60")            // offset to bytes (within the tuple)
            + Word("3")             // bytes length
            + RightPad("123456");   // bytes payload

        Assert.Equal(expected, planner.Encode());
    }

    [Fact]
    public void Encode_RoundTripsThroughDecode()
    {
        var planner = new CallPlanner(new List<Call>
        {
            new(TEST_ADDRESS_1, TEST_DATA_1, TEST_VALUE_1),
        });

        var decoded = AbiParamDecoder.Decode(new[] { CallPlanner.CallAbiParams }, planner.Encode());
        var calls = (List<object?>)decoded[0]!;
        Assert.Single(calls);
        var call = (List<object?>)calls[0]!;
        Assert.Equal(TEST_ADDRESS_1, call[0]);
        Assert.Equal(TEST_VALUE_1, call[1]);
        Assert.Equal(TEST_DATA_1, call[2]);
    }

    [Fact]
    public void Encode_ThrowsWhenNoCalls()
    {
        var planner = new CallPlanner();
        var ex = Assert.Throws<InvalidOperationException>(() => planner.Encode());
        Assert.Equal("No calls to encode", ex.Message);
    }

    [Fact]
    public void Add_AddsANewCall()
    {
        var planner = new CallPlanner();
        planner.Add(TEST_ADDRESS_1, TEST_VALUE_1, TEST_DATA_1);
        Assert.Equal(new List<Call> { new(TEST_ADDRESS_1, TEST_DATA_1, TEST_VALUE_1) }, planner.Calls);
    }

    [Fact]
    public void Add_AddsANewCallWithBigIntegerValue()
    {
        var planner = new CallPlanner();
        planner.Add(TEST_ADDRESS_1, 100, TEST_DATA_1);
        Assert.Equal(new List<Call> { new(TEST_ADDRESS_1, TEST_DATA_1, 100) }, planner.Calls);
    }

    [Fact]
    public void Add_ReturnsThePlannerForChaining()
    {
        var planner = new CallPlanner();
        var result = planner.Add(TEST_ADDRESS_1, TEST_VALUE_1, TEST_DATA_1);
        Assert.Same(planner, result);
    }

    [Fact]
    public void Add_AllowsChainingMultipleAddCalls()
    {
        var planner = new CallPlanner();
        planner
            .Add(TEST_ADDRESS_1, TEST_VALUE_1, TEST_DATA_1)
            .Add(TEST_ADDRESS_1, TEST_VALUE_2, TEST_DATA_2);

        Assert.Equal(new List<Call>
        {
            new(TEST_ADDRESS_1, TEST_DATA_1, TEST_VALUE_1),
            new(TEST_ADDRESS_1, TEST_DATA_2, TEST_VALUE_2),
        }, planner.Calls);
    }
}
