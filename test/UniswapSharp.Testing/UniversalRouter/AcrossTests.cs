using System.Numerics;
using System.Text.RegularExpressions;
using UniswapSharp.UniversalRouter.Entities.Actions;
using UniswapSharp.UniversalRouter.Utils;
using UniswapSharp.V4.Utils;

namespace UniswapSharp.Testing.UniversalRouter;

// Ported from sdks/universal-router-sdk/test/unit/across.test.ts
public class AcrossTests
{
    private const string WETH_MAINNET = "0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2";
    private const string WETH_OPTIMISM = "0x4200000000000000000000000000000000000006";
    private const string USDC_MAINNET = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";

    private const string ACROSS_V4_DEPOSIT_V3_TUPLE =
        "(address depositor,address recipient,address inputToken,address outputToken," +
        "uint256 inputAmount,uint256 outputAmount,uint256 destinationChainId,address exclusiveRelayer," +
        "uint32 quoteTimestamp,uint32 fillDeadline,uint32 exclusivityDeadline,bytes message,bool useNative)";

    private static AcrossV4DepositV3Params Params(object inputAmount, bool useNative = false) => new()
    {
        Depositor = "0x0000000000000000000000000000000000000001",
        Recipient = "0x0000000000000000000000000000000000000001",
        InputToken = WETH_MAINNET,
        OutputToken = WETH_OPTIMISM,
        InputAmount = inputAmount,
        OutputAmount = BigInteger.Parse("990000000000000000"),
        DestinationChainId = 10,
        ExclusiveRelayer = "0x0000000000000000000000000000000000000000",
        QuoteTimestamp = 1_700_000_000,
        FillDeadline = 1_700_003_600,
        ExclusivityDeadline = 0,
        Message = "0x",
        UseNative = useNative,
    };

    [Fact]
    public void AddAcrossBridge_AddsBridgeCommand()
    {
        var planner = new RoutePlanner();
        planner.AddAcrossBridge(Params(BigInteger.Parse("1000000000000000000")));
        Assert.Contains("40", planner.Commands);
        Assert.Single(planner.Inputs);
    }

    [Fact]
    public void EncodesSwapPlusBridgeWithContractBalance()
    {
        var planner = new RoutePlanner();
        planner.AddCommand(CommandType.WRAP_ETH, new object?[] { "0x0000000000000000000000000000000000000002", BigInteger.Parse("1000000000000000000") });
        planner.AddAcrossBridge(Params(Constants.CONTRACT_BALANCE));
        Assert.Equal("0x0b40", planner.Commands);
        Assert.Equal(2, planner.Inputs.Count);
    }

    [Fact]
    public void SupportsNativeEthBridging()
    {
        var planner = new RoutePlanner();
        planner.AddAcrossBridge(Params(BigInteger.Parse("1000000000000000000"), useNative: true));
        Assert.Contains("40", planner.Commands);
        Assert.Single(planner.Inputs);
    }

    [Fact]
    public void EncodesBridgeCommandInputAsHex()
    {
        var planner = new RoutePlanner();
        planner.AddCommand(CommandType.WRAP_ETH, new object?[] { "0x0000000000000000000000000000000000000002", BigInteger.Parse("1000000000000000000") });
        planner.AddAcrossBridge(Params(Constants.CONTRACT_BALANCE));
        Assert.Equal("0x0b40", planner.Commands);
        Assert.Equal(2, planner.Inputs.Count);
        Assert.Matches(new Regex("^0x[0-9a-f]+$"), planner.Inputs[1]);
    }

    [Fact]
    public void SupportsMultipleBridges()
    {
        var planner = new RoutePlanner();
        planner.AddAcrossBridge(Params(BigInteger.Parse("500000000000000000")));
        var bridge2 = new AcrossV4DepositV3Params
        {
            Depositor = "0x0000000000000000000000000000000000000001",
            Recipient = "0x0000000000000000000000000000000000000001",
            InputToken = USDC_MAINNET,
            OutputToken = "0x7F5c764cBc14f9669B88837ca1490cCa17c31607",
            InputAmount = BigInteger.Parse("500000000"),
            OutputAmount = BigInteger.Parse("495000000"),
            DestinationChainId = 10,
            ExclusiveRelayer = "0x0000000000000000000000000000000000000000",
            QuoteTimestamp = 1_700_000_000,
            FillDeadline = 1_700_003_600,
            ExclusivityDeadline = 0,
            Message = "0x",
            UseNative = false,
        };
        planner.AddAcrossBridge(bridge2);
        Assert.Equal("0x4040", planner.Commands);
        Assert.Equal(2, planner.Inputs.Count);
    }

