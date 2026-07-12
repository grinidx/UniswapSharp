using System.Numerics;
using AwesomeAssertions;
using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.UniswapX.Order;
using UniswapSharp.UniswapX.Trade;

namespace UniswapSharp.Testing.UniswapX;

// Port of uniswapx-sdk src/trade/PriorityOrderTrade.test.ts.
public class PriorityOrderTradeTests
{
    private static readonly Token Usdc = new(1, "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48", 6, "USDC");
    private static readonly Token Dai = new(1, "0x6B175474E89094C44Da98b954EedeAC495271d0F", 18, "DAI");
    private const string Zero = "0x0000000000000000000000000000000000000000";
    private static readonly BigInteger NonFeeOutputAmount = BigInteger.Parse("1000000000000000000");

    private static UnsignedPriorityOrderInfo OrderInfo(string outToken, bool twoOutputs) => new()
    {
        Deadline = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 1000,
        Reactor = Zero,
        Swapper = Zero,
        Nonce = 10,
        Cosigner = Zero,
        AdditionalValidationContract = Zero,
        AdditionalValidationData = "0x",
        AuctionStartBlock = 100000,
        BaselinePriorityFeeWei = 2,
        Input = new PriorityInput { Token = Usdc.Address, Amount = 1000, MpsPerPriorityFeeWei = 0 },
        Outputs = twoOutputs
            ? new List<PriorityOutput>
            {
                new() { Token = outToken, Amount = NonFeeOutputAmount, MpsPerPriorityFeeWei = 5, Recipient = Zero },
                new() { Token = outToken, Amount = 1000, MpsPerPriorityFeeWei = 5, Recipient = Zero },
            }
            : new List<PriorityOutput>
            {
                new() { Token = outToken, Amount = NonFeeOutputAmount, MpsPerPriorityFeeWei = 5, Recipient = Zero },
            },
    };

    private static PriorityOrderTrade<BaseCurrency, BaseCurrency> Trade() =>
        new(Usdc, new BaseCurrency[] { Dai }, OrderInfo(Dai.Address, true), TradeType.EXACT_INPUT);

    [Fact]
    public void ReturnsRightInputAmount() => Trade().InputAmount.Quotient.ToString().Should().Be("1000");

    [Fact]
    public void ReturnsNonFeeOutputAmount() => Trade().OutputAmount.Quotient.ToString().Should().Be(NonFeeOutputAmount.ToString());

    [Fact]
    public void ReturnsMinimumAmountOut() => Trade().MinimumAmountOut().Quotient.ToString().Should().Be(NonFeeOutputAmount.ToString());

    [Fact]
    public void WorksForNativeOutputTrades()
    {
        var trade = new PriorityOrderTrade<BaseCurrency, BaseCurrency>(
            Usdc, new BaseCurrency[] { Ether.OnChain(1) }, OrderInfo(NativeAssets.ETH.ToString(), false), TradeType.EXACT_INPUT);
        trade.OutputAmount.Currency.Should().Be(Ether.OnChain(1));
    }

    [Fact]
    public void WorksForNativeOutputTradesWithZeroAddress()
    {
        var trade = new PriorityOrderTrade<BaseCurrency, BaseCurrency>(
            Usdc, new BaseCurrency[] { Ether.OnChain(1) }, OrderInfo(Zero, false), TradeType.EXACT_INPUT);
        trade.OutputAmount.Currency.Should().Be(Ether.OnChain(1));
    }

    [Fact]
    public void ReturnsCorrectAmountsWithExpectedQuoteData()
    {
        var trade = new PriorityOrderTrade<BaseCurrency, BaseCurrency>(
            Usdc, new BaseCurrency[] { Dai }, OrderInfo(Dai.Address, true), TradeType.EXACT_INPUT, new ExpectedAmounts("1", "1"));
        trade.InputAmount.Quotient.ToString().Should().Be("1");
        trade.OutputAmount.Quotient.ToString().Should().Be("1");
    }
}
