using System.Net.Http.Json;

namespace UniswapSharp.Tamperproof;

/// <summary>
/// Fetches the HTTPS public-key manifest, injected into <see cref="Verifier.VerifyAsyncJson"/>.
/// Mirrors the upstream global <c>fetch(url, options)</c> that <c>verify.test.ts</c> overrides.
/// </summary>
public interface IManifestFetcher
{
    /// <summary>Fetches <paramref name="url"/> with the given options.</summary>
    Task<ManifestResponse> FetchAsync(Uri url, ManifestFetchOptions options);
}

/// <summary>
/// The subset of <c>fetch</c> request options used by verification. Mirrors the
/// <c>{ redirect: "error", signal, headers: { Accept: "application/json" } }</c> upstream passes.
/// </summary>
public sealed class ManifestFetchOptions
{
    /// <summary>Redirect handling; always <c>"error"</c> (redirects are rejected).</summary>
    public string Redirect { get; init; } = "error";

    /// <summary>Request headers (always <c>{ Accept: "application/json" }</c>).</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } =
        new Dictionary<string, string>();

    /// <summary>Cancellation token carrying the request timeout (the <c>AbortController.signal</c>).</summary>
    public CancellationToken CancellationToken { get; init; }
}

/// <summary>
/// The subset of a <c>fetch</c> <c>Response</c> used by verification.
/// </summary>
public sealed class ManifestResponse
{
    /// <summary>Whether the status is in the 2xx range (<c>response.ok</c>).</summary>
    public bool Ok { get; init; }

    /// <summary>The HTTP status code.</summary>
    public int Status { get; init; }

    /// <summary>Header lookup by (case-insensitive) name, mirroring <c>response.headers.get(name)</c>.</summary>
    public Func<string, string?> GetHeader { get; init; } = _ => null;

    /// <summary>Reads and parses the JSON body (<c>response.json()</c>).</summary>
    public Func<Task<PublicKeyManifest>> ReadJsonAsync { get; init; } =
        () => Task.FromResult(new PublicKeyManifest());
}

/// <summary>The manifest document: a list of published public keys. Mirrors the fetched JSON shape.</summary>
public sealed class PublicKeyManifest
{
    /// <summary>The published public keys.</summary>
    public List<PublicKeyEntry> PublicKeys { get; init; } = new();
}

/// <summary>One published public key entry (<c>{ id, alg, publicKey }</c>).</summary>
public sealed class PublicKeyEntry
{
    /// <summary>The key id (EIP-7754 1-indexed string).</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>The JWS algorithm (a key in <see cref="Algorithms.Config"/>).</summary>
    public string Alg { get; init; } = string.Empty;

    /// <summary>The SPKI public key as hex (with or without <c>0x</c>).</summary>
    public string PublicKey { get; init; } = string.Empty;
}

/// <summary>
/// Default <see cref="IManifestFetcher"/> backed by <see cref="HttpClient"/>: rejects redirects,
/// sends <c>Accept: application/json</c>, and honours the request-timeout cancellation token. Not
/// exercised by the test suite (tests inject fakes); provided as a production convenience.
/// </summary>
public sealed class HttpManifestFetcher : IManifestFetcher
{
    /// <summary>A process-wide shared instance (redirects disabled).</summary>
    public static readonly HttpManifestFetcher Shared = new();

    private static readonly HttpClient Client = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
    })
    {
        Timeout = Timeout.InfiniteTimeSpan, // the per-request CancellationToken governs the timeout
    };

    /// <inheritdoc />
    public async Task<ManifestResponse> FetchAsync(Uri url, ManifestFetchOptions options)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        foreach (var header in options.Headers)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        HttpResponseMessage response = await Client
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, options.CancellationToken)
            .ConfigureAwait(false);

        return new ManifestResponse
        {
            Ok = response.IsSuccessStatusCode,
            Status = (int)response.StatusCode,
            GetHeader = name =>
                response.Content.Headers.TryGetValues(name, out var contentValues)
                    ? string.Join(",", contentValues)
                    : response.Headers.TryGetValues(name, out var values)
                        ? string.Join(",", values)
                        : null,
            ReadJsonAsync = async () =>
                await response.Content
                    .ReadFromJsonAsync<PublicKeyManifest>(options.CancellationToken)
                    .ConfigureAwait(false) ?? new PublicKeyManifest(),
        };
    }
}
