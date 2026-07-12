using System.Numerics;
using UniswapSharp.UniswapX.Order;
using UniswapSharp.UniswapX.Utils;

namespace UniswapSharp.UniswapX.Builder;

/// <summary>Port of uniswapx-sdk <c>builder/V2DutchOrderBuilder.ts</c>.</summary>
public sealed class V2DutchOrderBuilder
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
    private DutchInput? _input;
    private readonly List<DutchOutput> _outputs = new();
    private CosignerData _cosignerData = DefaultCosignerData();

    public static V2DutchOrderBuilder FromOrder(UnsignedV2DutchOrder order)
    {
        var builder = new V2DutchOrderBuilder(order.ChainId, order.Info.Reactor)
            .Deadline(order.Info.Deadline)
            .Swapper(order.Info.Swapper)
            .Nonce(order.Info.Nonce)
            .Input(order.Info.Input)
            .Cosigner(order.Info.Cosigner)
            .Validation(new ValidationInfo
            {
                AdditionalValidationContract = order.Info.AdditionalValidationContract,
                AdditionalValidationData = order.Info.AdditionalValidationData,
            });
        foreach (var output in order.Info.Outputs)
        {
            builder.Output(output);
        }
        if (order is CosignedV2DutchOrder cosigned)
        {
            builder.Cosignature(cosigned.Info.Cosignature);
            builder.DecayEndTime(cosigned.Info.CosignerData.DecayEndTime);
            builder.DecayStartTime(cosigned.Info.CosignerData.DecayStartTime);
            builder.CosignerData(cosigned.Info.CosignerData);
        }
        return builder;
    }

    public V2DutchOrderBuilder(int chainId, string? reactorAddress = null, string? permit2Address = null)
    {
        _chainId = chainId;
        _reactor = OrderUtils.GetReactor(chainId, OrderType.Dutch_V2, reactorAddress);
        _permit2Address = OrderUtils.GetPermit2(chainId, permit2Address);
    }

    private static CosignerData DefaultCosignerData() => new()
    {
        DecayStartTime = 0,
        DecayEndTime = 0,
        ExclusiveFiller = AddressZero,
        ExclusivityOverrideBps = 0,
        InputOverride = 0,
        OutputOverrides = new List<BigInteger>(),
    };

    public V2DutchOrderBuilder DecayStartTime(long decayStartTime)
    {
        _cosignerData.DecayStartTime = decayStartTime;
        return this;
    }

    public V2DutchOrderBuilder DecayEndTime(long decayEndTime)
    {
        _cosignerData.DecayEndTime = decayEndTime;
        _deadline ??= decayEndTime;
        return this;
    }

    public V2DutchOrderBuilder Input(DutchInput input)
    {
        _input = input;
        return this;
    }

    public V2DutchOrderBuilder Output(DutchOutput output)
    {
        BuilderInvariant.Check(output.StartAmount >= output.EndAmount,
            $"startAmount must be greater than endAmount: {output.StartAmount}");
        _outputs.Add(output);
        return this;
    }

    public V2DutchOrderBuilder Deadline(long deadline)
    {
        _deadline = deadline;
        if (_cosignerData.DecayEndTime == 0)
        {
            DecayEndTime(deadline);
        }
        return this;
    }

    public V2DutchOrderBuilder Swapper(string swapper)
    {
        _swapper = swapper;
        return this;
    }

    public V2DutchOrderBuilder Nonce(BigInteger nonce)
    {
        _nonce = nonce;
        return this;
    }

    public V2DutchOrderBuilder Validation(ValidationInfo info)
    {
        _additionalValidationContract = info.AdditionalValidationContract;
        _additionalValidationData = info.AdditionalValidationData;
        return this;
    }

    public V2DutchOrderBuilder NonFeeRecipient(string newRecipient, string? feeRecipient = null)
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

    public V2DutchOrderBuilder ExclusiveFiller(string exclusiveFiller)
    {
        _cosignerData.ExclusiveFiller = exclusiveFiller;
        return this;
    }

    public V2DutchOrderBuilder ExclusivityOverrideBps(BigInteger exclusivityOverrideBps)
    {
        _cosignerData.ExclusivityOverrideBps = exclusivityOverrideBps;
        return this;
    }

    public V2DutchOrderBuilder InputOverride(BigInteger inputOverride)
    {
        _cosignerData.InputOverride = inputOverride;
        return this;
    }

    public V2DutchOrderBuilder OutputOverrides(IReadOnlyList<BigInteger> outputOverrides)
    {
        _cosignerData.OutputOverrides = outputOverrides;
        return this;
    }

    public V2DutchOrderBuilder Cosigner(string cosigner)
    {
        _cosigner = cosigner;
        return this;
    }

    public V2DutchOrderBuilder Cosignature(string? cosignature)
    {
        _cosignature = cosignature;
        return this;
    }

    public V2DutchOrderBuilder CosignerData(CosignerData cosignerData)
    {
        DecayStartTime(cosignerData.DecayStartTime);
        DecayEndTime(cosignerData.DecayEndTime);
        ExclusiveFiller(cosignerData.ExclusiveFiller);
        ExclusivityOverrideBps(cosignerData.ExclusivityOverrideBps);
        InputOverride(cosignerData.InputOverride);
        OutputOverrides(cosignerData.OutputOverrides);
        return this;
    }

    public UnsignedV2DutchOrder BuildPartial()
    {
        BuilderInvariant.Check(_cosigner is not null, "cosigner not set");
        BuilderInvariant.Check(_input is not null, "input not set");
        BuilderInvariant.Check(_outputs.Count > 0, "outputs not set");
        BuilderInvariant.Check(_deadline is null || _cosignerData.DecayStartTime <= _deadline,
            $"if present, decayStartTime must be before or same as deadline: {_cosignerData.DecayStartTime}");
        BuilderInvariant.Check(_deadline is null || _cosignerData.DecayEndTime <= _deadline,
            $"if present, decayEndTime must be before or same as deadline: {_cosignerData.DecayEndTime}");

        var info = GetOrderInfo();
        return new UnsignedV2DutchOrder(
            new UnsignedV2DutchOrderInfo
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
            },
            _chainId,
            _permit2Address);
    }

    public CosignedV2DutchOrder Build()
    {
        BuilderInvariant.Check(_cosigner is not null, "cosigner not set");
        BuilderInvariant.Check(_cosignature is not null, "cosignature not set");
        BuilderInvariant.Check(_input is not null, "input not set");
        BuilderInvariant.Check(_outputs.Count > 0, "outputs not set");
        BuilderInvariant.Check(_cosignerData.InputOverride <= _input!.StartAmount, "inputOverride larger than original input");
        BuilderInvariant.Check(_cosignerData.OutputOverrides.Count > 0, "outputOverrides not set");
        for (int idx = 0; idx < _cosignerData.OutputOverrides.Count; idx++)
        {
            BuilderInvariant.Check(_cosignerData.OutputOverrides[idx] >= _outputs[idx].StartAmount,
                "outputOverride must be larger than or equal to original output");
        }
        BuilderInvariant.Check(_deadline is null || _cosignerData.DecayStartTime <= _deadline,
            $"decayStartTime must be before or same as deadline: {_cosignerData.DecayStartTime}");
        BuilderInvariant.Check(_deadline is null || _cosignerData.DecayEndTime <= _deadline,
            $"decayEndTime must be before or same as deadline: {_cosignerData.DecayEndTime}");

        var info = GetOrderInfo();
        return new CosignedV2DutchOrder(
            new CosignedV2DutchOrderInfo
            {
                Reactor = info.Reactor,
                Swapper = info.Swapper,
                Nonce = info.Nonce,
                Deadline = info.Deadline,
                AdditionalValidationContract = info.AdditionalValidationContract,
                AdditionalValidationData = info.AdditionalValidationData,
                CosignerData = _cosignerData,
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
}
