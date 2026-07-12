using System.Numerics;
using AwesomeAssertions;
using Nethereum.Signer;
using UniswapSharp.Permit2;
using UniswapSharp.UniswapX.Order;
using UniswapSharp.UniswapX.Utils;

namespace UniswapSharp.Testing.UniswapX;

// Port of uniswapx-sdk src/order/V3DutchOrder.test.ts.
public class V3DutchOrderTests
{
    private const long Time = 1725379823;
    private const long BlockNumber = 20671221;
    private static readonly BigInteger RawAmount = BigInteger.Parse("2121000");
    private const string InputToken = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";
    private const string OutputToken = "0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2";
    private const int ChainId = 1;
    private const string Zero = "0x0000000000000000000000000000000000000000";

    private static V3CosignerData CosignerWithOverrides() => new()
    {
        DecayStartBlock = BlockNumber,
        ExclusiveFiller = Zero,
        ExclusivityOverrideBps = 0,
        InputOverride = RawAmount,
        OutputOverrides = new List<BigInteger> { RawAmount * 102 / 100 },
    };

    private static V3CosignerData CosignerWithoutOverrides() => new()
    {
        DecayStartBlock = BlockNumber,
        ExclusiveFiller = Zero,
        ExclusivityOverrideBps = 0,
        InputOverride = 0,
        OutputOverrides = new List<BigInteger> { 0 },
    };

    private static CosignedV3DutchOrderInfo GetFullOrderInfo(Action<CosignedV3DutchOrderInfo>? mutate = null)
    {
        var info = new CosignedV3DutchOrderInfo
        {
            Reactor = Zero,
            Swapper = Zero,
            Nonce = 21,
            Deadline = Time + 1000,
            AdditionalValidationContract = Zero,
            AdditionalValidationData = "0x",
            Cosigner = Zero,
            StartingBaseFee = 0,
            CosignerData = CosignerWithOverrides(),
            Input = new V3DutchInput
            {
                Token = InputToken,
                StartAmount = RawAmount,
                Curve = new NonlinearDutchDecay { RelativeBlocks = new List<int>(), RelativeAmounts = new List<BigInteger>() },
                MaxAmount = RawAmount,
                AdjustmentPerGweiBaseFee = 0,
            },
            Outputs = new List<V3DutchOutput>
            {
                new()
                {
                    Token = OutputToken,
                    StartAmount = RawAmount,
                    Curve = new NonlinearDutchDecay
                    {
                        RelativeBlocks = new List<int> { 1, 2, 3, 4 },
                        RelativeAmounts = new List<BigInteger> { 1, 2, 3, 4 },
                    },
                    Recipient = Zero,
                    MinAmount = RawAmount - 4,
                    AdjustmentPerGweiBaseFee = 0,
                },
            },
            Cosignature = "0x",
        };
        mutate?.Invoke(info);
        return info;
    }

    [Fact]
    public void ParsesASerializedV3Order()
    {
        var orderInfo = GetFullOrderInfo();
        var order = new CosignedV3DutchOrder(orderInfo, ChainId);
        var parsed = CosignedV3DutchOrder.Parse(order.Serialize(), ChainId);
        parsed.Info.Should().BeEquivalentTo(orderInfo);
    }

    [Fact]
    public void ParsesASerializedV3OrderWithNegativeRelativeAmounts()
    {
        var orderInfo = GetFullOrderInfo(i => i.Outputs = new List<V3DutchOutput>
        {
            new()
            {
                Token = OutputToken,
                StartAmount = RawAmount,
                Curve = new NonlinearDutchDecay
                {
                    RelativeBlocks = new List<int> { 1, 2, 3, 4 },
                    RelativeAmounts = new List<BigInteger> { -1, -2, -3, -4 },
                },
                Recipient = Zero,
                MinAmount = 0,
                AdjustmentPerGweiBaseFee = 0,
            },
        });
        var order = new CosignedV3DutchOrder(orderInfo, ChainId);
        var parsed = CosignedV3DutchOrder.Parse(order.Serialize(), ChainId);
        parsed.Info.Should().BeEquivalentTo(orderInfo);
    }

