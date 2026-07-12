using System.Numerics;
using AwesomeAssertions;
using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.UniswapX.Order;
using UniswapSharp.UniswapX.Trade;

namespace UniswapSharp.Testing.UniswapX;

// Port of uniswapx-sdk src/trade/RelayOrderTrade.test.ts.
public class RelayOrderTradeTests
{
    private static readonly Token Usdc = new(1, "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48", 6, "USDC");
    private static readonly Token Dai = new(1, "0x6B175474E89094C44Da98b954EedeAC495271d0F", 18, "DAI");
    private const string Zero = "0x0000000000000000000000000000000000000000";
    private const string One = "0x0000000000000000000000000000000000000001";
    private static readonly BigInteger FeeStartAmount = 100;
    private static readonly BigInteger FeeEndAmount = 200;
    private static readonly BigInteger InputAmount = 1000;

    private static CurrencyAmount<BaseCurrency> MockSwapOutputAmount() =>
        CurrencyAmount<BaseCurrency>.FromRawAmount<BaseCurrency>(Dai, BigInteger.Parse("1000000000000000000000"));

    private static RelayOrderInfo OrderInfo(string feeToken, string inputToken)
    {
        long feeStartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long feeEndTime = feeStartTime + 1000;
        return new RelayOrderInfo
        {
            Deadline = feeEndTime,
            Reactor = Zero,
            Swapper = Zero,
            Nonce = 10,
            UniversalRouterCalldata = "0x",
            Fee = new RelayFee { Token = feeToken, StartAmount = FeeStartAmount, EndAmount = FeeEndAmount, StartTime = feeStartTime, EndTime = feeEndTime },
            Input = new RelayInput { Token = inputToken, Amount = InputAmount, Recipient = One },
        };
    }

    private static RelayOrderTrade<BaseCurrency, BaseCurrency> SameTokenTrade() =>
        new(new BaseCurrency[] { Usdc }, MockSwapOutputAmount(), OrderInfo(Usdc.Address, Usdc.Address), TradeType.EXACT_INPUT);

    [Fact]
    public void ReturnsRightAmountIn() => SameTokenTrade().AmountIn.Quotient.ToString().Should().Be("1000");

    [Fact]
    public void ReturnsCorrectOutputAmount() => SameTokenTrade().OutputAmount.Quotient.ToString().Should().Be("1000000000000000000000");

    [Fact]
    public void ReturnsCorrectFeeAmountIn() => SameTokenTrade().AmountInFee.Quotient.ToString().Should().Be(FeeStartAmount.ToString());

    [Fact]
    public void ReturnsCorrectFeeMaximumAmountIn() => SameTokenTrade().MaximumAmountInFee.Quotient.ToString().Should().Be(FeeEndAmount.ToString());

    [Fact]
    public void SameToken_ReturnsCorrectExecutionPrice() => SameTokenTrade().ExecutionPrice.Quotient.ToString().Should().Be("1000000000000000000");

    [Fact]
    public void SameToken_ReturnsCorrectWorstExecutionPrice() => SameTokenTrade().WorstExecutionPrice().Quotient.ToString().Should().Be("1000000000000000000");

    private static RelayOrderTrade<BaseCurrency, BaseCurrency> DifferentTokenTrade() =>
        new(new BaseCurrency[] { Usdc, Dai }, MockSwapOutputAmount(), OrderInfo(Usdc.Address, Dai.Address), TradeType.EXACT_INPUT);

    [Fact]
    public void DifferentTokens_ReturnsCorrectExecutionPrice() => DifferentTokenTrade().ExecutionPrice.Quotient.ToString().Should().Be("1000000000000000000");

    [Fact]
    public void DifferentTokens_ReturnsCorrectWorstExecutionPrice() => DifferentTokenTrade().WorstExecutionPrice().Quotient.ToString().Should().Be("1000000000000000000");
}
