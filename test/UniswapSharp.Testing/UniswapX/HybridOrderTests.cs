using System.Numerics;
using AwesomeAssertions;
using Nethereum.Signer;
using UniswapSharp.Core.Utils;
using UniswapSharp.UniswapX.Order.V4;
using UniswapSharp.V4.Utils;

namespace UniswapSharp.Testing.UniswapX;

// Port of uniswapx-sdk src/order/v4/HybridOrder.test.ts (CosignedHybridOrder).
public class HybridOrderTests
{
    private const int ChainIdValue = 1;
    private const string ZeroAddress = "0x0000000000000000000000000000000000000000";
    private const string Resolver = "0x0000000000000000000000000000000000000210";
    private const string Reactor = "0x0000000000000000000000000000000000000456";
    private const string Swapper = "0x0000000000000000000000000000000000000789";
    private const string PreHook = "0x0000000000000000000000000000000000000999";
    private const string PostHook = "0x0000000000000000000000000000000000000a10";
    private const string OutputRecipient = "0x0000000000000000000000000000000000000bbb";
    private const string InputToken = "0x0000000000000000000000000000000000000c10";
    private const string OutputToken = "0x0000000000000000000000000000000000000d10";
    private static readonly BigInteger Wad = BigInteger.Pow(10, 18);
    private static readonly EthECKey Cosigner = new("0x59c6995e998f97a5a0044976f7d75e7b7d6f4b6b55bdbb1c0cfd43a3d6ab1e31");

    private static BigInteger PackPriceCurveElement(int duration, BigInteger scalingFactor) =>
        ((BigInteger)duration << 240) | scalingFactor;

    private static CosignedHybridOrderInfo BuildOrder(Action<CosignedHybridOrderInfo>? overrides = null)
    {
        var order = new CosignedHybridOrderInfo
        {
            Reactor = Reactor,
            Swapper = Swapper,
            Nonce = 1,
            Deadline = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600,
            PreExecutionHook = PreHook,
            PreExecutionHookData = "0x1234",
            PostExecutionHook = PostHook,
            PostExecutionHookData = "0x",
            AuctionResolver = Resolver,
            Cosigner = AddressValidator.GetAddress(Cosigner.GetPublicAddress()),
            Input = new HybridInput { Token = InputToken, MaxAmount = BigInteger.Parse("1000000000000000000") },
            Outputs = new List<HybridOutput>
            {
                new() { Token = OutputToken, MinAmount = BigInteger.Parse("3000000000000000000"), Recipient = OutputRecipient },
            },
            AuctionStartBlock = 100,
            BaselinePriorityFee = BigInteger.Parse("1000000000"),
            ScalingFactor = Wad + 1,
            PriceCurve = new List<BigInteger>
            {
                PackPriceCurveElement(0, Wad),
                PackPriceCurveElement(5, Wad + BigInteger.Parse("50000000000000000")),
            },
            CosignerData = new HybridCosignerData
            {
                AuctionTargetBlock = 105,
                SupplementalPriceCurve = new List<BigInteger> { Wad + BigInteger.Parse("10000000000000000") },
                ExclusiveFiller = ZeroAddress,
                ExclusivityOverrideBps = 0,
                ExclusivityEndBlock = 0,
            },
            Cosignature = "0x",
        };
        overrides?.Invoke(order);

        if (order.Cosigner != ZeroAddress && order.Cosignature == "0x")
        {
            order.Cosignature = SignCosignerDigest(order);
        }
        if (order.Cosigner == ZeroAddress)
        {
            order.Cosignature = "0x";
        }
        return order;
    }

    private static string SignCosignerDigest(CosignedHybridOrderInfo order)
    {
        string digest = Hashing.HashHybridCosignerData(Hashing.HashHybridOrder(order), order.CosignerData, ChainIdValue);
        byte[] digestBytes = Convert.FromHexString(digest[2..]);
        return DutchOrderTests.SignDigest(Cosigner, digestBytes);
    }

    [Fact]
    public void HashesHybridOrderViaHelper()
    {
        var order = BuildOrder();
        var hybrid = new CosignedHybridOrder(order, ChainIdValue, Resolver);
        hybrid.Hash().Should().Be(Hashing.HashHybridOrder(order));
    }

