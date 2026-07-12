using System.Security.Cryptography;
using System.Text;
using AwesomeAssertions;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using UniswapSharp.Tamperproof;
using UniswapSharp.Tamperproof.Constants;
using UniswapSharp.Tamperproof.Utils;

namespace UniswapSharp.Testing.Tamperproof;

// Ported from sdks/tamperproof-transactions/src/verify.test.ts. The DoH resolver and HTTPS manifest
// fetch (global `fetch` + `dohjs` upstream) are replaced with the injectable IDohResolver /
// IManifestFetcher fakes below, mirroring how verify.test.ts mocks them.
public class VerifyTests
{
    private const string Data = "data";
    private static byte[] DataBytes => Encoding.UTF8.GetBytes(Data);

    // --- fakes -------------------------------------------------------------------------------

    private sealed class FakeDohResolver : IDohResolver
    {
        private readonly Func<Task<DohResponse>> _onQuery;

        public FakeDohResolver(Func<Task<DohResponse>> onQuery) => _onQuery = onQuery;

        public FakeDohResolver(DohResponse response) => _onQuery = () => Task.FromResult(response);

        public bool Called { get; private set; }

        public Task<DohResponse> QueryAsync(
            string qname, string qtype, string method,
            IReadOnlyDictionary<string, string> headers, int timeoutMs)
        {
            Called = true;
            return _onQuery();
        }
    }

    private sealed class FakeManifestFetcher : IManifestFetcher
    {
        private readonly Func<Uri, ManifestFetchOptions, ManifestResponse> _onFetch;

        public FakeManifestFetcher(Func<Uri, ManifestFetchOptions, ManifestResponse> onFetch) =>
            _onFetch = onFetch;

        public FakeManifestFetcher(ManifestResponse response) => _onFetch = (_, _) => response;

        public bool Called { get; private set; }

        public Uri? LastUrl { get; private set; }

        public ManifestFetchOptions? LastOptions { get; private set; }

        public Task<ManifestResponse> FetchAsync(Uri url, ManifestFetchOptions options)
        {
            Called = true;
            LastUrl = url;
            LastOptions = options;
            return Task.FromResult(_onFetch(url, options));
        }
    }

    private static DohResponse Answers(params object[] datas) => new()
    {
        Answers = datas.Select(d => new DohAnswer { Data = d }).ToList(),
    };

    private static byte[] Wire(params string[] chunks)
    {
        var bytes = new List<byte>();
        foreach (string chunk in chunks)
        {
            byte[] data = Encoding.UTF8.GetBytes(chunk);
            bytes.Add((byte)data.Length);
            bytes.AddRange(data);
        }

        return bytes.ToArray();
    }

    private static Func<string, string?> Headers(string? contentType, string? contentLength = "0") =>
        name => name switch
        {
            "content-type" => contentType,
            "content-length" => contentLength,
            _ => null,
        };

    private static PublicKeyManifest Manifest(params PublicKeyEntry[] keys) =>
        new() { PublicKeys = keys.ToList() };

    private static ManifestResponse Ok200(
        PublicKeyManifest manifest, string? contentType = "application/json", string? contentLength = "0") =>
        new()
        {
            Ok = true,
            Status = 200,
            GetHeader = Headers(contentType, contentLength),
            ReadJsonAsync = () => Task.FromResult(manifest),
        };

    // A fetcher whose manifest fails at the crypto step (used by the DNS-parsing tests, which only
    // assert that parsing succeeded and the flow proceeded past the resolver).
    private static FakeManifestFetcher CryptoFailFetcher() => new(Ok200(Manifest(
        new PublicKeyEntry { Id = "1", Alg = "ES256", PublicKey = "0x123456789abcdef" })));

    // ========================================================================================
    // verifyAsyncDns — failure cases
    // ========================================================================================

    [Fact]
    public async Task VerifyAsyncDns_ThrowsIfDnsResolutionFails()
    {
        var resolver = new FakeDohResolver(() =>
            Task.FromException<DohResponse>(new Exception("DNS resolution failed")));

        Func<Task> act = () =>
            Verifier.VerifyAsyncDns("data", "signature", "example.com", "1", resolver, CryptoFailFetcher());
        await act.Should().ThrowAsync<Exception>().WithMessage("DNS resolution failed");
    }

