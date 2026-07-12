using System.Numerics;
using System.Text.RegularExpressions;
using UniswapSharp.UniversalRouter.Entities.Actions;
using UniswapSharp.UniversalRouter.Utils;

namespace UniswapSharp.Testing.UniversalRouter;

// Ported from sdks/universal-router-sdk/test/unit/across.test.ts
public class AcrossTests
{
    private const string WETH_MAINNET = "0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2";
    private const string WETH_OPTIMISM = "0x4200000000000000000000000000000000000006";
    private const string USDC_MAINNET = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";

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
}
