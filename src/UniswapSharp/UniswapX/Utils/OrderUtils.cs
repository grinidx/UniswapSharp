using System.Numerics;

namespace UniswapSharp.UniswapX.Utils;

/// <summary>Free helper functions from uniswapx-sdk <c>utils/index.ts</c> and <c>utils/order.ts</c>.</summary>
public static class OrderUtils
{
    /// <summary>Strips a leading <c>0x</c> prefix, if present.</summary>
    public static string StripHexPrefix(string a) => a.StartsWith("0x") ? a[2..] : a;

    /// <summary>Resolves the Permit2 address for a chain, preferring an explicit override.</summary>
    public static string GetPermit2(int chainId, string? permit2Address = null)
    {
        if (permit2Address != null)
        {
            return permit2Address;
        }
        if (Constants.Permit2Mapping.TryGetValue(chainId, out var mapped))
        {
            return mapped;
        }
        throw new MissingConfiguration("permit2", chainId.ToString());
    }

    /// <summary>Resolves the reactor address for a chain/order type, preferring an explicit override.</summary>
    public static string GetReactor(int chainId, OrderType orderType, string? reactorAddress = null)
    {
        string? mappedReactorAddress = null;
        if (Constants.ReactorAddressMapping.TryGetValue(chainId, out var reactors) &&
            reactors.TryGetValue(orderType, out var addr))
        {
            mappedReactorAddress = addr;
        }

        if (reactorAddress != null)
        {
            return reactorAddress;
        }
        if (mappedReactorAddress != null)
        {
            return mappedReactorAddress;
        }
        throw new MissingConfiguration("reactor", chainId.ToString());
    }

    /// <summary>Returns <paramref name="original"/> when <paramref name="value"/> is zero (uniswapx-sdk <c>originalIfZero</c>).</summary>
    public static BigInteger OriginalIfZero(BigInteger value, BigInteger original) =>
        value.IsZero ? original : value;

    /// <summary>
    /// Equivalent of <c>hexStripZeros(value.toHexString())</c>: the minimal lower-case <c>0x</c> hex of a
    /// non-negative integer with all leading zero nibbles removed (<c>0</c> renders as <c>0x0</c>).
    /// </summary>
    public static string HexStripZeros(BigInteger value)
    {
        if (value.Sign < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "value must be non-negative");
        }
        if (value.IsZero)
        {
            return "0x0";
        }
        string hex = value.ToString("x").TrimStart('0');
        return "0x" + (hex.Length == 0 ? "0" : hex);
    }
}
