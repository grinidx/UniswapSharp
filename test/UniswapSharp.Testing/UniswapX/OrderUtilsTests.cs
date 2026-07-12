using System.Numerics;
using AwesomeAssertions;
using UniswapSharp.UniswapX;
using UniswapSharp.UniswapX.Builder;
using UniswapSharp.UniswapX.Order;
using UniswapSharp.UniswapX.Order.V4;
using UniswapSharp.UniswapX.Utils;

namespace UniswapSharp.Testing.UniswapX;

// Port of uniswapx-sdk src/utils/order.test.ts.
public class OrderUtilsTests
{
    private const string Zero = "0x0000000000000000000000000000000000000000";
    private const string One = "0x0000000000000000000000000000000000000001";
    private const string TestHybridResolver = "0x1234567890123456789012345678901234567890";

    private readonly UniswapXOrderParser _uniswapXOrderParser = new();
    private readonly RelayOrderParser _relayOrderParser = new();

    private readonly int _chainId = 1;
    private readonly int _priorityChainId = 8453;
    private readonly int _blockBasedChainId = 42161;

    private readonly DutchOrder _dutchOrder;
    private readonly DutchOrder _dutchOrderExactOut;
    private readonly DutchOrder _limitOrder;
    private readonly RelayOrder _relayOrder;
    private readonly UnsignedV2DutchOrder _unsignedV2DutchOrder;
    private readonly CosignedV2DutchOrder _cosignedV2DutchOrder;
    private readonly UnsignedV3DutchOrder _unsignedV3DutchOrder;
    private readonly CosignedV3DutchOrder _cosignedV3DutchOrder;
    private readonly UnsignedPriorityOrder _unsignedPriorityOrder;
    private readonly CosignedPriorityOrder _cosignedPriorityOrder;
    private readonly UnsignedHybridOrder _unsignedHybridOrder;
    private readonly CosignedHybridOrder _cosignedHybridOrder;

