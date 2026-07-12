namespace UniswapSharp.Tamperproof;

/// <summary>
/// A DNS-over-HTTPS resolver, injected into <see cref="Verifier.VerifyAsyncDns"/>. Mirrors the
/// upstream <c>dohjs</c> <c>DohResolver.query(...)</c> surface (which upstream already parameterizes
/// via the <c>thisResolver</c> argument).
///
/// <para>Divergence: no live DoH resolver is bundled — resolving TXT records over the network is
/// deferred. Production callers inject an implementation; tests inject a fake.</para>
/// </summary>
public interface IDohResolver
{
    /// <summary>
    /// Queries DNS records. Mirrors <c>query(qname, qtype, method, headers, timeout)</c>.
    /// </summary>
    Task<DohResponse> QueryAsync(
        string qname,
        string qtype,
        string method,
        IReadOnlyDictionary<string, string> headers,
        int timeoutMs);
}

/// <summary>A DoH query response. Mirrors the <c>dohjs</c> <c>DnsResponse</c> (answers only).</summary>
public sealed class DohResponse
{
    /// <summary>The answer records, or <c>null</c>/empty when none were returned.</summary>
    public IReadOnlyList<DohAnswer>? Answers { get; init; }
}

/// <summary>A single DoH answer record. Mirrors the <c>dohjs</c> <c>DnsAnswer</c> (data only).</summary>
public sealed class DohAnswer
{
    /// <summary>
    /// The record payload: either a <see cref="string"/> or RFC-1035 length-prefixed wire-format
    /// <c>byte[]</c> (decoded by <see cref="Utils.TxtRecord.ProcessTxtRecordData"/>).
    /// </summary>
    public object? Data { get; init; }
}
