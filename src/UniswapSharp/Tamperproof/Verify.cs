using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UniswapSharp.Tamperproof.Constants;
using UniswapSharp.Tamperproof.Utils;

namespace UniswapSharp.Tamperproof;

/// <summary>
/// Signature verification. Port of <c>tamperproof-transactions/src/verify.ts</c>.
///
/// <para>Divergences: the DNS-over-HTTPS resolver and the HTTPS manifest fetch are behind the
/// injectable <see cref="IDohResolver"/> / <see cref="IManifestFetcher"/> interfaces (upstream uses a
/// global <c>fetch</c> and a <c>dohjs</c> resolver, already parameterized via <c>thisResolver</c>).
/// <see cref="Verify"/> is synchronous (WebCrypto's async subtle API has no analog in
/// <c>System.Security.Cryptography</c>) and takes the SPKI public key bytes directly.</para>
/// </summary>
public static partial class Verifier
{
    /// <summary>The TWIST TXT-record prefix.</summary>
    public const string Prefix = "TWIST=";

    private const int TimeoutMs = 1000;
    private const int MaxManifestBytes = 64 * 1024; // 64KB
    private const int MaxTwistPath = 1024;

    /// <summary>
    /// Resolves the TWIST TXT record for <paramref name="host"/> over DNS-over-HTTPS, derives the
    /// manifest URL, and verifies <paramref name="calldata"/> against the published key
    /// <paramref name="id"/>.
    /// </summary>
    public static async Task<bool> VerifyAsyncDns(
        string calldata,
        string signature,
        string host,
        string id,
        IDohResolver resolver,
        IManifestFetcher? fetcher = null)
    {
        // Use DNS over HTTPS to resolve TXT records.
        DohResponse response = await resolver.QueryAsync(
            host,
            "TXT",
            "GET",
            new Dictionary<string, string> { ["Accept"] = "application/dns-message" },
            TimeoutMs).ConfigureAwait(false);

        if (response.Answers is null || response.Answers.Count == 0)
        {
            throw new TamperproofException(Errors.NoTxtRecordsForHost(host));
        }

        string? twistRecord = null;

        // Scan all TXT records; capture the first with PREFIX and throw if another is found.
        foreach (DohAnswer answer in response.Answers)
        {
            string recordData = TxtRecord.ProcessTxtRecordData(answer.Data);

            if (recordData.StartsWith(Prefix, StringComparison.Ordinal))
            {
                if (!string.IsNullOrEmpty(twistRecord))
                {
                    throw new TamperproofException(Errors.MultipleTxtWithPrefixForHost(Prefix, host));
                }

                twistRecord = recordData[Prefix.Length..];
            }
        }

        if (string.IsNullOrEmpty(twistRecord))
        {
            throw new TamperproofException(Errors.NoTxtWithPrefixForHost(Prefix, host));
        }

        // Normalize and bound TWIST path; encode path segments.
        twistRecord = twistRecord.TrimStart('/');
        if (twistRecord.Length > MaxTwistPath)
        {
            throw new TamperproofException(Errors.TwistPathTooLong);
        }

        string encodedPath = string.Join(
            "/",
            twistRecord.Split('/').Select(EncodeUriComponent));

        var url = new Uri($"https://{host}/{encodedPath}");

        return await VerifyAsyncJson(calldata, signature, url, id, fetcher).ConfigureAwait(false);
    }

    /// <summary>
    /// Fetches the HTTPS manifest at <paramref name="url"/>, selects the public key with the given
    /// <paramref name="id"/>, and verifies <paramref name="calldata"/> against it.
    /// </summary>
    public static async Task<bool> VerifyAsyncJson(
        string calldata,
        string signature,
        Uri url,
        string id,
        IManifestFetcher? fetcher = null)
    {
        if (url.Scheme != "https")
        {
            throw new TamperproofException(Errors.ManifestHttpsOnly);
        }

        fetcher ??= HttpManifestFetcher.Shared;

        using var cts = new CancellationTokenSource(TimeoutMs);
        var options = new ManifestFetchOptions
        {
            Redirect = "error",
            Headers = new Dictionary<string, string> { ["Accept"] = "application/json" },
            CancellationToken = cts.Token,
        };

        ManifestResponse response = await fetcher.FetchAsync(url, options).ConfigureAwait(false);

        if (!response.Ok)
        {
            throw new TamperproofException(Errors.ManifestFetchFailed(response.Status));
        }

        string ct = response.GetHeader("content-type") ?? string.Empty;
        if (!ContentTypeRegex().IsMatch(ct))
        {
            throw new TamperproofException(Errors.ManifestContentType);
        }

        string? cl = response.GetHeader("content-length");
        if (!string.IsNullOrEmpty(cl)
            && long.TryParse(cl, NumberStyles.Integer, CultureInfo.InvariantCulture, out long length)
            && length > MaxManifestBytes)
        {
            throw new TamperproofException(Errors.ManifestTooLarge);
        }

        PublicKeyManifest data = await response.ReadJsonAsync().ConfigureAwait(false);
        var matchingKeys = data.PublicKeys.Where(pk => pk.Id == id).ToList();

        if (matchingKeys.Count == 0)
        {
            throw new TamperproofException(Errors.PublicKeyIdNotFound(id));
        }

        if (matchingKeys.Count > 1)
        {
            throw new TamperproofException(Errors.MultiplePublicKeysWithId(id));
        }

        PublicKeyEntry publicKey = matchingKeys[0];

        if (!Algorithms.IsSupported(publicKey.Alg))
        {
            throw new TamperproofException(Errors.AlgorithmNotSupported(publicKey.Alg));
        }

        byte[] spki = Hex.FromHex(publicKey.PublicKey);
        return Verify(calldata, signature, spki, publicKey.Alg);
    }

    /// <summary>
    /// Verifies <paramref name="signature"/> (hex) over <paramref name="calldata"/> using an SPKI
    /// public key under the given JWS algorithm. For ECDSA, only raw <c>r||s</c> signatures of the
    /// expected length are accepted (others return <c>false</c>).
    /// </summary>
    public static bool Verify(string calldata, string signature, byte[] publicKeySpki, string alg)
    {
        byte[] bufferData = Encoding.UTF8.GetBytes(calldata);
        byte[] signatureBytes = Hex.FromHex(signature);

        if (!Algorithms.Config.TryGetValue(alg, out SigningAlgorithmConfig? config))
        {
            throw new TamperproofException(Errors.AlgorithmNotSupported(alg));
        }

        if (config.Family == SigningFamily.Ecdsa)
        {
            // Only accept raw r||s for ECDSA signatures.
            int coordLen = config.EcdsaCoordinateLength!.Value;
            if (signatureBytes.Length != coordLen * 2)
            {
                return false;
            }
        }

        return SigningCrypto.Verify(config, publicKeySpki, signatureBytes, bufferData);
    }

    /// <summary>
    /// encodeURIComponent-compatible percent-encoding: <see cref="Uri.EscapeDataString(string)"/> additionally
    /// escapes <c>!'()*</c>, which encodeURIComponent leaves unescaped, so those are restored.
    /// </summary>
    private static string EncodeUriComponent(string segment) => Uri.EscapeDataString(segment)
        .Replace("%21", "!")
        .Replace("%27", "'")
        .Replace("%28", "(")
        .Replace("%29", ")")
        .Replace("%2A", "*");

    [GeneratedRegex("^application/json(?:;|$)", RegexOptions.IgnoreCase)]
    private static partial Regex ContentTypeRegex();
}
