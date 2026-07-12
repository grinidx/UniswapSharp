using System.Numerics;
using UniswapSharp.UniswapX.Order.V4;

namespace UniswapSharp.UniswapX.Builder;

/// <summary>Port of uniswapx-sdk <c>builder/HybridOrderBuilder.ts</c>.</summary>
public sealed class HybridOrderBuilder
{
    private const string AddressZero = "0x0000000000000000000000000000000000000000";

    private readonly int _chainId;
    private readonly string _resolver;
    private readonly string? _permit2Address;

    // OrderInfoV4 state
    private string _reactor;
    private string? _swapper;
    private BigInteger? _nonce;
    private long? _deadline;
    private string _preExecutionHook = AddressZero;
    private string _preExecutionHookData = "0x";
    private string _postExecutionHook = AddressZero;
    private string _postExecutionHookData = "0x";
    private string _auctionResolver;

    private string? _cosigner;
    private HybridInput? _input;
    private readonly List<HybridOutput> _outputs = new();
    private BigInteger? _auctionStartBlock;
    private BigInteger? _baselinePriorityFee;
    private BigInteger? _scalingFactor;
    private IReadOnlyList<BigInteger>? _priceCurve;
    private HybridCosignerData _cosignerData = DefaultCosignerData();
    private string? _cosignature;

    public static HybridOrderBuilder FromOrder(UnsignedHybridOrder order, string? resolver = null)
    {
        var builder = new HybridOrderBuilder(order.ChainId, order.Info.Reactor, resolver ?? order.Resolver, order.Permit2Address);
        builder
            .Cosigner(order.Info.Cosigner)
            .Input(order.Info.Input)
            .Deadline(order.Info.Deadline)
            .Nonce(order.Info.Nonce)
            .Swapper(order.Info.Swapper)
            .AuctionStartBlock(order.Info.AuctionStartBlock)
            .BaselinePriorityFee(order.Info.BaselinePriorityFee)
            .ScalingFactor(order.Info.ScalingFactor)
            .PriceCurve(order.Info.PriceCurve)
            .PreExecutionHook(order.Info.PreExecutionHook, order.Info.PreExecutionHookData)
            .PostExecutionHook(order.Info.PostExecutionHook, order.Info.PostExecutionHookData)
            .AuctionResolver(order.Info.AuctionResolver);
        foreach (var output in order.Info.Outputs)
        {
            builder.Output(output);
        }
        if (order is CosignedHybridOrder cosigned)
        {
            builder.CosignerData(cosigned.Info.CosignerData);
            if (cosigned.Info.Cosignature != "0x")
            {
                builder.Cosignature(cosigned.Info.Cosignature);
            }
        }
        return builder;
    }

    public HybridOrderBuilder(int chainId, string reactor, string resolver, string? permit2Address = null)
    {
        _chainId = chainId;
        _resolver = resolver;
        _permit2Address = permit2Address;
        _reactor = reactor;
        _auctionResolver = resolver;
    }

    private static HybridCosignerData DefaultCosignerData() => new()
    {
        AuctionTargetBlock = 0,
        SupplementalPriceCurve = new List<BigInteger>(),
        ExclusiveFiller = AddressZero,
        ExclusivityOverrideBps = 0,
        ExclusivityEndBlock = 0,
    };

    private static void ValidatePriceCurve(IReadOnlyList<BigInteger> curve, string prefix)
    {
        for (int i = 0; i < curve.Count; i++)
        {
            if (curve[i] < 0)
            {
                throw new InvalidOperationException($"{prefix} curve element {i} must be non-negative");
            }
        }
    }

    public HybridOrderBuilder Reactor(string reactor) { _reactor = reactor; return this; }
    public HybridOrderBuilder Swapper(string swapper) { _swapper = swapper; return this; }
    public HybridOrderBuilder Nonce(BigInteger nonce) { _nonce = nonce; return this; }
    public HybridOrderBuilder Deadline(long deadline) { _deadline = deadline; return this; }
    public HybridOrderBuilder AuctionResolver(string resolver) { _auctionResolver = resolver; return this; }
    public HybridOrderBuilder Cosigner(string cosigner) { _cosigner = cosigner; return this; }
    public HybridOrderBuilder Cosignature(string cosignature) { _cosignature = cosignature; return this; }
    public HybridOrderBuilder Input(HybridInput input) { _input = input; return this; }
    public HybridOrderBuilder Output(HybridOutput output) { _outputs.Add(output); return this; }
    public HybridOrderBuilder AuctionStartBlock(BigInteger block) { _auctionStartBlock = block; return this; }
    public HybridOrderBuilder BaselinePriorityFee(BigInteger fee) { _baselinePriorityFee = fee; return this; }
    public HybridOrderBuilder ScalingFactor(BigInteger factor) { _scalingFactor = factor; return this; }
    public HybridOrderBuilder CosignerData(HybridCosignerData data) { _cosignerData = data; return this; }

    public HybridOrderBuilder PreExecutionHook(string hook, string? hookData = null)
    {
        _preExecutionHook = hook;
        if (hookData != null)
        {
            _preExecutionHookData = hookData;
        }
        return this;
    }

    public HybridOrderBuilder PostExecutionHook(string hook, string? hookData = null)
    {
        _postExecutionHook = hook;
        if (hookData != null)
        {
            _postExecutionHookData = hookData;
        }
        return this;
    }

    public HybridOrderBuilder PriceCurve(IReadOnlyList<BigInteger> curve)
    {
        ValidatePriceCurve(curve, "Price");
        _priceCurve = curve;
        return this;
    }

