using System.Numerics;
using UniswapSharp.UniswapX.Order;

namespace UniswapSharp.UniswapX.Builder;

/// <summary>Port of uniswapx-sdk <c>builder/DutchOrderBuilder.ts</c>.</summary>
public sealed class DutchOrderBuilder
{
    private const string AddressZero = "0x0000000000000000000000000000000000000000";

    private readonly int _chainId;
    private readonly string? _permit2Address;

    // OrderInfo state
    private string? _reactor;
    private string? _swapper;
    private BigInteger? _nonce;
    private long? _deadline;
    private string _additionalValidationContract = AddressZero;
    private string _additionalValidationData = "0x";

    // Dutch-specific state
    private long? _decayStartTime;
    private long? _decayEndTime;
    private string _exclusiveFiller = AddressZero;
    private BigInteger _exclusivityOverrideBps = BigInteger.Zero;
    private DutchInput? _input;
    private readonly List<DutchOutput> _outputs = new();

    public static DutchOrderBuilder FromOrder(DutchOrder order)
    {
        var builder = new DutchOrderBuilder(order.ChainId, order.Info.Reactor)
            .Deadline(order.Info.Deadline)
            .DecayEndTime(order.Info.DecayEndTime)
            .DecayStartTime(order.Info.DecayStartTime)
            .Swapper(order.Info.Swapper)
            .Nonce(order.Info.Nonce)
            .Input(order.Info.Input)
            .ExclusiveFiller(order.Info.ExclusiveFiller, order.Info.ExclusivityOverrideBps)
            .Validation(new ValidationInfo
            {
                AdditionalValidationContract = order.Info.AdditionalValidationContract,
                AdditionalValidationData = order.Info.AdditionalValidationData,
            });
        foreach (var output in order.Info.Outputs)
        {
            builder.Output(output);
        }
        return builder;
    }

    public DutchOrderBuilder(int chainId, string? reactorAddress = null, string? permit2Address = null)
    {
        _chainId = chainId;
        _permit2Address = permit2Address;

        string? mapped = null;
        if (Constants.ReactorAddressMapping.TryGetValue(chainId, out var reactors) &&
            reactors.TryGetValue(OrderType.Dutch, out var addr))
        {
            mapped = addr;
        }
        if (reactorAddress != null)
        {
            _reactor = reactorAddress;
        }
        else if (mapped != null)
        {
            _reactor = mapped;
        }
        else
        {
            throw new MissingConfiguration("reactor", chainId.ToString());
        }
    }

    public DutchOrderBuilder DecayStartTime(long decayStartTime)
    {
        _decayStartTime = decayStartTime;
        return this;
    }

    public DutchOrderBuilder DecayEndTime(long decayEndTime)
    {
        _deadline ??= decayEndTime;
        _decayEndTime = decayEndTime;
        return this;
    }

    public DutchOrderBuilder Input(DutchInput input)
    {
        _input = input;
        return this;
    }

    public DutchOrderBuilder Output(DutchOutput output)
    {
        BuilderInvariant.Check(output.StartAmount >= output.EndAmount,
            $"startAmount must be greater than endAmount: {output.StartAmount}");
        _outputs.Add(output);
        return this;
    }

    public DutchOrderBuilder Deadline(long deadline)
    {
        _deadline = deadline;
        if (_decayEndTime is null)
        {
            DecayEndTime(deadline);
        }
        return this;
    }

    public DutchOrderBuilder Swapper(string swapper)
    {
        _swapper = swapper;
        return this;
    }

    public DutchOrderBuilder Nonce(BigInteger nonce)
    {
        _nonce = nonce;
        return this;
    }

    public DutchOrderBuilder Validation(ValidationInfo info)
    {
        _additionalValidationContract = info.AdditionalValidationContract;
        _additionalValidationData = info.AdditionalValidationData;
        return this;
    }

    public DutchOrderBuilder NonFeeRecipient(string newRecipient, string? feeRecipient = null)
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

    public DutchOrderBuilder ExclusiveFiller(string exclusiveFiller, BigInteger exclusivityOverrideBps)
    {
        _exclusiveFiller = exclusiveFiller;
        _exclusivityOverrideBps = exclusivityOverrideBps;
        return this;
    }

    public DutchOrder Build()
    {
        BuilderInvariant.Check(_decayStartTime is not null, "decayStartTime not set");
        BuilderInvariant.Check(_input is not null, "input not set");
        BuilderInvariant.Check(_decayEndTime is not null, "decayEndTime not set");
        BuilderInvariant.Check(_outputs.Count != 0, "outputs not set");
        BuilderInvariant.Check(_deadline is null || _decayStartTime <= _deadline,
            $"decayStartTime must be before or same as deadline: {_decayStartTime}");
        BuilderInvariant.Check(_deadline is null || _decayEndTime <= _deadline,
            $"decayEndTime must be before or same as deadline: {_decayEndTime}");

        var info = GetOrderInfo();
        return new DutchOrder(
            new DutchOrderInfo
            {
                Reactor = info.Reactor,
                Swapper = info.Swapper,
                Nonce = info.Nonce,
                Deadline = info.Deadline,
                AdditionalValidationContract = info.AdditionalValidationContract,
                AdditionalValidationData = info.AdditionalValidationData,
                DecayStartTime = _decayStartTime!.Value,
                DecayEndTime = _decayEndTime!.Value,
                ExclusiveFiller = _exclusiveFiller,
                ExclusivityOverrideBps = _exclusivityOverrideBps,
                Input = _input!,
                Outputs = _outputs.ToList(),
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
