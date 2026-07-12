using System.Numerics;
using AwesomeAssertions;
using Nethereum.Signer;
using UniswapSharp.Permit2;
using UniswapSharp.UniswapX.Order;

namespace UniswapSharp.Testing.UniswapX;

// Port of uniswapx-sdk src/order/V2DutchOrder.test.ts.
public class V2DutchOrderTests
{
    private static readonly long Now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    private static readonly BigInteger RawAmount = BigInteger.Parse("1000000");
    private const string InputToken = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";
    private const string OutputToken = "0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2";
    private const string Zero = "0x0000000000000000000000000000000000000000";

    private static CosignerData CosignerData() => new()
    {
        DecayStartTime = Now,
        DecayEndTime = Now + 1000,
        ExclusiveFiller = Zero,
        ExclusivityOverrideBps = 0,
        InputOverride = RawAmount,
        OutputOverrides = new List<BigInteger> { RawAmount * 102 / 100 },
    };

    private static CosignedV2DutchOrderInfo GetFullOrderInfo(Action<CosignedV2DutchOrderInfo>? mutate = null)
    {
        var info = new CosignedV2DutchOrderInfo
        {
            Deadline = Now + 1000,
            Reactor = Zero,
            Swapper = Zero,
            Nonce = 10,
            AdditionalValidationContract = Zero,
            AdditionalValidationData = "0x",
            Cosigner = Zero,
            CosignerData = CosignerData(),
            Input = new DutchInput { Token = InputToken, StartAmount = RawAmount, EndAmount = RawAmount },
            Outputs = new List<DutchOutput>
            {
                new() { Token = OutputToken, StartAmount = RawAmount, EndAmount = RawAmount * 90 / 100, Recipient = Zero },
            },
            Cosignature = "0x",
        };
        mutate?.Invoke(info);
        return info;
    }

    [Fact]
    public void ParsesASerializedOrder()
    {
        var orderInfo = GetFullOrderInfo();
        var order = new CosignedV2DutchOrder(orderInfo, 1);
        var parsed = CosignedV2DutchOrder.Parse(order.Serialize(), 1);
        parsed.Info.Should().BeEquivalentTo(orderInfo);
    }

    [Fact]
    public void ParsesTheInnerV2OrderWithNoCosignerOverrides()
    {
        var json = new UnsignedV2DutchOrder.UnsignedV2DutchOrderInfoJSON(
            Reactor: Zero,
            Swapper: Zero,
            Nonce: "10",
            Deadline: Now + 1000,
            AdditionalValidationContract: Zero,
            AdditionalValidationData: "0x",
            Cosigner: Zero,
            Input: new DutchInputJSON(InputToken, "1000000", "1000000"),
            Outputs: new[] { new DutchOutputJSON(OutputToken, "1000000", "900000", Zero) });
        var order = UnsignedV2DutchOrder.FromJSON(json, 1);
        order.Info.Input.StartAmount.Should().Be(BigInteger.Parse("1000000"));
        order.Info.Outputs[0].StartAmount.Should().Be(BigInteger.Parse("1000000"));
    }

    [Fact]
    public void ValidSignatureOverInnerOrder()
    {
        var fullOrderInfo = GetFullOrderInfo();
        var order = new UnsignedV2DutchOrder(fullOrderInfo, 1);
        var key = EthECKey.GenerateKey();
        string expected = UniswapSharp.Core.Utils.AddressValidator.GetAddress(key.GetPublicAddress());

        var permitData = order.PermitData();
        byte[] digest = Eip712TypedDataEncoder.Hash(permitData.Domain, permitData.Types, permitData.Values);
        string signature = DutchOrderTests.SignDigest(key, digest);
        order.GetSigner(signature).Should().Be(expected);

        var fullOrder = CosignedV2DutchOrder.FromUnsignedOrder(order, fullOrderInfo.CosignerData, fullOrderInfo.Cosignature);
        fullOrder.GetSigner(signature).Should().Be(expected);
    }

    [Fact]
    public void ValidatesCosignatureOverHashAndCosignerData()
    {
        var key = EthECKey.GenerateKey();
        string cosigner = UniswapSharp.Core.Utils.AddressValidator.GetAddress(key.GetPublicAddress());
        var orderInfo = GetFullOrderInfo(i => i.Cosigner = cosigner);
        var order = new UnsignedV2DutchOrder(orderInfo, 1);
        string fullOrderHash = order.CosignatureHash(orderInfo.CosignerData);
        string cosignature = new EthereumMessageSigner().EncodeUTF8AndSign(fullOrderHash, key);
        var signedOrder = CosignedV2DutchOrder.FromUnsignedOrder(order, CosignerData(), cosignature);
        signedOrder.RecoverCosigner().Should().Be(cosigner);
    }

    [Fact]
    public void ResolvesBeforeDecayStartTime()
    {
        var order = new CosignedV2DutchOrder(GetFullOrderInfo(), 1);
        var resolved = order.Resolve(new OrderResolutionOptions(order.Info.CosignerData.DecayStartTime - 100));
        resolved.Input.Token.Should().Be(order.Info.Input.Token);
        resolved.Input.Amount.Should().Be(order.Info.CosignerData.InputOverride);
        resolved.Outputs[0].Amount.Should().Be(order.Info.CosignerData.OutputOverrides[0]);
    }

    [Fact]
    public void ResolvesWithOriginalValueWhenOverridesAreZero()
    {
        var order = new CosignedV2DutchOrder(GetFullOrderInfo(i => i.CosignerData = new CosignerData
        {
            DecayStartTime = Now,
            DecayEndTime = Now + 1000,
            ExclusiveFiller = Zero,
            ExclusivityOverrideBps = 0,
            InputOverride = 0,
            OutputOverrides = new List<BigInteger> { 0 },
        }), 1);
        var resolved = order.Resolve(new OrderResolutionOptions(order.Info.CosignerData.DecayStartTime - 100));
        resolved.Input.Amount.Should().Be(order.Info.Input.StartAmount);
        resolved.Outputs[0].Amount.Should().Be(order.Info.Outputs[0].StartAmount);
    }

    [Fact]
    public void ResolvesAtDecayEndTime()
    {
        var order = new CosignedV2DutchOrder(GetFullOrderInfo(), 1);
        var resolved = order.Resolve(new OrderResolutionOptions(order.Info.CosignerData.DecayEndTime));
        resolved.Input.Amount.Should().Be(order.Info.CosignerData.InputOverride);
        resolved.Outputs[0].Amount.Should().Be(order.Info.Outputs[0].EndAmount);
    }
}