    [Fact]
    public async Task VerifyAsyncDns_ThrowsIfNoTxtRecords()
    {
        var resolver = new FakeDohResolver(new DohResponse { Answers = new List<DohAnswer>() });

        Func<Task> act = () =>
            Verifier.VerifyAsyncDns("data", "signature", "example.com", "1", resolver, CryptoFailFetcher());
        await act.Should().ThrowAsync<TamperproofException>()
            .WithMessage("No TXT records found for host example.com");
    }

    [Fact]
    public async Task VerifyAsyncDns_ThrowsIfNoRecordWithPrefix()
    {
        var resolver = new FakeDohResolver(Answers("WRONG_PREFIX=somedata"));

        Func<Task> act = () =>
            Verifier.VerifyAsyncDns("data", "signature", "example.com", "1", resolver, CryptoFailFetcher());
        await act.Should().ThrowAsync<TamperproofException>()
            .WithMessage($"No TXT record found with prefix {Verifier.Prefix} for host example.com");
    }

    // ========================================================================================
    // verifyAsyncDns — TXT record parsing
    // ========================================================================================

    [Fact]
    public async Task VerifyAsyncDns_SingleSubstringString()
    {
        var resolver = new FakeDohResolver(Answers("TWIST=test-endpoint"));

        Func<Task> act = () =>
            Verifier.VerifyAsyncDns("data", "signature", "example.com", "1", resolver, CryptoFailFetcher());
        await act.Should().ThrowAsync<Exception>(); // fails at crypto step, but parsing succeeded
    }

