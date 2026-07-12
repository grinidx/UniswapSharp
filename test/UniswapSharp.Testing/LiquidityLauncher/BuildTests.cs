using System.Numerics;
using UniswapSharp.Core.Utils;
using UniswapSharp.LiquidityLauncher;
using UniswapSharp.V3.Utils;
using UniswapSharp.V4.Utils;

namespace UniswapSharp.Testing.LiquidityLauncher;

// Ported from sdks/liquidity-launcher-sdk/src/build.test.ts.
public class BuildTests
{
    private static readonly string Launcher = AddressValidator.GetAddress("0x00004c4ccc709Ef590F7C81102C0689F0263D4e9");
    private static readonly string Token = AddressValidator.GetAddress("0x15d0e0c55a3e7ee67152ad7e89acf164253ff68d");
    private const string Salt = "0x0000000000000000000000000000000000000000000000000000000000000000";

    private static readonly Distribution TheDistribution = new(
        AddressValidator.GetAddress("0x824a3ecde463dd45cc156b64cefa132596c9a000"),
        BigInteger.Parse("100000000000000"),
        "0x");

    // Function selectors of the launcher subcalls.
    private const string DepositSelector = "0x44599bc5";
    private const string DistributeSelector = "0xb6982b48";

    private static readonly string MulticallSelector = "0x" + AbiFunctionEncoder.Selector("multicall(bytes[])");

    // Decodes a `multicall(bytes[])` calldata blob into its inner subcall hex strings.
    private static List<string> DecodeMulticallCalls(string data)
    {
        Assert.StartsWith(MulticallSelector, data);
        string argsHex = "0x" + data[10..];
        var decoded = AbiParamDecoder.Decode(new[] { "bytes[]" }, argsHex);
        var calls = (List<object?>)decoded[0]!;
        return calls.Select(c => (string)c!).ToList();
    }

    [Fact]
    public void BuildLaunchTransactions_ExistingTokenLaunchIsApprovalsPlusOneMulticall()
    {
        var approval = new TransactionRequest(Token, "0xabcdef", BigInteger.Zero);
        var txs = Build.BuildLaunchTransactions(new BuildLaunchParams(
            LiquidityLauncher: Launcher,
            Token: Token,
            Salt: Salt,
            Acquire: new DepositTokenAcquisition(BigInteger.Parse("100000000000000")),
            Distributions: new[] { TheDistribution },
            Approvals: new[] { approval }));

        Assert.Equal(2, txs.Count);
        Assert.Equal(approval, txs[0]);

        var multicall = txs[1];
        Assert.Equal(Launcher, multicall.To);
        Assert.Equal(BigInteger.Zero, multicall.Value);

        var calls = DecodeMulticallCalls(multicall.Data);
        Assert.StartsWith(DepositSelector, calls[0]);
        Assert.StartsWith(DistributeSelector, calls[1]);
    }

    [Fact]
    public void BuildLaunchTransactions_NewTokenLaunchIsASingleMulticallNoApprovals()
    {
        var txs = Build.BuildLaunchTransactions(new BuildLaunchParams(
            LiquidityLauncher: Launcher,
            Token: Token,
            Salt: Salt,
            Acquire: new CreateTokenAcquisition(new CreateTokenArgs(
                Factory: "0x0000000000000000000000000000000000000000",
                Name: "Test",
                Symbol: "TST",
                Decimals: 18,
                InitialSupply: new BigInteger(1_000_000),
                Recipient: Launcher,
                TokenData: "0x")),
            Distributions: new[] { TheDistribution }));

        Assert.Single(txs);
        var calls = DecodeMulticallCalls(txs[0].Data);
        Assert.Equal(2, calls.Count);
    }
}
