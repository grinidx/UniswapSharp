using System.Numerics;
using AwesomeAssertions;
using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.UniswapX.Order;
using UniswapSharp.UniswapX.Trade;

namespace UniswapSharp.Testing.UniswapX;

// Port of uniswapx-sdk src/trade/V2DutchOrderTrade.test.ts.
public class V2DutchOrderTradeTests
{
    private static readonly Token Usdc = new(1, "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48", 6, "USDC");
    private static readonly Token Dai = new(1, "0x6B175474E89094C44Da98b954EedeAC495271d0F", 18, "DAI");
    private const string Zero = "0x0000000000000000000000000000000000000000";
    private static readonly BigInteger NonFeeOutputAmount = BigInteger.Parse("1000000000000000000");
    private static readonly BigInteger NonFeeMinimumAmountOut = BigInteger.Parse("900000000000000000");

    private static UnsignedV2DutchOrderInfo OrderInfo(string outToken, bool twoOutputs) => new()
    {
        Deadline = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 1000,
        Reactor = Zero,
        Swapper = Zero,
        Nonce = 10,
        Cosigner = Zero,
        AdditionalValidationContract = Zero,
        AdditionalValidationData = "0x",
        Input = new DutchInput { Token = Usdc.Address, StartAmount = 1000, EndAmount = 1000 },
        Outputs = twoOutputs
            ? new List<DutchOutput>
            {
                new() { Token = outToken, StartAmount = NonFeeOutputAmount, EndAmount = NonFeeMinimumAmountOut, Recipient = Zero },
                new() { Token = outToken, StartAmount = 1000, EndAmount = 2000, Recipient = Zero },
            }
            : new List<DutchOutput>
            {
                new() { Token = outToken, StartAmount = NonFeeOutputAmount, EndAmount = NonFeeMinimumAmountOut, Recipient = Zero },
            },
    };

    private static V2DutchOrderTrade<BaseCurrency, BaseCurrency> Trade() =>
        new(Usdc, new BaseCurrency[] { Dai }, OrderInfo(Dai.Address, true), TradeType.EXACT_INPUT);

    [Fact]
    public void ReturnsRightInputAmount() => Trade().InputAmount.Quotient.ToString().Should().Be("1000");

    [Fact]
    public void ReturnsNonFeeOutputAmount() => Trade().OutputAmount.Quotient.ToString().Should().Be(NonFeeOutputAmount.ToString());

    [Fact]
    public void ReturnsMinimumAmountOut() => Trade().MinimumAmountOut().Quotient.ToString().Should().Be(NonFeeMinimumAmountOut.ToString());

    [Fact]
    public void WorksForNativeOutputTrades()
    {
        var trade = new V2DutchOrderTrade<BaseCurrency, BaseCurrency>(
            Usdc, new BaseCurrency[] { Ether.OnChain(1) }, OrderInfo(NativeAssets.ETH.ToString(), false), TradeType.EXACT_INPUT);
        trade.OutputAmount.Currency.Should().Be(Ether.OnChain(1));
    }

    [Fact]
    public void WorksForNativeOutputTradesWithZeroAddress()
    {
        var trade = new V2DutchOrderTrade<BaseCurrency, BaseCurrency>(
            Usdc, new BaseCurrency[] { Ether.OnChain(1) }, OrderInfo(Zero, false), TradeType.EXACT_INPUT);
        trade.OutputAmount.Currency.Should().Be(Ether.OnChain(1));
    }
}
