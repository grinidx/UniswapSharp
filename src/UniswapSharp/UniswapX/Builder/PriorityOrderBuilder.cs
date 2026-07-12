using System.Numerics;
using UniswapSharp.UniswapX.Order;
using UniswapSharp.UniswapX.Utils;

namespace UniswapSharp.UniswapX.Builder;

/// <summary>Port of uniswapx-sdk <c>builder/PriorityOrderBuilder.ts</c>.</summary>
public sealed class PriorityOrderBuilder
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
    private BigInteger? _auctionStartBlock;
    private BigInteger? _baselinePriorityFeeWei;
    private PriorityInput? _input;
    private readonly List<PriorityOutput> _outputs = new();
    private PriorityCosignerData _cosignerData = new() { AuctionTargetBlock = 0 };

    public static PriorityOrderBuilder FromOrder(UnsignedPriorityOrder order)
    {
        var builder = new PriorityOrderBuilder(order.ChainId, order.Info.Reactor, order.Permit2Address)
            .Deadline(order.Info.Deadline)
            .Swapper(order.Info.Swapper)
            .Nonce(order.Info.Nonce)
            .Input(order.Info.Input)
            .AuctionStartBlock(order.Info.AuctionStartBlock)
            .BaselinePriorityFeeWei(order.Info.BaselinePriorityFeeWei)
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
        if (order is CosignedPriorityOrder cosigned)
        {
            builder.Cosignature(cosigned.Info.Cosignature);
            builder.AuctionTargetBlock(cosigned.Info.CosignerData.AuctionTargetBlock);
        }
        return builder;
    }

    public PriorityOrderBuilder(int chainId, string? reactorAddress = null, string? permit2Address = null)
    {
        _chainId = chainId;
        _reactor = OrderUtils.GetReactor(chainId, OrderType.Priority, reactorAddress);
        _permit2Address = OrderUtils.GetPermit2(chainId, permit2Address);
    }

    public PriorityOrderBuilder Cosigner(string cosigner) { _cosigner = cosigner; return this; }
    public PriorityOrderBuilder AuctionStartBlock(BigInteger auctionStartBlock) { _auctionStartBlock = auctionStartBlock; return this; }
    public PriorityOrderBuilder BaselinePriorityFeeWei(BigInteger fee) { _baselinePriorityFeeWei = fee; return this; }
    public PriorityOrderBuilder CosignerData(PriorityCosignerData cosignerData) { _cosignerData = cosignerData; return this; }
    public PriorityOrderBuilder Cosignature(string? cosignature) { _cosignature = cosignature; return this; }
    public PriorityOrderBuilder Input(PriorityInput input) { _input = input; return this; }
    public PriorityOrderBuilder Output(PriorityOutput output) { _outputs.Add(output); return this; }
    public PriorityOrderBuilder Deadline(long deadline) { _deadline = deadline; return this; }
    public PriorityOrderBuilder Swapper(string swapper) { _swapper = swapper; return this; }
    public PriorityOrderBuilder Nonce(BigInteger nonce) { _nonce = nonce; return this; }

    public PriorityOrderBuilder AuctionTargetBlock(BigInteger auctionTargetBlock)
    {
        _cosignerData.AuctionTargetBlock = auctionTargetBlock;
        return this;
    }

    public PriorityOrderBuilder Validation(ValidationInfo info)
    {
        _additionalValidationContract = info.AdditionalValidationContract;
        _additionalValidationData = info.AdditionalValidationData;
        return this;
    }

    public PriorityOrderBuilder NonFeeRecipient(string newRecipient, string? feeRecipient = null)
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

    public UnsignedPriorityOrder BuildPartial()
    {
        BuilderInvariant.Check(_input is not null, "input not set");
        BuilderInvariant.Check(_cosigner is not null, "cosigner not set");
        BuilderInvariant.Check(_baselinePriorityFeeWei is not null, "baselinePriorityFeeWei not set");
        BuilderInvariant.Check(_outputs.Count != 0, "outputs not set");
        BuilderInvariant.Check(_auctionStartBlock is not null && _auctionStartBlock > 0, "auctionStartBlock not set");
        BuilderInvariant.Check(_input!.MpsPerPriorityFeeWei != 0 || _outputs.All(o => o.MpsPerPriorityFeeWei != 0),
            "Priority auction not configured");
        BuilderInvariant.Check(!(_input.MpsPerPriorityFeeWei > 0 && _outputs.All(o => o.MpsPerPriorityFeeWei > 0)),
            "Can only configure priority auction on either input or output");

        var info = GetOrderInfo();
        return new UnsignedPriorityOrder(
            new UnsignedPriorityOrderInfo
            {
                Reactor = info.Reactor,
                Swapper = info.Swapper,
                Nonce = info.Nonce,
                Deadline = info.Deadline,
                AdditionalValidationContract = info.AdditionalValidationContract,
                AdditionalValidationData = info.AdditionalValidationData,
                Cosigner = _cosigner!,
                AuctionStartBlock = _auctionStartBlock!.Value,
                BaselinePriorityFeeWei = _baselinePriorityFeeWei!.Value,
                Input = _input!,
                Outputs = _outputs.ToList(),
            },
            _chainId,
            _permit2Address);
    }

    public CosignedPriorityOrder Build()
    {
        BuilderInvariant.Check(_input is not null, "input not set");
        BuilderInvariant.Check(_cosigner is not null, "cosigner not set");
        BuilderInvariant.Check(_cosignature is not null, "cosignature not set");
        BuilderInvariant.Check(_baselinePriorityFeeWei is not null, "baselinePriorityFeeWei not set");
        BuilderInvariant.Check(_outputs.Count != 0, "outputs not set");
        BuilderInvariant.Check(_auctionStartBlock is not null && _auctionStartBlock > 0, "auctionStartBlock not set");
        BuilderInvariant.Check(
            _cosignerData.AuctionTargetBlock > 0 && _cosignerData.AuctionTargetBlock <= _auctionStartBlock,
            "auctionTargetBlock not set properly");
        BuilderInvariant.Check(_input!.MpsPerPriorityFeeWei != 0 || _outputs.All(o => o.MpsPerPriorityFeeWei != 0),
            "Priority auction not configured");
        BuilderInvariant.Check(!(_input.MpsPerPriorityFeeWei > 0 && _outputs.Any(o => o.MpsPerPriorityFeeWei > 0)),
            "Can only configure priority auction on either input or output");

        var info = GetOrderInfo();
        return new CosignedPriorityOrder(
            new CosignedPriorityOrderInfo
            {
                Reactor = info.Reactor,
                Swapper = info.Swapper,
                Nonce = info.Nonce,
                Deadline = info.Deadline,
                AdditionalValidationContract = info.AdditionalValidationContract,
                AdditionalValidationData = info.AdditionalValidationData,
                Cosigner = _cosigner!,
                AuctionStartBlock = _auctionStartBlock!.Value,
                BaselinePriorityFeeWei = _baselinePriorityFeeWei!.Value,
                Input = _input!,
                Outputs = _outputs.ToList(),
                CosignerData = _cosignerData,
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
