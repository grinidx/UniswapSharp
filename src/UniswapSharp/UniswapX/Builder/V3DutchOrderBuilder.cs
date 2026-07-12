using System.Numerics;
using UniswapSharp.UniswapX.Order;
using UniswapSharp.UniswapX.Utils;

namespace UniswapSharp.UniswapX.Builder;

/// <summary>Port of uniswapx-sdk <c>builder/V3DutchOrderBuilder.ts</c>.</summary>
public sealed class V3DutchOrderBuilder
{
    private const string AddressZero = "0x0000000000000000000000000000000000000000";

    private readonly int _chainId;
    private readonly string _permit2Address;

    private string? _reactor;
    private string? _swapper;
    private BigInteger? _nonce;
    private long? _deadline;
    private string _additionalValidationContract = AddressZero;
    private string _additionalValidationData = "0x";

    private string? _cosigner;
    private string? _cosignature;
    private BigInteger? _startingBaseFee;
    private V3DutchInput? _input;
    private readonly List<V3DutchOutput> _outputs = new();
    private V3CosignerData _cosignerData = DefaultCosignerData();

    public static V3DutchOrderBuilder FromOrder(UnsignedV3DutchOrder order)
    {
        var builder = new V3DutchOrderBuilder(order.ChainId, order.Info.Reactor);
        builder
            .Cosigner(order.Info.Cosigner)
            .StartingBaseFee(order.Info.StartingBaseFee)
            .Input(order.Info.Input)
            .Deadline(order.Info.Deadline)
            .Nonce(order.Info.Nonce)
            .Swapper(order.Info.Swapper)
            .Validation(new ValidationInfo
            {
                AdditionalValidationContract = order.Info.AdditionalValidationContract,
                AdditionalValidationData = order.Info.AdditionalValidationData,
            });
        foreach (var output in order.Info.Outputs)
        {
            builder.Output(output);
        }
        if (order is CosignedV3DutchOrder cosigned)
        {
            builder.Cosignature(cosigned.Info.Cosignature);
            builder.DecayStartBlock(cosigned.Info.CosignerData.DecayStartBlock);
            builder.ExclusiveFiller(cosigned.Info.CosignerData.ExclusiveFiller);
            builder.InputOverride(cosigned.Info.CosignerData.InputOverride);
            builder.ExclusivityOverrideBps(cosigned.Info.CosignerData.ExclusivityOverrideBps);
            builder.OutputOverrides(cosigned.Info.CosignerData.OutputOverrides);
        }
        return builder;
    }

    public V3DutchOrderBuilder(int chainId, string? reactorAddress = null, string? permit2Address = null)
    {
        _chainId = chainId;
        _reactor = OrderUtils.GetReactor(chainId, OrderType.Dutch_V3, reactorAddress);
        _permit2Address = OrderUtils.GetPermit2(chainId, permit2Address);
    }

    private static V3CosignerData DefaultCosignerData() => new()
    {
        DecayStartBlock = 0,
        ExclusiveFiller = AddressZero,
        ExclusivityOverrideBps = 0,
        InputOverride = 0,
        OutputOverrides = new List<BigInteger>(),
    };

