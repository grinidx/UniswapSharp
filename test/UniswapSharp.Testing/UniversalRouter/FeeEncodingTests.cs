using System.Numerics;
using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.UniversalRouter;
using UniswapSharp.UniversalRouter.Entities.Actions;
using UniswapSharp.UniversalRouter.Utils;
using UniswapSharp.V4.Utils;
using FeeOptions = UniswapSharp.V3.Payments.FeeOptions;
using V4Pool = UniswapSharp.V4.Entities.Pool;
using V4Route = UniswapSharp.V4.Entities.Route<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;
using V4Trade = UniswapSharp.V4.Entities.Trade<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;

namespace UniswapSharp.Testing.UniversalRouter;

// Ported from sdks/universal-router-sdk/test/utils/feeEncoding.test.ts
public class FeeEncodingTests
{
    private static readonly BaseCurrency ETHER = UniswapData.ETHER;
    private static readonly Token USDC = UniswapData.USDC;
    private static readonly string FEE_RECIPIENT = UniswapData.TEST_FEE_RECIPIENT_ADDRESS;
    private static readonly V4Pool ETH_USDC_V4 = UniswapData.MakeV4Pool(ETHER, USDC);

    private static BigInteger Hex(string h) => AbiParamEncoder.ToBigInteger(h);

    // ---- encodeFeeBips ----
    [Theory]
    [InlineData(5, 100, 500)]
    [InlineData(1, 100, 100)]
    [InlineData(3, 1000, 30)]
    [InlineData(100, 100, 10000)]
    [InlineData(1, 3, 3333)]
    public void EncodeFeeBips(int n, int d, int expected) =>
        Assert.Equal(expected, (int)Hex(Numbers.EncodeFeeBips(new Percent(n, d))));

    // ---- encodeFee1e18 ----
    [Fact]
    public void EncodeFee1e18_5Percent() =>
        Assert.Equal((BigInteger.Pow(10, 18) * 5 / 100).ToString(), Hex(Numbers.EncodeFee1e18(new Percent(5, 100))).ToString());

    [Fact]
    public void EncodeFee1e18_1Percent() =>
        Assert.Equal(BigInteger.Pow(10, 16), Hex(Numbers.EncodeFee1e18(new Percent(1, 100))));

    [Fact]
    public void EncodeFee1e18_OneThird() =>
        Assert.Equal("333333333333333333", Hex(Numbers.EncodeFee1e18(new Percent(1, 3))).ToString());

    // ---- PAY_PORTION command byte selection ----
    [Fact]
    public void PayPortion_CommandByteIs06()
    {
        var planner = new RoutePlanner();
        planner.AddCommand(CommandType.PAY_PORTION, new object?[] { "0x0000000000000000000000000000000000000001", "0x0000000000000000000000000000000000000002", (BigInteger)500 });
        Assert.Equal("0x06", planner.Commands);
    }

    [Fact]
    public void PayPortionFullPrecision_CommandByteIs07()
    {
        var planner = new RoutePlanner();
        planner.AddCommand(CommandType.PAY_PORTION_FULL_PRECISION, new object?[] { "0x0000000000000000000000000000000000000001", "0x0000000000000000000000000000000000000002", BigInteger.Pow(10, 16) * 5 });
        Assert.Equal("0x07", planner.Commands);
    }

    // ---- UniswapTrade fee command selection ----
    private static V4Trade EthUsdcTrade(string rawAmount, TradeType type) =>
        V4Trade.FromRoute(new V4Route(new List<V4Pool> { ETH_USDC_V4 }, ETHER, USDC),
            CurrencyAmount<BaseCurrency>.FromRawAmount(type == TradeType.EXACT_INPUT ? ETHER : USDC, BigInteger.Parse(rawAmount)),
            type).GetAwaiter().GetResult();

    private static UniversalRouterCommand? FeeCommand(SwapRouter.MethodParameters mp)
    {
        var parsed = CommandParser.ParseCalldata(mp.Calldata);
        return parsed.Commands.FirstOrDefault(c =>
            c.CommandName is "PAY_PORTION" or "PAY_PORTION_FULL_PRECISION" or "TRANSFER");
    }

    [Fact]
    public void UsesPayPortionBips_WhenUrVersionUndefined()
    {
        var trade = EthUsdcTrade("1000000000000000000", TradeType.EXACT_INPUT);
        var opts = UniswapData.SwapOptions(fee: new FeeOptions { Fee = new Percent(5, 100), Recipient = FEE_RECIPIENT });
        var mp = SwapRouter.SwapCallParameters(UniswapData.BuildTrade(new object[] { trade }), opts);

        var feeCmd = FeeCommand(mp)!;
        Assert.Equal("PAY_PORTION", feeCmd.CommandName);
        Assert.Equal("bips", feeCmd.Params[2].Name);
        Assert.Equal(500, (int)(BigInteger)feeCmd.Params[2].Value!);
    }

