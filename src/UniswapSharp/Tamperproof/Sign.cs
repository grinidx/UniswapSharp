using System.Text;
using UniswapSharp.Tamperproof.Constants;
using UniswapSharp.Tamperproof.Utils;

namespace UniswapSharp.Tamperproof;

/// <summary>
/// Signing entry point. Port of <c>tamperproof-transactions/src/sign.ts</c>.
///
/// <para>Divergence: upstream is <c>async</c> because it uses WebCrypto's Promise-based subtle API;
/// .NET's <c>System.Security.Cryptography</c> is synchronous, so this is a synchronous method.</para>
/// </summary>
public static class Signer
{
    /// <summary>
    /// Signs <paramref name="data"/> (a raw string, or an object serialized as canonical JSON) with a
    /// PKCS#8 private key (hex) under the given JWS algorithm, returning a <c>0x</c>-prefixed hex
    /// signature.
    /// </summary>
    /// <param name="data">A <see cref="string"/> (UTF-8 encoded), or an object canonicalized via
    /// <see cref="CanonicalJson.SerializeRequestPayload"/>.</param>
    /// <param name="privateKey">PKCS#8 private key as a hex string (with or without <c>0x</c>).</param>
    /// <param name="algorithm">One of the keys in <see cref="Algorithms.Config"/> (e.g. <c>RS256</c>).</param>
    public static string Sign(object data, string privateKey, string algorithm)
    {
        if (algorithm is null || !Algorithms.Config.TryGetValue(algorithm, out SigningAlgorithmConfig? config))
        {
            throw new TamperproofException(Errors.AlgorithmNotSupported(algorithm));
        }

        byte[] bufferData = data is string s
            ? Encoding.UTF8.GetBytes(s)
            : CanonicalJson.SerializeRequestPayload(data);

        byte[] pkcs8 = Hex.FromHex(privateKey);
        byte[] signature = SigningCrypto.Sign(config, pkcs8, bufferData);
        return "0x" + Hex.ToHex(signature);
    }
}
