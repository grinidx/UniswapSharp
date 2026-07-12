using System.Numerics;
using AwesomeAssertions;
using Nethereum.Signer;
using UniswapSharp.Permit2;
using UniswapSharp.UniswapX.Order;

namespace UniswapSharp.Testing.UniswapX;

// Port of uniswapx-sdk src/order/RelayOrder.test.ts.
public class RelayOrderTests
{
    private const string Zero = "0x0000000000000000000000000000000000000000";

    private static RelayOrderInfo GetOrderInfo()
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
            Input = new RelayInput
            {
                Token = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48",
                Amount = BigInteger.Parse("1000000"),
                Recipient = Zero,
            },
            Fee = new RelayFee
            {
                Token = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48",
                StartAmount = BigInteger.Parse("1000000"),
                EndAmount = BigInteger.Parse("1000000"),
                StartTime = feeStartTime,
                EndTime = feeEndTime,
            },
        };
    }

    [Fact]
    public void ParsesASerializedOrder()
    {
        var orderInfo = GetOrderInfo();
        var order = new RelayOrder(orderInfo, 1);
        var parsed = RelayOrder.Parse(order.Serialize(), 1);
        parsed.Info.Should().BeEquivalentTo(orderInfo);
    }

    [Fact]
    public void ValidSignatureOverInfo()
    {
        var order = new RelayOrder(GetOrderInfo(), 1);
        var key = EthECKey.GenerateKey();
        var permitData = order.PermitData();
        byte[] digest = Eip712TypedDataEncoder.Hash(permitData.Domain, permitData.Types, permitData.Values);
        string signature = DutchOrderTests.SignDigest(key, digest);
        order.GetSigner(signature).Should().Be(UniswapSharp.Core.Utils.AddressValidator.GetAddress(key.GetPublicAddress()));
    }

    [Fact]
    public void ResolvesBeforeDecayStartTime()
    {
        var order = new RelayOrder(GetOrderInfo(), 1);
        order.Resolve(new OrderResolutionOptions(order.Info.Fee.StartTime - 100)).Fee.Amount
            .Should().Be(order.Info.Fee.StartAmount);
    }

    [Fact]
    public void ResolvesAtDecayStartTime()
    {
        var order = new RelayOrder(GetOrderInfo(), 1);
        order.Resolve(new OrderResolutionOptions(order.Info.Fee.StartTime)).Fee.Amount
            .Should().Be(order.Info.Fee.StartAmount);
    }

    [Fact]
    public void ResolvesAtDecayEndTime()
    {
        var order = new RelayOrder(GetOrderInfo(), 1);
        order.Resolve(new OrderResolutionOptions(order.Info.Fee.EndTime)).Fee.Amount
            .Should().Be(order.Info.Fee.EndAmount);
    }

    [Fact]
    public void ResolvesAfterDecayEndTime()
    {
        var order = new RelayOrder(GetOrderInfo(), 1);
        order.Resolve(new OrderResolutionOptions(order.Info.Fee.EndTime + 100)).Fee.Amount
            .Should().Be(order.Info.Fee.EndAmount);
    }
}
