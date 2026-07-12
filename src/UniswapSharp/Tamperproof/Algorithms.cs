using System.Security.Cryptography;

namespace UniswapSharp.Tamperproof;

/// <summary>
/// The JWS signing family a configured algorithm maps onto.
/// </summary>
public enum SigningFamily
{
    /// <summary>RSASSA-PKCS1-v1_5.</summary>
    Rsa,

    /// <summary>RSA-PSS (salt length = hash length).</summary>
    RsaPss,

    /// <summary>ECDSA over a NIST P-curve; raw <c>r||s</c> signatures.</summary>
    Ecdsa,

    /// <summary>Ed25519 (EdDSA).</summary>
    Ed25519,
}

/// <summary>
/// The parameters for one signing algorithm. Port of the <c>SigningAlgorithmConfig</c> shape in
/// <c>tamperproof-transactions/src/algorithms.ts</c>, mapped onto <c>System.Security.Cryptography</c>.
/// </summary>
public sealed record SigningAlgorithmConfig(
    string Name,
    SigningFamily Family,
    HashAlgorithmName? Hash,
    int? EcdsaCoordinateLength);

/// <summary>
/// The JWS algorithm registry. Port of <c>tamperproof-transactions/src/algorithms.ts</c>
/// (<c>SIGNING_ALGORITHM_CONFIG</c> / <c>SIGNING_ALGORITHM_IMPORT_PARAMS</c>, unified here). Each key
/// maps to the crypto primitive, hash, and (for ECDSA) fixed coordinate length used by sign/verify.
/// </summary>
public static class Algorithms
{
    /// <summary>Upstream WebCrypto <c>name</c> for ECDSA.</summary>
    public const string EcdsaName = "ECDSA";

    /// <summary>Upstream WebCrypto <c>name</c> for RSA-PSS.</summary>
    public const string RsaPssName = "RSA-PSS";

    /// <summary>Upstream WebCrypto <c>name</c> for RSASSA-PKCS1-v1_5.</summary>
    public const string RsaPkcs1Name = "RSASSA-PKCS1-v1_5";

    /// <summary>Upstream WebCrypto <c>name</c> for Ed25519.</summary>
    public const string Ed25519Name = "Ed25519";

    /// <summary>
    /// Configuration for every supported algorithm, keyed by its JWS <c>alg</c> string.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, SigningAlgorithmConfig> Config =
        new Dictionary<string, SigningAlgorithmConfig>(StringComparer.Ordinal)
        {
            ["ES256"] = new(EcdsaName, SigningFamily.Ecdsa, HashAlgorithmName.SHA256, 32),
            ["ES384"] = new(EcdsaName, SigningFamily.Ecdsa, HashAlgorithmName.SHA384, 48),
            ["ES512"] = new(EcdsaName, SigningFamily.Ecdsa, HashAlgorithmName.SHA512, 66),
            ["EdDSA"] = new(Ed25519Name, SigningFamily.Ed25519, null, null),
            ["PS256"] = new(RsaPssName, SigningFamily.RsaPss, HashAlgorithmName.SHA256, null),
            ["PS384"] = new(RsaPssName, SigningFamily.RsaPss, HashAlgorithmName.SHA384, null),
            ["PS512"] = new(RsaPssName, SigningFamily.RsaPss, HashAlgorithmName.SHA512, null),
            ["RS256"] = new(RsaPkcs1Name, SigningFamily.Rsa, HashAlgorithmName.SHA256, null),
            ["RS384"] = new(RsaPkcs1Name, SigningFamily.Rsa, HashAlgorithmName.SHA384, null),
            ["RS512"] = new(RsaPkcs1Name, SigningFamily.Rsa, HashAlgorithmName.SHA512, null),
        };

    /// <summary>Whether <paramref name="alg"/> is a supported algorithm key.</summary>
    public static bool IsSupported(string? alg) => alg is not null && Config.ContainsKey(alg);
}
