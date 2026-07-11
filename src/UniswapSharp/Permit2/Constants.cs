using System.Numerics;

namespace UniswapSharp.Permit2;

/// <summary>Port of permit2-sdk <c>constants.ts</c>: the canonical Permit2 address(es) and the signature/allowance range limits.</summary>
public static class Constants
{
    /// <summary>The canonical Permit2 deployment address (same on every chain except zkSync Era).</summary>
    // @deprecated please use Permit2AddressFor(chainId)
    public const string Permit2Address = "0x000000000022D473030F116dDEE9F6B43aC78BA3";

    /// <summary>Returns the Permit2 address for the given chain (zkSync Era = 324 differs; all others share the canonical address).</summary>
    public static string Permit2AddressFor(int? chainId) => chainId switch
    {
        324 => "0x0000000000225e31D15943971F47aD3022F714Fa",
        _ => Permit2Address,
    };

    public static readonly BigInteger MaxUint48 = (BigInteger.One << 48) - 1;
    public static readonly BigInteger MaxUint160 = (BigInteger.One << 160) - 1;
    public static readonly BigInteger MaxUint256 = (BigInteger.One << 256) - 1;

    // Aliases for the allowance-transfer types.
    public static readonly BigInteger MaxAllowanceTransferAmount = MaxUint160;
    public static readonly BigInteger MaxAllowanceExpiration = MaxUint48;
    public static readonly BigInteger MaxOrderedNonce = MaxUint48;

    // Aliases for the signature-transfer types.
    public static readonly BigInteger MaxSignatureTransferAmount = MaxUint256;
    public static readonly BigInteger MaxUnorderedNonce = MaxUint256;
    public static readonly BigInteger MaxSigDeadline = MaxUint256;

    public static readonly BigInteger InstantExpiration = BigInteger.Zero;
}