    public V3DutchOrderBuilder Cosigner(string cosigner) { _cosigner = cosigner; return this; }
    public V3DutchOrderBuilder Cosignature(string? cosignature) { _cosignature = cosignature; return this; }
    public V3DutchOrderBuilder StartingBaseFee(BigInteger startingBaseFee) { _startingBaseFee = startingBaseFee; return this; }
    public V3DutchOrderBuilder Input(V3DutchInput input) { _input = input; return this; }
    public V3DutchOrderBuilder Output(V3DutchOutput output) { _outputs.Add(output); return this; }
    public V3DutchOrderBuilder DecayStartBlock(long decayStartBlock) { _cosignerData.DecayStartBlock = decayStartBlock; return this; }
    public V3DutchOrderBuilder InputOverride(BigInteger inputOverride) { _cosignerData.InputOverride = inputOverride; return this; }
    public V3DutchOrderBuilder OutputOverrides(IReadOnlyList<BigInteger> outputOverrides) { _cosignerData.OutputOverrides = outputOverrides; return this; }
    public V3DutchOrderBuilder ExclusiveFiller(string exclusiveFiller) { _cosignerData.ExclusiveFiller = exclusiveFiller; return this; }
    public V3DutchOrderBuilder ExclusivityOverrideBps(BigInteger bps) { _cosignerData.ExclusivityOverrideBps = bps; return this; }
    public V3DutchOrderBuilder Deadline(long deadline) { _deadline = deadline; return this; }
    public V3DutchOrderBuilder Swapper(string swapper) { _swapper = swapper; return this; }
    public V3DutchOrderBuilder Nonce(BigInteger nonce) { _nonce = nonce; return this; }

    public V3DutchOrderBuilder Validation(ValidationInfo info)
    {
        _additionalValidationContract = info.AdditionalValidationContract;
        _additionalValidationData = info.AdditionalValidationData;
        return this;
    }

    public V3DutchOrderBuilder CosignerData(V3CosignerData cosignerData)
    {
        DecayStartBlock(cosignerData.DecayStartBlock);
        ExclusiveFiller(cosignerData.ExclusiveFiller);
        ExclusivityOverrideBps(cosignerData.ExclusivityOverrideBps);
        InputOverride(cosignerData.InputOverride);
        OutputOverrides(cosignerData.OutputOverrides);
        return this;
    }

    public V3DutchOrderBuilder NonFeeRecipient(string newRecipient, string? feeRecipient = null)
    {
        BuilderInvariant.Check(newRecipient != feeRecipient,
            $"newRecipient must be different from feeRecipient: {newRecipient}");
        for (int i = 0; i < _outputs.Count; i++)
        {
            var output = _outputs[i];
            if (feeRecipient != null && output.Recipient.ToLowerInvariant() == feeRecipient.ToLowerInvariant())
            {
                continue;
            }
            _outputs[i] = output with { Recipient = newRecipient };
        }
        return this;
    }

    private static bool IsRelativeBlocksIncreasing(IReadOnlyList<int> relativeBlocks)
    {
        int prev = 0;
        foreach (var block in relativeBlocks)
        {
            if (block <= prev)
            {
                return false;
            }
            prev = block;
        }
        return true;
    }

    private void CheckUnsignedInvariants()
    {
        BuilderInvariant.Check(_cosigner is not null, "cosigner not set");
        BuilderInvariant.Check(_startingBaseFee is not null, "startingBaseFee not set");
        BuilderInvariant.Check(_input is not null, "input not set");
        BuilderInvariant.Check(_outputs.Count > 0, "outputs not set");
        BuilderInvariant.Check(_input!.Curve.RelativeAmounts.Count == _input.Curve.RelativeBlocks.Count,
            "relativeBlocks and relativeAmounts length mismatch");
        BuilderInvariant.Check(IsRelativeBlocksIncreasing(_input.Curve.RelativeBlocks), "relativeBlocks not strictly increasing");
        foreach (var output in _outputs)
        {
            BuilderInvariant.Check(output.Curve.RelativeBlocks.Count == output.Curve.RelativeAmounts.Count,
                "relativeBlocks and relativeAmounts length mismatch");
            BuilderInvariant.Check(IsRelativeBlocksIncreasing(output.Curve.RelativeBlocks), "relativeBlocks not strictly increasing");
        }
        BuilderInvariant.Check(_deadline is not null, "deadline not set");
        BuilderInvariant.Check(_swapper is not null, "swapper not set");
    }