    [Fact]
    public async Task VerifyAsyncDns_MultipleSubstringBuffer()
    {
        var resolver = new FakeDohResolver(Answers(Wire("TWIST=", "test-end", "point")));

        Func<Task> act = () =>
            Verifier.VerifyAsyncDns("data", "signature", "example.com", "1", resolver, CryptoFailFetcher());
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task VerifyAsyncDns_EmptySubstringBuffer()
    {
        // "TWIST=" + "" + "data"
        var resolver = new FakeDohResolver(Answers(Wire("TWIST=", "", "data")));

        Func<Task> act = () =>
            Verifier.VerifyAsyncDns("data", "signature", "example.com", "1", resolver, CryptoFailFetcher());
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task VerifyAsyncDns_MalformedBufferThrows()
    {
        var bytes = new List<byte>();
        bytes.AddRange(Wire("TWIST="));
        bytes.Add(10); // claims 10 bytes but only 4 ("test")
        bytes.AddRange(Encoding.UTF8.GetBytes("test"));

        var resolver = new FakeDohResolver(Answers(bytes.ToArray()));

        Func<Task> act = () =>
            Verifier.VerifyAsyncDns("data", "signature", "example.com", "1", resolver, CryptoFailFetcher());
        await act.Should().ThrowAsync<TamperproofException>()
            .WithMessage("Invalid TXT record format: length exceeds buffer size");
    }

    [Fact]
    public async Task VerifyAsyncDns_MixedRecordsSelectsFirstTwist()
    {
        var resolver = new FakeDohResolver(Answers(
            "OTHER_PREFIX=ignore-this",
            Wire("TWIST=", "valid"),
            "IGNORED=backup"));

        var fetcher = CryptoFailFetcher();
        Func<Task> act = () =>
            Verifier.VerifyAsyncDns("data", "signature", "example.com", "1", resolver, fetcher);
        await act.Should().ThrowAsync<Exception>();
        fetcher.LastUrl!.AbsoluteUri.Should().Be("https://example.com/valid");
    }

    [Fact]
    public async Task VerifyAsyncDns_ThrowsWhenMultipleTwistRecords()
    {
        var resolver = new FakeDohResolver(Answers("TWIST=one", "TWIST=two"));

        Func<Task> act = () =>
            Verifier.VerifyAsyncDns("data", "signature", "example.com", "1", resolver, CryptoFailFetcher());
        await act.Should().ThrowAsync<TamperproofException>().WithMessage(
            $"Multiple TXT records found with prefix {Verifier.Prefix} for host example.com. Only one is allowed.");
    }

    [Fact]
    public async Task VerifyAsyncDns_SanitizesLeadingSlashesAndEncodesSegments()
    {
        var resolver = new FakeDohResolver(Answers($"{Verifier.Prefix}//api v1/ƙeys?bad#frag"));
        var fetcher = new FakeManifestFetcher(Ok200(Manifest(
            new PublicKeyEntry { Id = "1", Alg = "RS256", PublicKey = "00" })));

        Func<Task> act = () =>
            Verifier.VerifyAsyncDns("data", "signature", "example.com", "1", resolver, fetcher);
        await act.Should().ThrowAsync<Exception>();

        fetcher.Called.Should().BeTrue();
        fetcher.LastUrl.Should().NotBeNull();
        fetcher.LastUrl!.AbsoluteUri.Should().Be("https://example.com/api%20v1/%C6%99eys%3Fbad%23frag");
    }

    [Fact]
    public async Task VerifyAsyncDns_RejectsWhenTwistPathTooLong()
    {
        string longPath = new string('a', 1025);
        var resolver = new FakeDohResolver(Answers($"{Verifier.Prefix}{longPath}"));
        var fetcher = new FakeManifestFetcher(Ok200(Manifest()));

        Func<Task> act = () =>
            Verifier.VerifyAsyncDns("data", "signature", "example.com", "1", resolver, fetcher);
        await act.Should().ThrowAsync<TamperproofException>().WithMessage("TWIST path too long");

        fetcher.Called.Should().BeFalse();
    }

    // ========================================================================================
    // verifyAsyncJson
    // ========================================================================================

    private static readonly RSA LocalRsa = RSA.Create(2048);

    private static string LocalRsaSpkiHex => Hex.ToHex(LocalRsa.ExportSubjectPublicKeyInfo());

    private static string SignLocalRs256(string data) =>
        Hex.ToHex(LocalRsa.SignData(Encoding.UTF8.GetBytes(data), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

    private static readonly Uri HttpsUrl = new("https://example.com/manifest.json");

    [Fact]
    public async Task VerifyAsyncJson_ThrowsIfNotHttps()
    {
        var httpUrl = new Uri("http://example.com/manifest.json");
        var fetcher = new FakeManifestFetcher(Ok200(Manifest()));

        Func<Task> act = () => Verifier.VerifyAsyncJson("data", "signature", httpUrl, "1", fetcher);
        await act.Should().ThrowAsync<TamperproofException>()
            .WithMessage("Manifest must be fetched over HTTPS");
    }

    [Fact]
    public async Task VerifyAsyncJson_ThrowsIfStatusNotOk()
    {
        var fetcher = new FakeManifestFetcher(new ManifestResponse
        {
            Ok = false,
            Status = 404,
            GetHeader = _ => null,
        });

        Func<Task> act = () => Verifier.VerifyAsyncJson("data", "signature", HttpsUrl, "1", fetcher);
        await act.Should().ThrowAsync<TamperproofException>()
            .WithMessage("Failed to fetch manifest: HTTP 404");
    }

    [Fact]
    public async Task VerifyAsyncJson_ThrowsIfContentTypeNotJson()
    {
        var fetcher = new FakeManifestFetcher(new ManifestResponse
        {
            Ok = true,
            Status = 200,
            GetHeader = _ => "text/plain",
        });

        Func<Task> act = () => Verifier.VerifyAsyncJson("data", "signature", HttpsUrl, "1", fetcher);
        await act.Should().ThrowAsync<TamperproofException>()
            .WithMessage("Manifest Content-Type must be application/json");
    }

    [Fact]
    public async Task VerifyAsyncJson_ThrowsIfKeyIdNotFound()
    {
        var fetcher = new FakeManifestFetcher(Ok200(Manifest()));

        Func<Task> act = () => Verifier.VerifyAsyncJson("data", "signature", HttpsUrl, "1", fetcher);
        await act.Should().ThrowAsync<TamperproofException>()
            .WithMessage("Public key with id 1 not found");
    }

    [Fact]
    public async Task VerifyAsyncJson_ThrowsIfDuplicateKeyIds()
    {
        var fetcher = new FakeManifestFetcher(Ok200(Manifest(
            new PublicKeyEntry { Id = "1", Alg = "RS256", PublicKey = "00" },
            new PublicKeyEntry { Id = "1", Alg = "RS256", PublicKey = "00" })));

        Func<Task> act = () => Verifier.VerifyAsyncJson("data", "signature", HttpsUrl, "1", fetcher);
        await act.Should().ThrowAsync<TamperproofException>()
            .WithMessage("Multiple public keys found with id 1. Key IDs must be unique.");
    }

    [Fact]
    public async Task VerifyAsyncJson_ThrowsIfAlgorithmUnsupported()
    {
        var fetcher = new FakeManifestFetcher(Ok200(Manifest(
            new PublicKeyEntry { Id = "1", Alg = "UNSUPPORTED", PublicKey = "00" })));

        Func<Task> act = () => Verifier.VerifyAsyncJson("data", "signature", HttpsUrl, "1", fetcher);
        await act.Should().ThrowAsync<TamperproofException>()
            .WithMessage("Algorithm is not supported: UNSUPPORTED");
    }

    [Fact]
    public async Task VerifyAsyncJson_ReturnsTrueForRs256()
    {
        string signatureHex = SignLocalRs256(Data);
        var fetcher = new FakeManifestFetcher(Ok200(Manifest(
            new PublicKeyEntry { Id = "1", Alg = "RS256", PublicKey = LocalRsaSpkiHex })));

        (await Verifier.VerifyAsyncJson(Data, signatureHex, HttpsUrl, "1", fetcher)).Should().BeTrue();
    }

    [Fact]
    public async Task VerifyAsyncJson_AcceptsContentTypeWithCharset()
    {
        string signatureHex = SignLocalRs256(Data);
        var fetcher = new FakeManifestFetcher(Ok200(
            Manifest(new PublicKeyEntry { Id = "1", Alg = "RS256", PublicKey = LocalRsaSpkiHex }),
            contentType: "application/json; charset=utf-8"));

        (await Verifier.VerifyAsyncJson(Data, signatureHex, HttpsUrl, "1", fetcher)).Should().BeTrue();
    }

    [Fact]
    public async Task VerifyAsyncJson_ThrowsIfManifestTooLarge()
    {
        var fetcher = new FakeManifestFetcher(Ok200(
            Manifest(), contentLength: (64 * 1024 + 1).ToString()));

        Func<Task> act = () => Verifier.VerifyAsyncJson("data", "signature", HttpsUrl, "1", fetcher);
        await act.Should().ThrowAsync<TamperproofException>().WithMessage("Manifest too large");
    }

    [Fact]
    public async Task VerifyAsyncJson_UsesStrictFetchOptions()
    {
        string signatureHex = SignLocalRs256(Data);
        var fetcher = new FakeManifestFetcher(Ok200(Manifest(
            new PublicKeyEntry { Id = "1", Alg = "RS256", PublicKey = LocalRsaSpkiHex })));

        await Verifier.VerifyAsyncJson(Data, signatureHex, HttpsUrl, "1", fetcher);

        fetcher.Called.Should().BeTrue();
        fetcher.LastOptions!.Redirect.Should().Be("error");
        fetcher.LastOptions.Headers.Should().BeEquivalentTo(
            new Dictionary<string, string> { ["Accept"] = "application/json" });
        fetcher.LastOptions.CancellationToken.CanBeCanceled.Should().BeTrue();
    }

    // ========================================================================================
    // verify (direct)
    // ========================================================================================

    private static readonly ECDsa Ecdsa256 = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private static readonly ECDsa Ecdsa384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);
    private static readonly ECDsa Ecdsa521 = ECDsa.Create(ECCurve.NamedCurves.nistP521);
    private static readonly RSA Rsa = RSA.Create(2048);

    private static string EcdsaSign(ECDsa key, HashAlgorithmName hash) =>
        Hex.ToHex(key.SignData(DataBytes, hash, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));

    private static string RsaSign(HashAlgorithmName hash, RSASignaturePadding padding) =>
        Hex.ToHex(Rsa.SignData(DataBytes, hash, padding));

    [Fact]
    public void Verify_Es256()
    {
        Verifier.Verify(Data, EcdsaSign(Ecdsa256, HashAlgorithmName.SHA256),
            Ecdsa256.ExportSubjectPublicKeyInfo(), "ES256").Should().BeTrue();
    }

    [Fact]
    public void Verify_Es384()
    {
        Verifier.Verify(Data, EcdsaSign(Ecdsa384, HashAlgorithmName.SHA384),
            Ecdsa384.ExportSubjectPublicKeyInfo(), "ES384").Should().BeTrue();
    }

    [Fact]
    public void Verify_Es512()
    {
        Verifier.Verify(Data, EcdsaSign(Ecdsa521, HashAlgorithmName.SHA512),
            Ecdsa521.ExportSubjectPublicKeyInfo(), "ES512").Should().BeTrue();
    }

    [Fact]
    public void Verify_EdDSA()
    {
        var generator = new Ed25519KeyPairGenerator();
        generator.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));
        var pair = generator.GenerateKeyPair();
        var priv = (Ed25519PrivateKeyParameters)pair.Private;
        var pub = (Ed25519PublicKeyParameters)pair.Public;

        var signer = new Ed25519Signer();
        signer.Init(true, priv);
        signer.BlockUpdate(DataBytes, 0, DataBytes.Length);
        string signatureHex = Hex.ToHex(signer.GenerateSignature());

        byte[] spki = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(pub).GetDerEncoded();

        Verifier.Verify(Data, signatureHex, spki, "EdDSA").Should().BeTrue();
    }

    [Fact]
    public void Verify_Rs256()
    {
        Verifier.Verify(Data, RsaSign(HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1),
            Rsa.ExportSubjectPublicKeyInfo(), "RS256").Should().BeTrue();
    }

    [Fact]
    public void Verify_Rs384()
    {
        Verifier.Verify(Data, RsaSign(HashAlgorithmName.SHA384, RSASignaturePadding.Pkcs1),
            Rsa.ExportSubjectPublicKeyInfo(), "RS384").Should().BeTrue();
    }

    [Fact]
    public void Verify_Rs512()
    {
        Verifier.Verify(Data, RsaSign(HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1),
            Rsa.ExportSubjectPublicKeyInfo(), "RS512").Should().BeTrue();
    }

    [Fact]
    public void Verify_Ps256()
    {
        Verifier.Verify(Data, RsaSign(HashAlgorithmName.SHA256, RSASignaturePadding.Pss),
            Rsa.ExportSubjectPublicKeyInfo(), "PS256").Should().BeTrue();
    }

    [Fact]
    public void Verify_Ps384()
    {
        Verifier.Verify(Data, RsaSign(HashAlgorithmName.SHA384, RSASignaturePadding.Pss),
            Rsa.ExportSubjectPublicKeyInfo(), "PS384").Should().BeTrue();
    }

    [Fact]
    public void Verify_Ps512()
    {
        Verifier.Verify(Data, RsaSign(HashAlgorithmName.SHA512, RSASignaturePadding.Pss),
            Rsa.ExportSubjectPublicKeyInfo(), "PS512").Should().BeTrue();
    }

    [Fact]
    public void Verify_ObjectPayloadUsingCanonicalJson()
    {
        var payload = new Dictionary<string, object?>
        {
            ["method"] = "foo",
            ["params"] = new Dictionary<string, object?> { ["y"] = 2, ["x"] = 1 },
        };
        string canonical = CanonicalJson.CanonicalStringify(payload);
        string signatureHex = Hex.ToHex(
            Rsa.SignData(Encoding.UTF8.GetBytes(canonical), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

        Verifier.Verify(canonical, signatureHex, Rsa.ExportSubjectPublicKeyInfo(), "RS256").Should().BeTrue();
    }

    [Fact]
    public void Verify_ReturnsFalseForIncorrectPublicKey()
    {
        string signatureHex = RsaSign(HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var rsa2 = RSA.Create(2048);

        Verifier.Verify(Data, signatureHex, rsa2.ExportSubjectPublicKeyInfo(), "RS256").Should().BeFalse();
    }

    [Fact]
    public void Verify_ReturnsFalseForMalformedEcdsaSignatureLength()
    {
        string invalidLengthHex = string.Concat(Enumerable.Repeat("aa", 63)); // 63 bytes instead of 64
        Verifier.Verify(Data, invalidLengthHex, Ecdsa256.ExportSubjectPublicKeyInfo(), "ES256")
            .Should().BeFalse();
    }
}
