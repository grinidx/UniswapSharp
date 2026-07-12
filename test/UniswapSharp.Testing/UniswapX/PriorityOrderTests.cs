using System.Numerics;
using AwesomeAssertions;
using Nethereum.Signer;
using UniswapSharp.Permit2;
using UniswapSharp.UniswapX.Order;

namespace UniswapSharp.Testing.UniswapX;

// Port of uniswapx-sdk src/order/PriorityOrder.test.ts.
public class PriorityOrderTests
{
    private static readonly BigInteger Block = 100;
    private static readonly long Now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    private static readonly BigInteger RawAmount = BigInteger.Parse("1000000");
    private const string InputToken = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";
    private const string OutputToken = "0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2";
    private const string Zero = "0x0000000000000000000000000000000000000000";

    private static CosignedPriorityOrderInfo GetFullOrderInfo(Action<CosignedPriorityOrderInfo>? mutate = null)
    {
        var info = new CosignedPriorityOrderInfo
        {
            Deadline = Now + 1000,
            Reactor = Zero,
            Swapper = Zero,
            Nonce = 10,
            AdditionalValidationContract = Zero,
            AdditionalValidationData = "0x",
            Cosigner = Zero,
            AuctionStartBlock = Block,
            BaselinePriorityFeeWei = 0,
            Input = new PriorityInput { Token = InputToken, Amount = RawAmount, MpsPerPriorityFeeWei = 0 },
            Outputs = new List<PriorityOutput>
            {
                new() { Token = OutputToken, Amount = RawAmount, MpsPerPriorityFeeWei = 10, Recipient = Zero },
            },
            CosignerData = new PriorityCosignerData { AuctionTargetBlock = Block - 2 },
            Cosignature = "0x",
        };
        mutate?.Invoke(info);
        return info;
    }

    [Fact]
    public void ParsesASerializedOrder()
    {
        var orderInfo = GetFullOrderInfo();
        var order = new CosignedPriorityOrder(orderInfo, 1);
        var parsed = CosignedPriorityOrder.Parse(order.Serialize(), 1);
        parsed.Info.Should().BeEquivalentTo(orderInfo);
    }

    [Fact]
    public void ValidSignatureOverOrder()
    {
        var order = new CosignedPriorityOrder(GetFullOrderInfo(), 1);
        var key = EthECKey.GenerateKey();
        var permitData = order.PermitData();
        byte[] digest = Eip712TypedDataEncoder.Hash(permitData.Domain, permitData.Types, permitData.Values);
        string signature = DutchOrderTests.SignDigest(key, digest);
        order.GetSigner(signature).Should().Be(UniswapSharp.Core.Utils.AddressValidator.GetAddress(key.GetPublicAddress()));
    }

    [Fact]
    public void ThrowsWhenCurrentBlockBeforeAuctionTargetBlock()
    {
        var order = new CosignedPriorityOrder(GetFullOrderInfo(), 1);
        Action act1 = () => order.Resolve(new PriorityOrderResolutionOptions(1, Block - 10));
        act1.Should().Throw<OrderNotFillable>().WithMessage("Target block in the future");

        var order2 = new CosignedPriorityOrder(
            GetFullOrderInfo(i => i.CosignerData = new PriorityCosignerData { AuctionTargetBlock = 0 }), 1);
        Action act2 = () => order2.Resolve(new PriorityOrderResolutionOptions(1, Block - 1));
        act2.Should().Throw<OrderNotFillable>().WithMessage("Start block in the future");
    }

    [Fact]
    public void ResolvesAtCurrentBlock()
    {
        var order = new CosignedPriorityOrder(GetFullOrderInfo(), 1);
        var resolved = order.Resolve(new PriorityOrderResolutionOptions(1, Block));
        resolved.Input.Token.Should().Be(order.Info.Input.Token);
        resolved.Input.Amount.Should().Be(order.Info.Input.Amount);
        resolved.Outputs[0].Token.Should().Be(order.Info.Outputs[0].Token);
        resolved.Outputs[0].Amount.Should().Be(order.Info.Outputs[0].Amount + 1);
    }
}
