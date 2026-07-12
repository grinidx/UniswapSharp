using System.Globalization;
using UniswapSharp.Tamperproof.Constants;
using UniswapSharp.Tamperproof.Utils;

namespace UniswapSharp.Tamperproof;

/// <summary>
/// A public key + its JWS algorithm, as accepted by <see cref="Generator.Generate"/>. Port of the
/// <c>PublicKey</c> type in <c>tamperproof-transactions/src/generate.ts</c>.
/// </summary>
/// <param name="Key">Public key as a hex string (with or without <c>0x</c>).</param>
/// <param name="Algorithm">One of the keys in <see cref="Algorithms.Config"/> (e.g. <c>RS256</c>).</param>
public sealed record PublicKey(string Key, string Algorithm);

/// <summary>
/// Manifest generation. Port of <c>tamperproof-transactions/src/generate.ts</c>.
/// </summary>
public static class Generator
{
    /// <summary>
    /// Builds the TWIST public-key manifest JSON for the given keys, assigning EIP-7754 1-indexed
    /// string ids in order and normalizing each key to <c>0x</c>-prefixed lowercase hex.
    /// </summary>
    public static string Generate(params PublicKey[] publicKeys)
    {
        var pubKeys = new List<object>();
        for (int index = 0; index < publicKeys.Length; index++)
        {
            PublicKey publicKey = publicKeys[index];
            if (!Algorithms.Config.ContainsKey(publicKey.Algorithm))
            {
                throw new TamperproofException(Errors.AlgorithmNotSupported(publicKey.Algorithm));
            }

            pubKeys.Add(new List<KeyValuePair<string, object?>>
            {
                // EIP states 1-indexed string.
                new("id", (index + 1).ToString(CultureInfo.InvariantCulture)),
                new("alg", publicKey.Algorithm),
                new("publicKey", Hex.NormalizeHex(publicKey.Key)),
            });
        }

        return CanonicalJson.Stringify(new List<KeyValuePair<string, object?>>
        {
            new("publicKeys", pubKeys),
        });
    }
}