    [Fact]
    public void SerializesToResolverPrefixedBytes()
    {
        var order = BuildOrder();
        var hybrid = new CosignedHybridOrder(order, ChainIdValue, Resolver);
        string serialized = hybrid.Serialize();

        var outer = AbiParamDecoder.Decode(new[] { "address", "bytes" }, serialized);
        AddressValidator.GetAddress((string)outer[0]!).Should().Be(Resolver);
        string encoded = (string)outer[1]!;

        string manual = AbiParamEncoder.Encode(new[] { UnsignedHybridOrder.HybridOrderAbi }, new object?[]
        {
            new object?[]
            {
                new object?[]
                {
                    order.Reactor, order.Swapper, order.Nonce, order.Deadline, order.PreExecutionHook,
                    order.PreExecutionHookData, order.PostExecutionHook, order.PostExecutionHookData, order.AuctionResolver,
                },
                order.Cosigner,
                new object?[] { order.Input.Token, order.Input.MaxAmount },
                order.Outputs.Select(o => new object?[] { o.Token, o.MinAmount, o.Recipient }).ToArray(),
                order.AuctionStartBlock,
                order.BaselinePriorityFee,
                order.ScalingFactor,
                order.PriceCurve.ToArray(),
                new object?[]
                {
                    order.CosignerData.AuctionTargetBlock,
                    order.CosignerData.SupplementalPriceCurve.ToArray(),
                    order.CosignerData.ExclusiveFiller,
                    order.CosignerData.ExclusivityOverrideBps,
                    order.CosignerData.ExclusivityEndBlock,
                },
                order.Cosignature,
            },
        });
        encoded.Should().Be(manual);
    }

    [Fact]
    public void RoundTripsViaJson()
    {
        var order = BuildOrder();
        var hybrid = new CosignedHybridOrder(order, ChainIdValue, Resolver);
        var json = hybrid.ToJSON();
        var revived = CosignedHybridOrder.FromJSON(json, ChainIdValue, Resolver);
        revived.Info.Should().BeEquivalentTo(order);
    }

    [Fact]
    public void ComputesCosignerDigestAndRecoversSigner()
    {
        var order = BuildOrder();
        var hybrid = new CosignedHybridOrder(order, ChainIdValue, Resolver);
        string digest = Hashing.HashHybridCosignerData(Hashing.HashHybridOrder(order), order.CosignerData, ChainIdValue);
        hybrid.CosignatureHash().Should().Be(digest);
        AddressValidator.GetAddress(hybrid.RecoverCosigner())
            .Should().Be(AddressValidator.GetAddress(Cosigner.GetPublicAddress()));
    }

    [Fact]
    public void FallsBackToAuctionStartBlockForOverridesWhenCosignerDataUnset()
    {
        var order = BuildOrder(o => o.CosignerData = new HybridCosignerData
        {
            AuctionTargetBlock = 0,
            SupplementalPriceCurve = new List<BigInteger>(),
            ExclusiveFiller = ZeroAddress,
            ExclusivityOverrideBps = 0,
            ExclusivityEndBlock = 0,
        });
        var hybrid = new CosignedHybridOrder(order, ChainIdValue, Resolver);
        hybrid.BlockOverrides!.Number.Should().Be(UniswapSharp.UniswapX.Utils.OrderUtils.HexStripZeros(order.AuctionStartBlock));
    }

    [Fact]
    public void PrefersCosignerAuctionTargetBlockForOverrides()
    {
        var order = BuildOrder(o => o.CosignerData = new HybridCosignerData
        {
            AuctionTargetBlock = 222,
            SupplementalPriceCurve = new List<BigInteger>(),
            ExclusiveFiller = ZeroAddress,
            ExclusivityOverrideBps = 0,
            ExclusivityEndBlock = 0,
        });
        var hybrid = new CosignedHybridOrder(order, ChainIdValue, Resolver);
        hybrid.BlockOverrides!.Number.Should().Be(UniswapSharp.UniswapX.Utils.OrderUtils.HexStripZeros(order.CosignerData.AuctionTargetBlock));
    }