    // ---- command input encoding matches the contract decoder (upstream #690) ----
    //
    // ChainedActions.sol reads the command input with
    // `abi.decode(input, (AcrossV4DepositV3Params))` -- a SINGLE tuple with a dynamic
    // member (`bytes message`), so the encoding must be offset-prefixed. A flat
    // 13-value encoding of the same fields does not decode; the dispatcher reverts
    // with empty data.

    private static AcrossV4DepositV3Params DecoderParams() => new()
    {
        Depositor = "0x0000000000000000000000000000000000000001",
        Recipient = "0x0000000000000000000000000000000000000002",
        InputToken = WETH_MAINNET,
        OutputToken = WETH_OPTIMISM,
        InputAmount = Constants.CONTRACT_BALANCE,
        OutputAmount = BigInteger.Parse("990000000000000000"),
        DestinationChainId = 10,
        ExclusiveRelayer = "0x0000000000000000000000000000000000000000",
        QuoteTimestamp = 1_700_000_000,
        FillDeadline = 1_700_003_600,
        ExclusivityDeadline = 0,
        Message = "0x1234",
        UseNative = false,
    };

    [Fact]
    public void AcrossInput_DecodesWithSingleTupleSemantics()
    {
        var planner = new RoutePlanner();
        planner.AddAcrossBridge(DecoderParams());

        // Mirrors the contract's `abi.decode(input, (AcrossV4DepositV3Params))`.
        var decoded = (List<object?>)AbiParamDecoder.Decode(
            new[] { ACROSS_V4_DEPOSIT_V3_TUPLE }, planner.Inputs[0])[0]!;

        Assert.Equal("0x0000000000000000000000000000000000000001", ((string)decoded[0]!).ToLowerInvariant());
        Assert.Equal("0x0000000000000000000000000000000000000002", ((string)decoded[1]!).ToLowerInvariant());
        Assert.Equal(WETH_MAINNET.ToLowerInvariant(), ((string)decoded[2]!).ToLowerInvariant());
        Assert.Equal(WETH_OPTIMISM.ToLowerInvariant(), ((string)decoded[3]!).ToLowerInvariant());
        Assert.Equal(Constants.CONTRACT_BALANCE, (BigInteger)decoded[4]!);
        Assert.Equal(BigInteger.Parse("990000000000000000"), (BigInteger)decoded[5]!);
        Assert.Equal(new BigInteger(10), (BigInteger)decoded[6]!);
        Assert.Equal("0x0000000000000000000000000000000000000000", ((string)decoded[7]!).ToLowerInvariant());
        Assert.Equal(new BigInteger(1_700_000_000), (BigInteger)decoded[8]!);
        Assert.Equal(new BigInteger(1_700_003_600), (BigInteger)decoded[9]!);
        Assert.Equal(BigInteger.Zero, (BigInteger)decoded[10]!);
        Assert.Equal("0x1234", ((string)decoded[11]!).ToLowerInvariant());
        Assert.Equal(false, decoded[12]);
    }

    [Fact]
    public void AcrossInput_IsOffsetPrefixedSingleDynamicTuple_NotAFlatParameterList()
    {
        var planner = new RoutePlanner();
        planner.AddAcrossBridge(DecoderParams());

        // Word 0 of a single dynamic-tuple encoding is the offset to the tuple body (0x20).
        // The old flat encoding put the depositor address here instead.
        string input = planner.Inputs[0];
        string word0 = input.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? input.Substring(2, 64)
            : input.Substring(0, 64);

        Assert.Equal(32, (int)BigInteger.Parse("0" + word0, System.Globalization.NumberStyles.HexNumber));
    }
}
