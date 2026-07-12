using System.Numerics;
using System.Text;
using Nethereum.Util;

namespace UniswapSharp.UniswapX;

/// <summary>Port of uniswapx-sdk <c>constants/v4.ts</c>: V4-specific EIP-712 type hashes and constants.</summary>
public static class ConstantsV4
{
    internal static string Keccak256Utf8(string text) =>
        "0x" + Convert.ToHexStringLower(Sha3Keccack.Current.CalculateHash(Encoding.UTF8.GetBytes(text)));

    /// <summary>EIP-712 Domain type hash for Permit2.</summary>
    public static readonly string Permit2DomainTypeHash =
        Keccak256Utf8("EIP712Domain(string name,uint256 chainId,address verifyingContract)");

    /// <summary>EIP-712 Domain type hash for DCAHook.</summary>
    public static readonly string DcaHookDomainTypeHash =
        Keccak256Utf8("EIP712Domain(string name,string version,uint256 chainId,address verifyingContract)");

    /// <summary>Permit2 Witness Transfer From type hash.</summary>
    public static readonly string PermitWitnessTransferFromTypeHash =
        Keccak256Utf8(
            "PermitWitnessTransferFrom(" +
            "TokenPermissions permitted," +
            "address spender," +
            "uint256 nonce," +
            "uint256 deadline," +
            "GenericOrder witness)" +
            "GenericOrder(address resolver,bytes32 orderHash)" +
            "TokenPermissions(address token,uint256 amount)");

    /// <summary>TokenPermissions type hash.</summary>
    public static readonly string TokenPermissionsTypeHash =
        Keccak256Utf8("TokenPermissions(address token,uint256 amount)");

    /// <summary>DCAHook domain name.</summary>
    public const string DcaHookDomainName = "DCAHook";

    /// <summary>DCAHook domain version.</summary>
    public const string DcaHookDomainVersion = "1";

    /// <summary>Permit2 domain name.</summary>
    public const string Permit2DomainName = "Permit2";

    /// <summary>Neutral scaling factor (1e18) for the hybrid auction (Tribunal's implementation).</summary>
    public static readonly BigInteger BaseScalingFactor = BigInteger.Pow(10, 18);
}
