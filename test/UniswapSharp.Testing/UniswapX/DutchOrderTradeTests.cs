using System.Numerics;
using AwesomeAssertions;
using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.UniswapX.Order;
using UniswapSharp.UniswapX.Trade;

namespace UniswapSharp.Testing.UniswapX;

// Port of uniswapx-sdk src/trade/DutchOrderTrade.test.ts.
public class DutchOrderTradeTests
{
    private static readonly Token Usdc = new(1, "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48", 6, "USDC");
    private static readonly Token Dai = new(1, "0x6B175474E89094C44Da98b954EedeAC495271d0F", 18, "DAI");
    private const string Zero = "0x0000000000000000000000000000000000000000";
    private static readonly BigInteger NonFeeOutputAmount = BigInteger.Parse("1000000000000000000");
    private static readonly BigInteger NonFeeMinimumAmountOut = BigInteger.Parse("900000000000000000");

    private static DutchOrderInfo OrderInfo(string outToken) => new()
    {
        Deadline = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 1000,
        Reactor = Zero,
        Swapper = Zero,
        Nonce = 10,
        AdditionalValidationContract = Zero,
        AdditionalValidationData = "0x",
        ExclusiveFiller = Zero,
        ExclusivityOverrideBps = 0,
        DecayStartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        DecayEndTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 1000,
        Input = new DutchInput { Token = Usdc.Address, StartAmount = 1000, EndAmount = 1000 },
        Outputs = new List<DutchOutput>
        {
            new() { Token = outToken, StartAmount = NonFeeOutputAmount, EndAmount = NonFeeMinimumAmountOut, Recipient = Zero },
        },
    };

    private static DutchOrderInfo TwoOutputOrderInfo()
    {
        var info = OrderInfo(Dai.Address);
        info.Outputs.Add(new DutchOutput { Token = Dai.Address, StartAmount = 1000, EndAmount = 2000, Recipient = Zero });
        return info;
    }

    private static DutchOrderTrade<BaseCurrency, BaseCurrency> Trade() =>
        new(Usdc, new BaseCurrency[] { Dai }, TwoOutputOrderInfo(), TradeType.EXACT_INPUT);

    [Fact]
    public void ReturnsRightInputAmount() => Trade().InputAmount.Quotient.ToString().Should().Be("1000");

    [Fact]
    public void ReturnsNonFeeOutputAmount() => Trade().OutputAmount.Quotient.ToString().Should().Be(NonFeeOutputAmount.ToString());

    [Fact]
    public void ReturnsMinimumAmountOut() => Trade().MinimumAmountOut().Quotient.ToString().Should().Be(NonFeeMinimumAmountOut.ToString());

    [Fact]
    public void WorksForNativeOutputTrades()
    {
        var trade = new DutchOrderTrade<BaseCurrency, BaseCurrency>(
            Usdc, new BaseCurrency[] { Ether.OnChain(1) }, OrderInfo(NativeAssets.ETH.ToString()), TradeType.EXACT_INPUT);
        trade.OutputAmount.Currency.Should().Be(Ether.OnChain(1));
    }

    [Fact]
    public void WorksForNativeOutputTradesWithZeroAddress()
    {
        var trade = new DutchOrderTrade<BaseCurrency, BaseCurrency>(
            Usdc, new BaseCurrency[] { Ether.OnChain(1) }, OrderInfo(Zero), TradeType.EXACT_INPUT);
        trade.OutputAmount.Currency.Should().Be(Ether.OnChain(1));
    }
}
