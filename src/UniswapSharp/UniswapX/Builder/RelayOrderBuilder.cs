using System.Numerics;
using UniswapSharp.UniswapX.Order;

namespace UniswapSharp.UniswapX.Builder;

/// <summary>Port of uniswapx-sdk <c>builder/RelayOrderBuilder.ts</c>.</summary>
public sealed class RelayOrderBuilder
{
    private readonly int _chainId;
    private readonly string? _permit2Address;

    private string? _reactor;
    private string? _swapper;
    private BigInteger? _nonce;
    private long? _deadline;
    private string? _universalRouterCalldata;
    private RelayInput? _input;
    private RelayFee? _fee;

    public static RelayOrderBuilder FromOrder(RelayOrder order)
    {
        return new RelayOrderBuilder(order.ChainId, order.Info.Reactor)
            .Deadline(order.Info.Deadline)
            .Swapper(order.Info.Swapper)
            .Nonce(order.Info.Nonce)
            .UniversalRouterCalldata(order.Info.UniversalRouterCalldata)
            .Input(order.Info.Input)
            .Fee(order.Info.Fee)
            .FeeStartTime(order.Info.Fee.StartTime)
            .FeeEndTime(order.Info.Fee.EndTime);
    }

    public RelayOrderBuilder(int chainId, string? reactorAddress = null, string? permit2Address = null)
    {
        _chainId = chainId;
        _permit2Address = permit2Address;

        string? mapped = null;
        if (Constants.ReactorAddressMapping.TryGetValue(chainId, out var reactors) &&
            reactors.TryGetValue(OrderType.Relay, out var addr))
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

    public RelayOrderBuilder Deadline(long deadline)
    {
        _deadline = deadline;
        return this;
    }

    public RelayOrderBuilder Nonce(BigInteger nonce)
    {
        _nonce = nonce;
        return this;
    }

    public RelayOrderBuilder Swapper(string swapper)
    {
        _swapper = swapper;
        return this;
    }

    public RelayOrderBuilder UniversalRouterCalldata(string universalRouterCalldata)
    {
        _universalRouterCalldata = universalRouterCalldata;
        return this;
    }

    public RelayOrderBuilder FeeStartTime(long feeStartTime)
    {
        BuilderInvariant.Check(_fee is not null, "fee not set");
        _fee = _fee! with { StartTime = feeStartTime };
        return this;
    }

    public RelayOrderBuilder FeeEndTime(long feeEndTime)
    {
        BuilderInvariant.Check(_fee is not null, "fee not set");
        _deadline ??= feeEndTime;
        _fee = _fee! with { EndTime = feeEndTime };
        return this;
    }

    public RelayOrderBuilder Input(RelayInput input)
    {
        _input = input;
        return this;
    }

    public RelayOrderBuilder Fee(RelayFee fee)
    {
        BuilderInvariant.Check(fee.StartAmount <= fee.EndAmount,
            $"startAmount must be less than or equal than endAmount: {fee.StartAmount}");
        _fee = fee;
        return this;
    }

    public RelayOrder Build()
    {
        BuilderInvariant.Check(_reactor is not null, "reactor not set");
        BuilderInvariant.Check(_nonce is not null, "nonce not set");
        BuilderInvariant.Check(_deadline is not null, "deadline not set");
        BuilderInvariant.Check(_deadline > BuilderInvariant.NowSeconds, $"Deadline must be in the future: {_deadline}");
        BuilderInvariant.Check(_swapper is not null, "swapper not set");
        BuilderInvariant.Check(_universalRouterCalldata is not null, "universalRouterCalldata not set");
        BuilderInvariant.Check(_input is not null, "input not set");
        BuilderInvariant.Check(_fee is not null, "fee not set");
        BuilderInvariant.Check(_deadline is null || _fee!.StartTime <= _deadline,
            $"feeStartTime must be before or same as deadline: {_fee!.StartTime}");
        BuilderInvariant.Check(_deadline is null || _fee!.EndTime <= _deadline,
            $"feeEndTime must be before or same as deadline: {_fee!.EndTime}");

        return new RelayOrder(
            new RelayOrderInfo
            {
                Reactor = _reactor!,
                Swapper = _swapper!,
                Nonce = _nonce!.Value,
                Deadline = _deadline!.Value,
                Input = _input!,
                Fee = _fee!,
                UniversalRouterCalldata = _universalRouterCalldata!,
            },
            _chainId,
            _permit2Address);
    }
}
