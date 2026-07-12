using System.Security.Cryptography;
using UniswapSharp.Permit2;

namespace UniswapSharp.UniversalRouter.Utils;

/// <summary>Port of universal-router-sdk <c>utils/eip712.ts</c> — signed-routes typed data.</summary>
public static class Eip712
{
    public const string EIP712_DOMAIN_NAME = "UniversalRouter";
    public const string EIP712_DOMAIN_VERSION = "2";

    /// <summary>The <c>ExecuteSigned</c> EIP-712 struct type. Port of upstream <c>EXECUTE_SIGNED_TYPES</c>.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<TypedDataField>> EXECUTE_SIGNED_TYPES =
        new Dictionary<string, IReadOnlyList<TypedDataField>>
        {
            ["ExecuteSigned"] = new List<TypedDataField>
            {
                new("commands", "bytes"),
                new("inputs", "bytes[]"),
                new("intent", "bytes32"),
                new("data", "bytes32"),
                new("sender", "address"),
                new("nonce", "bytes32"),
                new("deadline", "uint256"),
            },
        };

    /// <summary>Generate the EIP-712 domain for the Universal Router.</summary>
    public static Eip712Domain GetUniversalRouterDomain(int chainId, string verifyingContract) => new()
    {
        Name = EIP712_DOMAIN_NAME,
        Version = EIP712_DOMAIN_VERSION,
        ChainId = chainId,
        VerifyingContract = verifyingContract,
    };

    /// <summary>Generate a random 32-byte nonce for signed execution.</summary>
    public static string GenerateNonce()
    {
        byte[] randomBytes = RandomNumberGenerator.GetBytes(32);
        return "0x" + Convert.ToHexStringLower(randomBytes);
    }

    /// <summary>Sentinel value to skip nonce checking (allows signature replay). bytes32(type(uint256).max).</summary>
    public const string NONCE_SKIP_CHECK =
        "0xffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff";
}
