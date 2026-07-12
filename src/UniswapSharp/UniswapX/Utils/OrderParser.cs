using System.Globalization;
using System.Numerics;
using UniswapSharp.Core.Utils;
using UniswapSharp.UniswapX.Order;
using UniswapSharp.UniswapX.Order.V4;
using UniswapSharp.V4.Utils;

namespace UniswapSharp.UniswapX.Utils;

/// <summary>Base of the order parsers (uniswapx-sdk <c>OrderParser</c>).</summary>
public abstract class OrderParser
{
    private const int SlotLength = 64;
    private const int AddressLength = 40;

    protected abstract int OrderInfoOffset { get; }

    /// <summary>Parses a serialized order to its concrete order object.</summary>
    public abstract IOffChainOrder ParseOrder(string order, int chainId);

    /// <summary>Determines the order type by looking up the reactor address in a serialized order.</summary>
    protected OrderType ParseOrderTypeFromEncoded(string order)
    {
        string stripped = OrderUtils.StripHexPrefix(order);
        int orderInfoOffsetBytes = (int)ParseHex(stripped.Substring(OrderInfoOffset, SlotLength));
        int reactorAddressOffset = orderInfoOffsetBytes * 2 + SlotLength;
        string reactorAddressSlot = stripped.Substring(reactorAddressOffset, SlotLength);
        string reactorAddress = AddressValidator
            .GetAddress(reactorAddressSlot.Substring(SlotLength - AddressLength))
            .ToLowerInvariant();

        if (!Constants.ReverseReactorMapping.TryGetValue(reactorAddress, out var mapping))
        {
            throw new MissingConfiguration("reactor", reactorAddress);
        }
        return mapping.OrderType;
    }

    /// <summary>Determines the order type from an order object (uniswapx-sdk <c>getOrderType</c>).</summary>
    public virtual OrderType GetOrderType(IOffChainOrder order)
    {
        string reactor = ReactorOf(order);
        return Constants.ReverseReactorMapping[reactor.ToLowerInvariant()].OrderType;
    }

    /// <summary>Determines the order type from a serialized order (uniswapx-sdk <c>getOrderTypeFromEncoded</c>).</summary>
    public OrderType GetOrderTypeFromEncoded(string order, int chainId) => GetOrderType(ParseOrder(order, chainId));

    internal static string ReactorOf(IOffChainOrder order) => order switch
    {
        DutchOrder d => d.Info.Reactor,
        UnsignedV2DutchOrder v2 => v2.Info.Reactor,
        UnsignedV3DutchOrder v3 => v3.Info.Reactor,
        UnsignedPriorityOrder p => p.Info.Reactor,
        RelayOrder r => r.Info.Reactor,
        UnsignedHybridOrder h => h.Info.Reactor,
        _ => throw new ArgumentException($"Unknown order type: {order.GetType().Name}"),
    };

    private static BigInteger ParseHex(string hex) =>
        BigInteger.Parse("0" + hex, NumberStyles.HexNumber);
}

/// <summary>Port of uniswapx-sdk <c>UniswapXOrderParser</c>.</summary>
public sealed class UniswapXOrderParser : OrderParser
{
    protected override int OrderInfoOffset => 64;

    public override IOffChainOrder ParseOrder(string order, int chainId)
    {
        // First try resolver-based detection for V4 orders.
        OrderType? v4OrderType = DetectV4OrderType(order);
        if (v4OrderType is OrderType v4)
        {
            return ParseV4Order(order, chainId, v4);
        }

        OrderType orderType = ParseOrderTypeFromEncoded(order);
        switch (orderType)
        {
            case OrderType.Dutch:
                return DutchOrder.Parse(order, chainId);
            case OrderType.Dutch_V2:
            {
                var cosigned = CosignedV2DutchOrder.Parse(order, chainId);
                return cosigned.Info.Cosignature == "0x" ? UnsignedV2DutchOrder.Parse(order, chainId) : cosigned;
            }
            case OrderType.Dutch_V3:
            {
                var cosigned = CosignedV3DutchOrder.Parse(order, chainId);
                return cosigned.Info.Cosignature == "0x" ? UnsignedV3DutchOrder.Parse(order, chainId) : cosigned;
            }
            case OrderType.Priority:
            {
                var cosigned = CosignedPriorityOrder.Parse(order, chainId);
                return cosigned.Info.Cosignature == "0x" ? UnsignedPriorityOrder.Parse(order, chainId) : cosigned;
            }
            default:
                throw new MissingConfiguration("orderType", orderType.ToString());
        }
    }

    private static OrderType? DetectV4OrderType(string order)
    {
        try
        {
            var decoded = AbiParamDecoder.Decode(new[] { "address", "bytes" }, order);
            string resolverLower = AddressValidator.GetAddress((string)decoded[0]!).ToLowerInvariant();
            if (Constants.ReverseResolverMapping.TryGetValue(resolverLower, out var mapping))
            {
                return mapping.OrderType;
            }
        }
        catch
        {
            // Not a V4 order format.
        }
        return null;
    }

    private static IOffChainOrder ParseV4Order(string order, int chainId, OrderType orderType)
    {
        switch (orderType)
        {
            case OrderType.Hybrid:
            {
                var cosigned = CosignedHybridOrder.Parse(order, chainId);
                return cosigned.Info.Cosignature == "0x" ? UnsignedHybridOrder.Parse(order, chainId) : cosigned;
            }
            default:
                throw new MissingConfiguration("v4OrderType", orderType.ToString());
        }
    }

    public override OrderType GetOrderType(IOffChainOrder order)
    {
        // V4 orders: check by instance type.
        if (order is UnsignedHybridOrder)
        {
            return OrderType.Hybrid;
        }

        string reactor = ReactorOf(order);
        OrderType orderType = Constants.ReverseReactorMapping[reactor.ToLowerInvariant()].OrderType;

        if (orderType == OrderType.Dutch && order is DutchOrder dutch)
        {
            bool isLimit =
                dutch.Info.Input.StartAmount == dutch.Info.Input.EndAmount &&
                dutch.Info.Outputs.All(o => o.StartAmount == o.EndAmount);
            return isLimit ? OrderType.Limit : OrderType.Dutch;
        }

        return orderType;
    }
}

/// <summary>Port of uniswapx-sdk <c>RelayOrderParser</c>.</summary>
public sealed class RelayOrderParser : OrderParser
{
    protected override int OrderInfoOffset => 64;

    public override IOffChainOrder ParseOrder(string order, int chainId) => RelayOrder.Parse(order, chainId);
}