    [Fact]
    public void UsesPayPortionBips_WhenUrVersionV2_0()
    {
        var trade = EthUsdcTrade("1000000000000000000", TradeType.EXACT_INPUT);
        var opts = UniswapData.SwapOptions(fee: new FeeOptions { Fee = new Percent(5, 100), Recipient = FEE_RECIPIENT }, urVersion: UniversalRouterVersion.V2_0);
        var mp = SwapRouter.SwapCallParameters(UniswapData.BuildTrade(new object[] { trade }), opts);

        var feeCmd = FeeCommand(mp)!;
        Assert.Equal("PAY_PORTION", feeCmd.CommandName);
        Assert.Equal(500, (int)(BigInteger)feeCmd.Params[2].Value!);
    }

    [Fact]
    public void UsesPayPortionFullPrecision_WhenUrVersionV2_1_1()
    {
        var trade = EthUsdcTrade("1000000000000000000", TradeType.EXACT_INPUT);
        var opts = UniswapData.SwapOptions(fee: new FeeOptions { Fee = new Percent(5, 100), Recipient = FEE_RECIPIENT }, urVersion: UniversalRouterVersion.V2_1_1);
        var mp = SwapRouter.SwapCallParameters(UniswapData.BuildTrade(new object[] { trade }), opts);

        var feeCmd = FeeCommand(mp)!;
        Assert.Equal("PAY_PORTION_FULL_PRECISION", feeCmd.CommandName);
        Assert.Equal("portion", feeCmd.Params[2].Name);
        Assert.Equal(BigInteger.Pow(10, 16) * 5, (BigInteger)feeCmd.Params[2].Value!);
    }

    [Fact]
    public void EncodesCorrectFeeRecipientInFullPrecision()
    {
        var trade = EthUsdcTrade("1000000000000000000", TradeType.EXACT_INPUT);
        var opts = UniswapData.SwapOptions(fee: new FeeOptions { Fee = new Percent(5, 100), Recipient = FEE_RECIPIENT }, urVersion: UniversalRouterVersion.V2_1_1);
        var mp = SwapRouter.SwapCallParameters(UniswapData.BuildTrade(new object[] { trade }), opts);

        var feeCmd = FeeCommand(mp)!;
        Assert.Equal("recipient", feeCmd.Params[1].Name);
        Assert.Equal(FEE_RECIPIENT.ToLowerInvariant(), ((string)feeCmd.Params[1].Value!).ToLowerInvariant());
    }

    [Fact]
    public void UsesTransferForFlatFees()
    {
        var trade = EthUsdcTrade("1000000000000000000", TradeType.EXACT_INPUT);
        var opts = UniswapData.SwapOptions(flatFee: new FlatFeeOptions((BigInteger)50000000, FEE_RECIPIENT), urVersion: UniversalRouterVersion.V2_1_1);
        var mp = SwapRouter.SwapCallParameters(UniswapData.BuildTrade(new object[] { trade }), opts);

        var feeCmd = FeeCommand(mp)!;
        Assert.Equal("TRANSFER", feeCmd.CommandName);
    }

    [Fact]
    public void ExactOutputAdjustsMinWith1e18PrecisionForV2_1_1()
    {
        BigInteger outputUSDC = 1000 * BigInteger.Pow(10, 6);
        BigInteger adjustedOutput = outputUSDC * BigInteger.Pow(10, 18) / (BigInteger.Pow(10, 18) - BigInteger.Pow(10, 16) * 5);
        var trade = EthUsdcTrade(adjustedOutput.ToString(), TradeType.EXACT_OUTPUT);
        var opts = UniswapData.SwapOptions(fee: new FeeOptions { Fee = new Percent(5, 100), Recipient = FEE_RECIPIENT }, urVersion: UniversalRouterVersion.V2_1_1);
        var mp = SwapRouter.SwapCallParameters(UniswapData.BuildTrade(new object[] { trade }), opts);

        Assert.Equal("PAY_PORTION_FULL_PRECISION", FeeCommand(mp)!.CommandName);

        var sweep = CommandParser.ParseCalldata(mp.Calldata).Commands.First(c => c.CommandName == "SWEEP");
        Assert.True((BigInteger)sweep.Params[2].Value! > 0);
    }

    [Fact]
    public void ExactOutputAdjustsMinWithBipsForV2_0()
    {
        BigInteger outputUSDC = 1000 * BigInteger.Pow(10, 6);
        BigInteger adjustedOutput = outputUSDC * 10000 / (10000 - 500);
        var trade = EthUsdcTrade(adjustedOutput.ToString(), TradeType.EXACT_OUTPUT);
        var opts = UniswapData.SwapOptions(fee: new FeeOptions { Fee = new Percent(5, 100), Recipient = FEE_RECIPIENT });
        var mp = SwapRouter.SwapCallParameters(UniswapData.BuildTrade(new object[] { trade }), opts);

        Assert.Equal("PAY_PORTION", FeeCommand(mp)!.CommandName);
        var sweep = CommandParser.ParseCalldata(mp.Calldata).Commands.First(c => c.CommandName == "SWEEP");
        Assert.True((BigInteger)sweep.Params[2].Value! > 0);
    }
}