    [Fact]
    public void ExposesPermit2DataPointingToTokenTransferHook()
    {
        var order = BuildOrder();
        var hybrid = new CosignedHybridOrder(order, ChainIdValue, Resolver);
        var permit = hybrid.PermitData();
        permit.Values["spender"].Should().Be(order.PreExecutionHook);
        ((IReadOnlyDictionary<string, object?>)permit.Values["permitted"]!)["amount"].Should().Be(order.Input.MaxAmount);
    }

    [Fact]
    public void ResolvesExactInPathWhenScalingFactorGeWad()
    {
        var order = BuildOrder(o =>
        {
            o.PriceCurve = new List<BigInteger>();
            o.CosignerData = ZeroCosignerData();
            o.AuctionStartBlock = 0;
            o.ScalingFactor = Wad + 5;
        });
        var hybrid = new CosignedHybridOrder(order, ChainIdValue, Resolver);
        var result = hybrid.Resolve(new HybridOrderResolutionOptions(10, order.BaselinePriorityFee + 1));
        BigInteger expected = MulWadUp(order.Outputs[0].MinAmount, Wad + 5);
        result.Input.Amount.Should().Be(order.Input.MaxAmount);
        result.Outputs[0].Amount.Should().Be(expected);
    }

    [Fact]
    public void ResolvesExactOutPathWhenScalingFactorLtWad()
    {
        var order = BuildOrder(o =>
        {
            o.PriceCurve = new List<BigInteger>();
            o.CosignerData = ZeroCosignerData();
            o.AuctionStartBlock = 0;
            o.ScalingFactor = Wad - 5;
        });
        var hybrid = new CosignedHybridOrder(order, ChainIdValue, Resolver);
        var result = hybrid.Resolve(new HybridOrderResolutionOptions(10, order.BaselinePriorityFee + 1));
        BigInteger expectedInput = MulWad(order.Input.MaxAmount, Wad - 5);
        result.Input.Amount.Should().Be(expectedInput);
        result.Outputs[0].Amount.Should().Be(order.Outputs[0].MinAmount);
    }

    [Fact]
    public void ResolvesWhenFillerHasExclusivity()
    {
        var order = BuildOrder(o =>
        {
            o.PriceCurve = new List<BigInteger>();
            o.CosignerData = ZeroCosignerData("0x0000000000000000000000000000000000000aaa");
            o.AuctionStartBlock = 0;
            o.ScalingFactor = Wad + 5;
        });
        var hybrid = new CosignedHybridOrder(order, ChainIdValue, Resolver);
        var result = hybrid.Resolve(new HybridOrderResolutionOptions(0, order.BaselinePriorityFee, "0x0000000000000000000000000000000000000aaa"));
        result.Outputs[0].Amount.Should().Be(order.Outputs[0].MinAmount);
    }

    [Fact]
    public void ThrowsWhenCurrentBlockPrecedesTargetBlock()
    {
        var order = BuildOrder(o => o.CosignerData = new HybridCosignerData
        {
            AuctionTargetBlock = 500,
            SupplementalPriceCurve = new List<BigInteger>(),
            ExclusiveFiller = ZeroAddress,
            ExclusivityOverrideBps = 0,
            ExclusivityEndBlock = 0,
        });
        var hybrid = new CosignedHybridOrder(order, ChainIdValue, Resolver);
        Action act = () => hybrid.Resolve(new HybridOrderResolutionOptions(400, order.BaselinePriorityFee));
        act.Should().Throw<OrderResolutionError>();
    }

    private static HybridCosignerData ZeroCosignerData(string exclusiveFiller = ZeroAddress) => new()
    {
        AuctionTargetBlock = 0,
        SupplementalPriceCurve = new List<BigInteger>(),
        ExclusiveFiller = exclusiveFiller,
        ExclusivityOverrideBps = 0,
        ExclusivityEndBlock = 0,
    };

    private static BigInteger MulWad(BigInteger a, BigInteger b) => a.IsZero || b.IsZero ? 0 : a * b / Wad;
    private static BigInteger MulWadUp(BigInteger a, BigInteger b) => a.IsZero || b.IsZero ? 0 : (a * b + Wad - 1) / Wad;
}