    [Fact]
    public void ParsesInnerV3OrderWithNoCosignerOverrides()
    {
        var curve = new UnsignedV3DutchOrder.NonlinearDutchDecayJSON(
            new[] { 1, 2, 3, 4 }, new[] { "1", "2", "3", "4" });
        var json = new UnsignedV3DutchOrder.UnsignedV3DutchOrderInfoJSON(
            Reactor: Zero, Swapper: Zero, Nonce: "21", Deadline: Time + 1000,
            AdditionalValidationContract: Zero, AdditionalValidationData: "0x",
            Cosigner: Zero, StartingBaseFee: "0",
            Input: new UnsignedV3DutchOrder.V3DutchInputJSON(InputToken, "1000000", curve, "1000001", "0"),
            Outputs: new[] { new UnsignedV3DutchOrder.V3DutchOutputJSON(OutputToken, "1000000", curve, Zero, "1000000", "0") });
        var order = UnsignedV3DutchOrder.FromJSON(json, ChainId);
        order.Info.Input.StartAmount.Should().Be(BigInteger.Parse("1000000"));
        order.Info.Outputs[0].StartAmount.Should().Be(BigInteger.Parse("1000000"));
    }

    [Fact]
    public void ValidSignatureOverInnerOrder()
    {
        var fullOrderInfo = GetFullOrderInfo();
        var order = new UnsignedV3DutchOrder(fullOrderInfo, 1);
        var key = EthECKey.GenerateKey();
        string expected = UniswapSharp.Core.Utils.AddressValidator.GetAddress(key.GetPublicAddress());
        var permitData = order.PermitData();
        byte[] digest = Eip712TypedDataEncoder.Hash(permitData.Domain, permitData.Types, permitData.Values);
        string signature = DutchOrderTests.SignDigest(key, digest);
        order.GetSigner(signature).Should().Be(expected);
        var fullOrder = CosignedV3DutchOrder.FromUnsignedOrder(order, fullOrderInfo.CosignerData, fullOrderInfo.Cosignature);
        fullOrder.GetSigner(signature).Should().Be(expected);
    }

    [Fact]
    public void ValidatesCosignatureOverHashAndCosignerData()
    {
        var key = EthECKey.GenerateKey();
        string cosigner = UniswapSharp.Core.Utils.AddressValidator.GetAddress(key.GetPublicAddress());
        var orderInfo = GetFullOrderInfo(i => i.Cosigner = cosigner);
        var order = new UnsignedV3DutchOrder(orderInfo, 1);
        string fullOrderHash = order.CosignatureHash(orderInfo.CosignerData);
        byte[] hashBytes = Convert.FromHexString(fullOrderHash[2..]);
        string cosignature = DutchOrderTests.SignDigest(key, hashBytes);
        var signedOrder = CosignedV3DutchOrder.FromUnsignedOrder(order, CosignerWithOverrides(), cosignature);
        signedOrder.RecoverCosigner().Should().Be(cosigner);
    }

    [Fact]
    public void ResolvesBeforeDecayStartBlock()
    {
        var order = new CosignedV3DutchOrder(GetFullOrderInfo(), ChainId);
        var resolved = order.Resolve(new V3OrderResolutionOptions(BlockNumber - 1));
        resolved.Input.Amount.Should().Be(order.Info.CosignerData.InputOverride);
        resolved.Outputs[0].Amount.Should().Be(order.Info.CosignerData.OutputOverrides[0]);
    }

    [Fact]
    public void ResolvesWithOriginalValueWhenOverridesAreZero()
    {
        var order = new CosignedV3DutchOrder(GetFullOrderInfo(i => i.CosignerData = CosignerWithoutOverrides()), ChainId);
        var resolved = order.Resolve(new V3OrderResolutionOptions(BlockNumber - 1));
        resolved.Input.Amount.Should().Be(order.Info.Input.StartAmount);
        resolved.Outputs[0].Amount.Should().Be(order.Info.Outputs[0].StartAmount);
    }

    [Fact]
    public void ResolvesAtLastDecayBlockWithoutOverrides()
    {
        var order = new CosignedV3DutchOrder(GetFullOrderInfo(i => i.CosignerData = CosignerWithoutOverrides()), ChainId);
        var relativeBlocks = order.Info.Outputs[0].Curve.RelativeBlocks;
        var resolved = order.Resolve(new V3OrderResolutionOptions(BlockNumber + relativeBlocks[^1]));
        BigInteger endAmount = DutchBlockDecay.GetEndAmount(order.Info.Outputs[0].StartAmount, order.Info.Outputs[0].Curve.RelativeAmounts);
        resolved.Outputs[0].Amount.Should().Be(endAmount);
    }

    [Fact]
    public void ResolvesAfterLastDecayWithoutOverrides()
    {
        var order = new CosignedV3DutchOrder(GetFullOrderInfo(i => i.CosignerData = CosignerWithoutOverrides()), ChainId);
        var resolved = order.Resolve(new V3OrderResolutionOptions(BlockNumber + 42));
        BigInteger endAmount = DutchBlockDecay.GetEndAmount(order.Info.Outputs[0].StartAmount, order.Info.Outputs[0].Curve.RelativeAmounts);
        resolved.Outputs[0].Amount.Should().Be(endAmount);
    }
}
