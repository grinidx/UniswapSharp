namespace UniswapSharp.Tamperproof.Constants;

/// <summary>
/// Error thrown by the tamperproof-transactions port. Mirrors the upstream <c>new Error(...)</c>
/// throws; the message strings come from <see cref="Errors"/>.
/// </summary>
public sealed class TamperproofException : Exception
{
    public TamperproofException(string message) : base(message) { }
}

/// <summary>
/// Error message builders for the tamperproof-transactions package. Port of
/// <c>tamperproof-transactions/src/constants/errors.ts</c>. Every string matches upstream exactly;
/// the tests assert on them.
/// </summary>
public static class Errors
{
    public static string AlgorithmNotSupported(object? alg) =>
        $"Algorithm is not supported: {alg}";

    public static string NoTxtRecordsForHost(string host) =>
        $"No TXT records found for host {host}";

    public static string NoTxtWithPrefixForHost(string prefix, string host) =>
        $"No TXT record found with prefix {prefix} for host {host}";

    public static string MultipleTxtWithPrefixForHost(string prefix, string host) =>
        $"Multiple TXT records found with prefix {prefix} for host {host}. Only one is allowed.";

    public const string TwistPathTooLong = "TWIST path too long";

    public const string ManifestHttpsOnly = "Manifest must be fetched over HTTPS";

    public static string ManifestFetchFailed(int status) =>
        $"Failed to fetch manifest: HTTP {status}";

    public const string ManifestContentType =
        "Manifest Content-Type must be application/json";

    public const string ManifestTooLarge = "Manifest too large";

    public static string PublicKeyIdNotFound(object id) =>
        $"Public key with id {id} not found";

    public static string MultiplePublicKeysWithId(object id) =>
        $"Multiple public keys found with id {id}. Key IDs must be unique.";

    public const string InvalidTxtRecordFormat =
        "Invalid TXT record format: length exceeds buffer size";

    public const string InvalidHexLengthEven =
        "Invalid hex string: length must be even";

    public static string InvalidHexString(string hex) =>
        $"Invalid hex string: {hex}";

    public const string NoBase64Decoder =
        "No base64 decoder available in this environment";
}
