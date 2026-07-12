using System.Numerics;
using AwesomeAssertions;
using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.UniswapX.Order;
using UniswapSharp.UniswapX.Trade;

namespace UniswapSharp.Testing.UniswapX;

// Port of uniswapx-sdk src/trade/V3DutchOrderTrade.test.ts.
public class V3DutchOrderTradeTests
{
    private static readonly Token Usdc = new(1, "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48", 6, "USDC");
    private static readonly Token Dai = new(1, "0x6B175474E89094C44Da98b954EedeAC495271d0F", 18, "DAI");
    private const string Zero = "0x0000000000000000000000000000000000000000";
    private static readonly BigInteger NonFeeOutputAmount = BigInteger.Parse("1000000000000000000");
    private static readonly BigInteger NonFeeMinimumAmountOut = BigInteger.Parse("900000000000000000");
    private static long Deadline => DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 1000;

    private static NonlinearDutchDecay Curve(int[] blocks, string[] amounts) => new()
    {
        RelativeBlocks = blocks,
        RelativeAmounts = amounts.Select(BigInteger.Parse).ToList(),
    };

    private static UnsignedV3DutchOrderInfo InOrderInfo(string outToken) => new()
    {
        Deadline = Deadline,
        Reactor = Zero,
        Swapper = Zero,
        Nonce = 10,
        Cosigner = Zero,
        StartingBaseFee = 0,
        AdditionalValidationContract = Zero,
        AdditionalValidationData = "0x",
        Input = new V3DutchInput
        {
            Token = Usdc.Address,
            StartAmount = 1000,
            Curve = Curve(Array.Empty<int>(), Array.Empty<string>()),
            MaxAmount = 1000,
            AdjustmentPerGweiBaseFee = 0,
        },
        Outputs = new List<V3DutchOutput>
        {
            new()
            {
                Token = outToken, StartAmount = NonFeeOutputAmount,
                Curve = Curve(new[] { 21 }, new[] { "100000000000000000" }),
                Recipient = Zero, MinAmount = NonFeeMinimumAmountOut, AdjustmentPerGweiBaseFee = 0,
            },
            new()
            {
                Token = outToken, StartAmount = 1000,
                Curve = Curve(new[] { 21 }, new[] { "100" }),
                Recipient = Zero, MinAmount = 900, AdjustmentPerGweiBaseFee = 0,
            },
        },
    };

    private static UnsignedV3DutchOrderInfo OutOrderInfo() => new()
    {
        Deadline = Deadline,
        Reactor = Zero,
        Swapper = Zero,
        Nonce = 10,
        Cosigner = Zero,
        StartingBaseFee = 0,
        AdditionalValidationContract = Zero,
        AdditionalValidationData = "0x",
        Input = new V3DutchInput
        {
            Token = Usdc.Address,
            StartAmount = 1000,
            Curve = Curve(new[] { 10 }, new[] { "-100" }),
            MaxAmount = 1100,
            AdjustmentPerGweiBaseFee = 0,
        },
        Outputs = new List<V3DutchOutput>
        {
            new() { Token = Dai.Address, StartAmount = NonFeeOutputAmount, Curve = Curve(Array.Empty<int>(), Array.Empty<string>()), Recipient = Zero, MinAmount = NonFeeOutputAmount, AdjustmentPerGweiBaseFee = 0 },
            new() { Token = Dai.Address, StartAmount = 1000, Curve = Curve(Array.Empty<int>(), Array.Empty<string>()), Recipient = Zero, MinAmount = 1000, AdjustmentPerGweiBaseFee = 0 },
        },
    };

    private static V3DutchOrderTrade<BaseCurrency, BaseCurrency> Trade(ExpectedAmounts? expected = null) =>
        new(Usdc, new BaseCurrency[] { Dai }, InOrderInfo(Dai.Address), TradeType.EXACT_INPUT, expected);

    [Fact]
    public void ExactInput_ReturnsRightInputAmount() => Trade().InputAmount.Quotient.ToString().Should().Be("1000");

    [Fact]
    public void ExactInput_ReturnsNonFeeOutputAmount() => Trade().OutputAmount.Quotient.ToString().Should().Be(NonFeeOutputAmount.ToString());

    [Fact]
    public void ExactInput_ReturnsMinimumAmountOut() => Trade().MinimumAmountOut().Quotient.ToString().Should().Be(NonFeeMinimumAmountOut.ToString());

    [Fact]
    public void ExactOutput_ReturnsMaximumAmountIn()
    {
        var trade = new V3DutchOrderTrade<BaseCurrency, BaseCurrency>(Usdc, new BaseCurrency[] { Dai }, OutOrderInfo(), TradeType.EXACT_OUTPUT);
        trade.MaximumAmountIn().Quotient.ToString().Should().Be("1100");
    }

    [Fact]
    public void WorksForNativeOutputTrades()
    {
        var info = InOrderInfo(NativeAssets.ETH.ToString());
        info.Outputs.RemoveAt(1);
        var trade = new V3DutchOrderTrade<BaseCurrency, BaseCurrency>(Usdc, new BaseCurrency[] { Ether.OnChain(1) }, info, TradeType.EXACT_INPUT);
        trade.OutputAmount.Currency.Should().Be(Ether.OnChain(1));
    }

    [Fact]
    public void WorksForNativeOutputTradesWithZeroAddress()
    {
        var info = InOrderInfo(Zero);
        info.Outputs.RemoveAt(1);
        var trade = new V3DutchOrderTrade<BaseCurrency, BaseCurrency>(Usdc, new BaseCurrency[] { Ether.OnChain(1) }, info, TradeType.EXACT_INPUT);
        trade.OutputAmount.Currency.Should().Be(Ether.OnChain(1));
    }

    [Fact]
    public void UsesExpectedAmountInWhenProvided() =>
        Trade(new ExpectedAmounts("800", "900")).InputAmount.Quotient.ToString().Should().Be("800");

    [Fact]
    public void UsesExpectedAmountOutWhenProvided() =>
        Trade(new ExpectedAmounts("800", "900")).OutputAmount.Quotient.ToString().Should().Be("900");

    [Fact]
    public void FallsBackToOrderAmountsWhenExpectedNotProvided()
    {
        var trade = Trade();
        trade.InputAmount.Quotient.ToString().Should().Be("1000");
        trade.OutputAmount.Quotient.ToString().Should().Be(NonFeeOutputAmount.ToString());
    }
}