    public HybridOrderBuilder AuctionTargetBlock(BigInteger block) { _cosignerData.AuctionTargetBlock = block; return this; }

    public HybridOrderBuilder SupplementalPriceCurve(IReadOnlyList<BigInteger> curve)
    {
        ValidatePriceCurve(curve, "Supplemental price");
        _cosignerData.SupplementalPriceCurve = curve;
        return this;
    }

    public HybridOrderBuilder ExclusiveFiller(string exclusiveFiller) { _cosignerData.ExclusiveFiller = exclusiveFiller; return this; }
    public HybridOrderBuilder ExclusivityOverrideBps(BigInteger bps) { _cosignerData.ExclusivityOverrideBps = bps; return this; }
    public HybridOrderBuilder ExclusivityEndBlock(BigInteger block) { _cosignerData.ExclusivityEndBlock = block; return this; }

    private static readonly BigInteger BaseScalingFactor = BigInteger.Pow(10, 18);

    private static BigInteger ExtractScalingFactor(BigInteger curveElement)
    {
        BigInteger mask = (BigInteger.One << 240) - 1;
        return curveElement & mask;
    }

    private void CheckUnsignedInvariants()
    {
        BuilderInvariant.Check(_swapper is not null, "swapper not set");
        BuilderInvariant.Check(_nonce is not null, "nonce not set");
        BuilderInvariant.Check(_deadline is not null, "deadline not set");
        BuilderInvariant.Check(_deadline > BuilderInvariant.NowSeconds, $"Deadline must be in the future: {_deadline}");
        BuilderInvariant.Check(_input is not null, "input not set");
        BuilderInvariant.Check(_outputs.Count > 0, "outputs not set");
        BuilderInvariant.Check(_auctionStartBlock is not null, "auctionStartBlock not set");
        BuilderInvariant.Check(_baselinePriorityFee is not null, "baselinePriorityFee not set");
        BuilderInvariant.Check(_scalingFactor is not null, "scalingFactor not set");
        BuilderInvariant.Check(_priceCurve is not null, "priceCurve not set");

        if (_priceCurve!.Count > 0)
        {
            for (int i = 1; i < _priceCurve.Count; i++)
            {
                BigInteger prevScaling = ExtractScalingFactor(_priceCurve[i - 1]);
                BigInteger scaling = ExtractScalingFactor(_priceCurve[i]);
                bool sharesDirection = prevScaling == BaseScalingFactor || scaling == BaseScalingFactor ||
                    prevScaling > BaseScalingFactor == scaling > BaseScalingFactor;
                BuilderInvariant.Check(sharesDirection,
                    $"Price curve scaling factors must share direction. Element {i} violates this.");
            }
        }

        BuilderInvariant.Check(_input!.MaxAmount > 0, "input maxAmount must be greater than 0");
        for (int i = 0; i < _outputs.Count; i++)
        {
            BuilderInvariant.Check(_outputs[i].MinAmount > 0, $"output {i} minAmount must be greater than 0");
        }
        BuilderInvariant.Check(_baselinePriorityFee >= 0, "baselinePriorityFee must be non-negative");
    }

    private void CheckCosignedInvariants()
    {
        BuilderInvariant.Check(_cosignature is not null && _cosignature != "0x", "cosignature not set");
    }

    public UnsignedHybridOrder BuildPartial()
    {
        CheckUnsignedInvariants();
        return new UnsignedHybridOrder(BuildInfo(_cosigner ?? AddressZero), _chainId, _resolver, _permit2Address);
    }

    public CosignedHybridOrder Build()
    {
        CheckUnsignedInvariants();
        CheckCosignedInvariants();
        var baseInfo = BuildInfo(_cosigner!);
        return new CosignedHybridOrder(
            new CosignedHybridOrderInfo
            {
                Reactor = baseInfo.Reactor,
                Swapper = baseInfo.Swapper,
                Nonce = baseInfo.Nonce,
                Deadline = baseInfo.Deadline,
                PreExecutionHook = baseInfo.PreExecutionHook,
                PreExecutionHookData = baseInfo.PreExecutionHookData,
                PostExecutionHook = baseInfo.PostExecutionHook,
                PostExecutionHookData = baseInfo.PostExecutionHookData,
                AuctionResolver = baseInfo.AuctionResolver,
                Cosigner = baseInfo.Cosigner,
                Input = baseInfo.Input,
                Outputs = baseInfo.Outputs,
                AuctionStartBlock = baseInfo.AuctionStartBlock,
                BaselinePriorityFee = baseInfo.BaselinePriorityFee,
                ScalingFactor = baseInfo.ScalingFactor,
                PriceCurve = baseInfo.PriceCurve,
                CosignerData = _cosignerData,
                Cosignature = _cosignature!,
            },
            _chainId,
            _resolver,
            _permit2Address);
    }

    private UnsignedHybridOrderInfo BuildInfo(string cosigner) => new()
    {
        Reactor = _reactor,
        Swapper = _swapper!,
        Nonce = _nonce!.Value,
        Deadline = _deadline!.Value,
        PreExecutionHook = _preExecutionHook,
        PreExecutionHookData = _preExecutionHookData,
        PostExecutionHook = _postExecutionHook,
        PostExecutionHookData = _postExecutionHookData,
        AuctionResolver = _auctionResolver,
        Cosigner = cosigner,
        Input = _input!,
        Outputs = _outputs.ToList(),
        AuctionStartBlock = _auctionStartBlock!.Value,
        BaselinePriorityFee = _baselinePriorityFee!.Value,
        ScalingFactor = _scalingFactor!.Value,
        PriceCurve = _priceCurve!,
    };
}