    private void CheckCosignedInvariants()
    {
        BuilderInvariant.Check(_cosignerData.OutputOverrides.Count > 0, "outputOverrides not set");
        BuilderInvariant.Check(_cosignerData.InputOverride <= _input!.StartAmount, "inputOverride larger than original input");
        for (int idx = 0; idx < _cosignerData.OutputOverrides.Count; idx++)
        {
            if (_cosignerData.OutputOverrides[idx].ToString() != "0")
            {
                BuilderInvariant.Check(_cosignerData.OutputOverrides[idx] >= _outputs[idx].StartAmount,
                    "outputOverride smaller than original output");
            }
        }
    }

    public UnsignedV3DutchOrder BuildPartial()
    {
        CheckUnsignedInvariants();
        var info = GetOrderInfo();
        return new UnsignedV3DutchOrder(
            new UnsignedV3DutchOrderInfo
            {
                Reactor = info.Reactor,
                Swapper = info.Swapper,
                Nonce = info.Nonce,
                Deadline = info.Deadline,
                AdditionalValidationContract = info.AdditionalValidationContract,
                AdditionalValidationData = info.AdditionalValidationData,
                Input = _input!,
                Outputs = _outputs.ToList(),
                Cosigner = _cosigner!,
                StartingBaseFee = _startingBaseFee!.Value,
            },
            _chainId,
            _permit2Address);
    }

    public CosignedV3DutchOrder Build()
    {
        BuilderInvariant.Check(_cosignature is not null, "cosignature not set");
        CheckUnsignedInvariants();
        CheckCosignedInvariants();
        var info = GetOrderInfo();
        return new CosignedV3DutchOrder(
            new CosignedV3DutchOrderInfo
            {
                Reactor = info.Reactor,
                Swapper = info.Swapper,
                Nonce = info.Nonce,
                Deadline = info.Deadline,
                AdditionalValidationContract = info.AdditionalValidationContract,
                AdditionalValidationData = info.AdditionalValidationData,
                CosignerData = _cosignerData,
                StartingBaseFee = _startingBaseFee!.Value,
                Input = _input!,
                Outputs = _outputs.ToList(),
                Cosigner = _cosigner!,
                Cosignature = _cosignature!,
            },
            _chainId,
            _permit2Address);
    }

    private OrderInfo GetOrderInfo()
    {
        BuilderInvariant.Check(_reactor is not null, "reactor not set");
        BuilderInvariant.Check(_nonce is not null, "nonce not set");
        BuilderInvariant.Check(_deadline is not null, "deadline not set");
        BuilderInvariant.Check(_deadline > BuilderInvariant.NowSeconds, $"Deadline must be in the future: {_deadline}");
        BuilderInvariant.Check(_swapper is not null, "swapper not set");
        return new OrderInfo
        {
            Reactor = _reactor!,
            Swapper = _swapper!,
            Nonce = _nonce!.Value,
            Deadline = _deadline!.Value,
            AdditionalValidationContract = _additionalValidationContract,
            AdditionalValidationData = _additionalValidationData,
        };
    }

    /// <summary>Helper to find the value to pass to <c>maxAmount</c> in an input (uniswapx-sdk <c>getMaxAmountOut</c>).</summary>
    public static BigInteger GetMaxAmountOut(BigInteger startAmount, IReadOnlyList<BigInteger> relativeAmounts)
    {
        if (relativeAmounts.Count == 0)
        {
            return startAmount;
        }
        BigInteger minRelative = relativeAmounts.Aggregate(BigInteger.Zero, (min, a) => a < min ? a : min);
        return startAmount - minRelative;
    }

    /// <summary>Helper to find the lowest possible output amount (uniswapx-sdk <c>getMinAmountOut</c>).</summary>
    public static BigInteger GetMinAmountOut(BigInteger startAmount, IReadOnlyList<BigInteger> relativeAmounts)
    {
        if (relativeAmounts.Count == 0)
        {
            return startAmount;
        }
        BigInteger maxRelative = relativeAmounts.Aggregate(BigInteger.Zero, (max, a) => a > max ? a : max);
        return startAmount - maxRelative;
    }
}