    public OrderUtilsTests()
    {
        long deadline = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 1000;
        var input = new DutchInput { Token = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48", StartAmount = 1000000, EndAmount = 1000000 };

        _dutchOrder = new DutchOrderBuilder(_chainId)
            .Deadline(deadline).DecayEndTime(deadline).DecayStartTime(deadline - 100)
            .Swapper(One).Nonce(100).Input(input)
            .Output(new DutchOutput { Token = "0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2", StartAmount = BigInteger.Parse("1000000000000000000"), EndAmount = BigInteger.Parse("900000000000000000"), Recipient = Zero })
            .Build();

        _dutchOrderExactOut = new DutchOrderBuilder(_chainId)
            .Deadline(deadline).DecayEndTime(deadline).DecayStartTime(deadline - 100)
            .Swapper(One).Nonce(100)
            .Input(new DutchInput { Token = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48", StartAmount = 900000, EndAmount = 1000000 })
            .Output(new DutchOutput { Token = "0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2", StartAmount = BigInteger.Parse("1000000000000000000"), EndAmount = BigInteger.Parse("1000000000000000000"), Recipient = Zero })
            .Build();

        _limitOrder = new DutchOrderBuilder(_chainId)
            .Deadline(deadline).DecayEndTime(deadline).DecayStartTime(deadline - 100)
            .Swapper(One).Nonce(100).Input(input)
            .Output(new DutchOutput { Token = "0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2", StartAmount = BigInteger.Parse("1000000000000000000"), EndAmount = BigInteger.Parse("1000000000000000000"), Recipient = Zero })
            .Build();

        _relayOrder = new RelayOrderBuilder(_chainId)
            .Deadline(deadline).Swapper(One).Nonce(100).UniversalRouterCalldata("0x")
            .Input(new RelayInput { Token = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48", Amount = 1000000, Recipient = Zero })
            .Fee(new RelayFee { Token = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48", StartAmount = 1000000, EndAmount = 1000000, StartTime = deadline - 100, EndTime = deadline })
            .Build();

        var v2Builder = new V2DutchOrderBuilder(_chainId, "0x00000011F84B9aa48e5f8aA8B9897600006289Be")
            .Cosigner("0xe463635f6e73C1E595554C3ae216472D0fb929a9")
            .Deadline(deadline).DecayEndTime(deadline).DecayStartTime(deadline - 100)
            .Swapper(Zero).Nonce(100)
            .Input(new DutchInput { Token = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48", StartAmount = 1000000, EndAmount = 1000000 })
            .Output(new DutchOutput { Token = "0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2", StartAmount = BigInteger.Parse("1000000000000000000"), EndAmount = BigInteger.Parse("1000000000000000000"), Recipient = Zero })
            .OutputOverrides(new BigInteger[] { BigInteger.Parse("100000000000000000000") });
        _unsignedV2DutchOrder = v2Builder.BuildPartial();
        _cosignedV2DutchOrder = v2Builder
            .Cosignature("0x65c6470fea0e1ca7d204b6904d0c1b0b640d7e6dcd4be3065497756e163c0399288c3eea0fba9b31ed00f34ccffe389ec3027bcd764df9fa853eeae8f68c9beb1b")
            .Build();

        var priorityBuilder = new PriorityOrderBuilder(_priorityChainId)
            .Cosigner("0xe463635f6e73C1E595554C3ae216472D0fb929a9")
            .Deadline(deadline).Swapper(Zero).Nonce(100)
            .AuctionStartBlock(123).BaselinePriorityFeeWei(0)
            .Input(new PriorityInput { Token = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48", Amount = 1000000, MpsPerPriorityFeeWei = 0 })
            .Output(new PriorityOutput { Token = "0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2", Amount = BigInteger.Parse("1000000000000000000"), MpsPerPriorityFeeWei = 1, Recipient = Zero });
        _unsignedPriorityOrder = priorityBuilder.BuildPartial();
        _cosignedPriorityOrder = priorityBuilder
            .CosignerData(new PriorityCosignerData { AuctionTargetBlock = 123 })
            .Cosignature("0x65c6470fea0e1ca7d204b6904d0c1b0b640d7e6dcd4be3065497756e163c0399288c3eea0fba9b31ed00f34ccffe389ec3027bcd764df9fa853eeae8f68c9beb1b")
            .Build();

        var v3Builder = new V3DutchOrderBuilder(_blockBasedChainId)
            .Cosigner("0xf4c37D77623D476F52225df3Bbe8a874209a1149")
            .Deadline(deadline).Swapper(Zero).Nonce(100).StartingBaseFee(0)
            .Input(new V3DutchInput
            {
                Token = "0xFd086bC7CD5C481DCC9C85ebE478A1C0b69FCbb9",
                StartAmount = 1000000,
                Curve = new NonlinearDutchDecay { RelativeBlocks = new List<int>(), RelativeAmounts = new List<BigInteger>() },
                MaxAmount = 1000000,
                AdjustmentPerGweiBaseFee = 0,
            })
            .Output(new V3DutchOutput
            {
                Token = "0x2f2a2543B76A4166549F7aaB2e75Bef0aefC5B0f",
                StartAmount = 1000000,
                Curve = new NonlinearDutchDecay { RelativeBlocks = new List<int> { 4 }, RelativeAmounts = new List<BigInteger> { 4 } },
                Recipient = Zero,
                MinAmount = 1000000 - 4,
                AdjustmentPerGweiBaseFee = 0,
            });
        _unsignedV3DutchOrder = v3Builder.BuildPartial();
        _cosignedV3DutchOrder = v3Builder
            .CosignerData(new V3CosignerData { DecayStartBlock = 100, ExclusiveFiller = Zero, ExclusivityOverrideBps = 0, InputOverride = 0, OutputOverrides = new List<BigInteger> { 0 } })
            .Cosignature("0x88a3d425308d71431b514826cbf9c74f713b57946b0a29f7d7e094ccf0ab562e270216a537b59210f1b5c87f5cc5662cd87dea5df7e699d92b061191bd2499c71b")
            .Build();

        var hybridBuilder = new HybridOrderBuilder(_chainId, One, TestHybridResolver)
            .Cosigner(Zero).Deadline(deadline).Swapper(Zero).Nonce(100)
            .Input(new HybridInput { Token = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48", MaxAmount = 1000000 })
            .Output(new HybridOutput { Token = "0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2", MinAmount = BigInteger.Parse("1000000000000000000"), Recipient = Zero })
            .AuctionStartBlock(100).BaselinePriorityFee(0).ScalingFactor(ConstantsV4.BaseScalingFactor).PriceCurve(new List<BigInteger>());
        _unsignedHybridOrder = hybridBuilder.BuildPartial();
        _cosignedHybridOrder = hybridBuilder
            .AuctionTargetBlock(100).SupplementalPriceCurve(new List<BigInteger>())
            .Cosignature("0x0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000")
            .Build();
    }

    private IDisposable WithHybridResolver()
    {
        Constants.ReverseResolverMapping[TestHybridResolver.ToLowerInvariant()] = new OrderTypeMapping(OrderType.Hybrid);
        return new Remover(() => Constants.ReverseResolverMapping.Remove(TestHybridResolver.ToLowerInvariant()));
    }

    private sealed class Remover(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }

    // ---- parseOrder ----

    [Fact]
    public void ParsesDutchOrderWithSingleOutput()
    {
        var parsed = (DutchOrder)_uniswapXOrderParser.ParseOrder(_dutchOrder.Serialize(), _chainId);
        parsed.Info.Should().BeEquivalentTo(_dutchOrder.Info);
    }

    [Fact]
    public void ParsesCosignedV2DutchOrder()
    {
        var parsed = (CosignedV2DutchOrder)_uniswapXOrderParser.ParseOrder(_cosignedV2DutchOrder.Serialize(), _chainId);
        parsed.Info.Should().BeEquivalentTo(_cosignedV2DutchOrder.Info);
    }

    [Fact]
    public void ParsesUnsignedV2DutchOrder()
    {
        var parsed = _uniswapXOrderParser.ParseOrder(_unsignedV2DutchOrder.Serialize(), _chainId);
        parsed.Should().BeOfType<UnsignedV2DutchOrder>();
        ((UnsignedV2DutchOrder)parsed).Info.Should().BeEquivalentTo(_unsignedV2DutchOrder.Info);
    }

    [Fact]
    public void ParsesRelayOrder()
    {
        var parsed = (RelayOrder)_relayOrderParser.ParseOrder(_relayOrder.Serialize(), _chainId);
        parsed.Info.Should().BeEquivalentTo(_relayOrder.Info);
    }

    [Fact]
    public void ParsesCosignedPriorityOrder()
    {
        var parsed = (CosignedPriorityOrder)_uniswapXOrderParser.ParseOrder(_cosignedPriorityOrder.Serialize(), _priorityChainId);
        parsed.Info.Should().BeEquivalentTo(_cosignedPriorityOrder.Info);
    }

    [Fact]
    public void ParsesUnsignedPriorityOrder()
    {
        var parsed = _uniswapXOrderParser.ParseOrder(_unsignedPriorityOrder.Serialize(), _priorityChainId);
        parsed.Should().BeOfType<UnsignedPriorityOrder>();
        ((UnsignedPriorityOrder)parsed).Info.Should().BeEquivalentTo(_unsignedPriorityOrder.Info);
    }

    [Fact]
    public void ParsesCosignedHybridOrderViaResolverDetection()
    {
        using var _ = WithHybridResolver();
        var parsed = _uniswapXOrderParser.ParseOrder(_cosignedHybridOrder.Serialize(), _chainId);
        parsed.Should().BeOfType<CosignedHybridOrder>();
        ((CosignedHybridOrder)parsed).Info.Should().BeEquivalentTo(_cosignedHybridOrder.Info);
    }

    [Fact]
    public void ParsesUnsignedHybridOrderViaResolverDetection()
    {
        using var _ = WithHybridResolver();
        var parsed = _uniswapXOrderParser.ParseOrder(_unsignedHybridOrder.Serialize(), _chainId);
        parsed.Should().BeOfType<UnsignedHybridOrder>();
        ((UnsignedHybridOrder)parsed).Info.Should().BeEquivalentTo(_unsignedHybridOrder.Info);
    }

    [Fact]
    public void RoundTripsCosignedHybridOrder()
    {
        using var _ = WithHybridResolver();
        var parsed = (CosignedHybridOrder)_uniswapXOrderParser.ParseOrder(_cosignedHybridOrder.Serialize(), _chainId);
        parsed.Serialize().Should().Be(_cosignedHybridOrder.Serialize());
    }

    [Fact]
    public void FallsBackToReactorDetectionWhenResolverUnknown()
    {
        // No resolver registered → hybrid detection fails, falls back to reactor lookup which throws.
        Action act = () => _uniswapXOrderParser.ParseOrder(_unsignedHybridOrder.Serialize(), _chainId);
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void ParsesLegacyDutchOrderEvenThoughItStartsWithAddressBytes()
    {
        var parsed = (DutchOrder)_uniswapXOrderParser.ParseOrder(_dutchOrder.Serialize(), _chainId);
        parsed.Info.Should().BeEquivalentTo(_dutchOrder.Info);
    }

    // ---- getOrderType ----

    [Fact] public void GetOrderType_Dutch() => _uniswapXOrderParser.GetOrderType(_dutchOrder).Should().Be(OrderType.Dutch);
    [Fact] public void GetOrderType_DutchExactOut() => _uniswapXOrderParser.GetOrderType(_dutchOrderExactOut).Should().Be(OrderType.Dutch);
    [Fact] public void GetOrderType_Limit() => _uniswapXOrderParser.GetOrderType(_limitOrder).Should().Be(OrderType.Limit);
    [Fact] public void GetOrderType_Relay() => _relayOrderParser.GetOrderType(_relayOrder).Should().Be(OrderType.Relay);
    [Fact] public void GetOrderType_CosignedV2() => _uniswapXOrderParser.GetOrderType(_cosignedV2DutchOrder).Should().Be(OrderType.Dutch_V2);
    [Fact] public void GetOrderType_UnsignedV2() => _uniswapXOrderParser.GetOrderType(_unsignedV2DutchOrder).Should().Be(OrderType.Dutch_V2);
    [Fact] public void GetOrderType_CosignedPriority() => _uniswapXOrderParser.GetOrderType(_cosignedPriorityOrder).Should().Be(OrderType.Priority);
    [Fact] public void GetOrderType_UnsignedPriority() => _uniswapXOrderParser.GetOrderType(_unsignedPriorityOrder).Should().Be(OrderType.Priority);
    [Fact] public void GetOrderType_UnsignedV3() => _uniswapXOrderParser.GetOrderType(_unsignedV3DutchOrder).Should().Be(OrderType.Dutch_V3);
    [Fact] public void GetOrderType_CosignedV3() => _uniswapXOrderParser.GetOrderType(_cosignedV3DutchOrder).Should().Be(OrderType.Dutch_V3);
    [Fact] public void GetOrderType_UnsignedHybrid() => _uniswapXOrderParser.GetOrderType(_unsignedHybridOrder).Should().Be(OrderType.Hybrid);
    [Fact] public void GetOrderType_CosignedHybrid() => _uniswapXOrderParser.GetOrderType(_cosignedHybridOrder).Should().Be(OrderType.Hybrid);

    // ---- getOrderTypeFromEncoded ----

    [Fact] public void GetOrderTypeFromEncoded_Dutch() => _uniswapXOrderParser.GetOrderTypeFromEncoded(_dutchOrder.Serialize(), _chainId).Should().Be(OrderType.Dutch);
    [Fact] public void GetOrderTypeFromEncoded_Limit() => _uniswapXOrderParser.GetOrderTypeFromEncoded(_limitOrder.Serialize(), _chainId).Should().Be(OrderType.Limit);
    [Fact] public void GetOrderTypeFromEncoded_Relay() => _relayOrderParser.GetOrderTypeFromEncoded(_relayOrder.Serialize(), _chainId).Should().Be(OrderType.Relay);
    [Fact] public void GetOrderTypeFromEncoded_UnsignedV2() => _uniswapXOrderParser.GetOrderTypeFromEncoded(_unsignedV2DutchOrder.Serialize(), _chainId).Should().Be(OrderType.Dutch_V2);
    [Fact] public void GetOrderTypeFromEncoded_CosignedV2() => _uniswapXOrderParser.GetOrderTypeFromEncoded(_cosignedV2DutchOrder.Serialize(), _chainId).Should().Be(OrderType.Dutch_V2);
    [Fact] public void GetOrderTypeFromEncoded_UnsignedV3() => _uniswapXOrderParser.GetOrderTypeFromEncoded(_unsignedV3DutchOrder.Serialize(), _blockBasedChainId).Should().Be(OrderType.Dutch_V3);
    [Fact] public void GetOrderTypeFromEncoded_CosignedV3() => _uniswapXOrderParser.GetOrderTypeFromEncoded(_cosignedV3DutchOrder.Serialize(), _blockBasedChainId).Should().Be(OrderType.Dutch_V3);
    [Fact] public void GetOrderTypeFromEncoded_UnsignedPriority() => _uniswapXOrderParser.GetOrderTypeFromEncoded(_unsignedPriorityOrder.Serialize(), _priorityChainId).Should().Be(OrderType.Priority);
    [Fact] public void GetOrderTypeFromEncoded_CosignedPriority() => _uniswapXOrderParser.GetOrderTypeFromEncoded(_cosignedPriorityOrder.Serialize(), _priorityChainId).Should().Be(OrderType.Priority);

    [Fact]
    public void GetOrderTypeFromEncoded_UnsignedHybrid()
    {
        using var _ = WithHybridResolver();
        _uniswapXOrderParser.GetOrderTypeFromEncoded(_unsignedHybridOrder.Serialize(), _chainId).Should().Be(OrderType.Hybrid);
    }

    [Fact]
    public void GetOrderTypeFromEncoded_CosignedHybrid()
    {
        using var _ = WithHybridResolver();
        _uniswapXOrderParser.GetOrderTypeFromEncoded(_cosignedHybridOrder.Serialize(), _chainId).Should().Be(OrderType.Hybrid);
    }
}
