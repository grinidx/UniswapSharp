using System.Text.Json;
using AwesomeAssertions;
using UniswapSharp.Tamperproof;
using UniswapSharp.Tamperproof.Utils;

namespace UniswapSharp.Testing.Tamperproof;

// Ported from sdks/tamperproof-transactions/src/generate.test.ts.
public class GenerateTests
{
    private static class TestKeys
    {
        public const string Rsassa =
            "30820122300d06092a864886f70d01010105000382010f003082010a02820101009d36845018ef5dc07a3097055a5657404be931644c98350ad86918ac3873dad2b3950ab8913856d1f47281a48eeec17737a0c7dd02f3dda3e1d86bfd72932968efee7b6d2a73e9b72a1eb741d3016b212a41f000936e0e7b9bc9726b7522447b8059a3263020c0685896f2d597a6b25dc8255c34c8ac12c3f6410d8200a8aa880f93cda8e7085550dba93ddb2623325094ef2fff466057998bf9da851c4ff7064a719cde40882ccec5c1c32ecc5918b63fb46416f1d3761aab4a2249737b5700e9e65df075a91cb33846e4efafccb45bfa622af11a6ff9ca6fcf7d3140d6227652b63337a90db79461957bb0390934454530292f243e9a2ace92d0375136e3f10203010001";

        public const string RsaPss =
            "30820122300d06092a864886f70d01010105000382010f003082010a0282010100a1b2c3d4e5f6789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef010203010001";

        public const string Ecdsa =
            "3059301306072a8648ce3d020106082a8648ce3d030107034200041234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";

        public const string Ed25519 =
            "302a300506032b6570032100abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890ab";
    }

    private sealed record ParsedKey(string Id, string Alg, string PublicKey);

    private static List<ParsedKey> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var list = new List<ParsedKey>();
        foreach (JsonElement el in doc.RootElement.GetProperty("publicKeys").EnumerateArray())
        {
            list.Add(new ParsedKey(
                el.GetProperty("id").GetString()!,
                el.GetProperty("alg").GetString()!,
                el.GetProperty("publicKey").GetString()!));
        }

        return list;
    }

    [Theory]
    [InlineData("RS256")]
    [InlineData("RS384")]
    [InlineData("RS512")]
    public void SingleKey_Rsassa(string alg)
    {
        var parsed = Parse(Generator.Generate(new PublicKey(TestKeys.Rsassa, alg)));
        parsed.Should().HaveCount(1);
        parsed[0].Should().Be(new ParsedKey("1", alg, $"0x{TestKeys.Rsassa}"));
    }

    [Theory]
    [InlineData("PS256")]
    [InlineData("PS384")]
    [InlineData("PS512")]
    public void SingleKey_RsaPss(string alg)
    {
        var parsed = Parse(Generator.Generate(new PublicKey(TestKeys.RsaPss, alg)));
        parsed.Should().HaveCount(1);
        parsed[0].Should().Be(new ParsedKey("1", alg, Hex.NormalizeHex(TestKeys.RsaPss)));
    }

    [Theory]
    [InlineData("ES256")]
    [InlineData("ES384")]
    [InlineData("ES512")]
    public void SingleKey_Ecdsa(string alg)
    {
        var parsed = Parse(Generator.Generate(new PublicKey(TestKeys.Ecdsa, alg)));
        parsed.Should().HaveCount(1);
        parsed[0].Should().Be(new ParsedKey("1", alg, $"0x{TestKeys.Ecdsa}"));
    }

    [Fact]
    public void SingleKey_Ed25519()
    {
        var parsed = Parse(Generator.Generate(new PublicKey(TestKeys.Ed25519, "EdDSA")));
        parsed.Should().HaveCount(1);
        parsed[0].Should().Be(new ParsedKey("1", "EdDSA", $"0x{TestKeys.Ed25519}"));
    }

    [Fact]
    public void TwoDifferentAlgorithms()
    {
        var parsed = Parse(Generator.Generate(
            new PublicKey(TestKeys.Rsassa, "RS256"),
            new PublicKey(TestKeys.Ecdsa, "ES256")));
        parsed.Should().HaveCount(2);
        parsed[0].Should().Be(new ParsedKey("1", "RS256", $"0x{TestKeys.Rsassa}"));
        parsed[1].Should().Be(new ParsedKey("2", "ES256", $"0x{TestKeys.Ecdsa}"));
    }

    [Fact]
    public void ThreeDifferentAlgorithms()
    {
        var parsed = Parse(Generator.Generate(
            new PublicKey(TestKeys.Rsassa, "RS256"),
            new PublicKey(TestKeys.Ecdsa, "ES256"),
            new PublicKey(TestKeys.Ed25519, "EdDSA")));
        parsed.Should().HaveCount(3);
        parsed[0].Should().Be(new ParsedKey("1", "RS256", $"0x{TestKeys.Rsassa}"));
        parsed[1].Should().Be(new ParsedKey("2", "ES256", $"0x{TestKeys.Ecdsa}"));
        parsed[2].Should().Be(new ParsedKey("3", "EdDSA", $"0x{TestKeys.Ed25519}"));
    }

    [Fact]
    public void AllFourAlgorithms()
    {
        var parsed = Parse(Generator.Generate(
            new PublicKey(TestKeys.Rsassa, "RS256"),
            new PublicKey(TestKeys.RsaPss, "PS256"),
            new PublicKey(TestKeys.Ecdsa, "ES256"),
            new PublicKey(TestKeys.Ed25519, "EdDSA")));
        parsed.Should().HaveCount(4);
        parsed[0].Should().Be(new ParsedKey("1", "RS256", $"0x{TestKeys.Rsassa}"));
        parsed[1].Should().Be(new ParsedKey("2", "PS256", Hex.NormalizeHex(TestKeys.RsaPss)));
        parsed[2].Should().Be(new ParsedKey("3", "ES256", $"0x{TestKeys.Ecdsa}"));
        parsed[3].Should().Be(new ParsedKey("4", "EdDSA", $"0x{TestKeys.Ed25519}"));
    }

    [Fact]
    public void DuplicateAlgorithmsWithDifferentKeys()
    {
        const string alternateEcdsaKey =
            "3059301306072a8648ce3d020106082a8648ce3d030107034200040987654321fedcba0987654321fedcba0987654321fedcba0987654321fedcba0987654321fedcba0987654321fedcba0987654321fedcba0987654321fedcba";

        var parsed = Parse(Generator.Generate(
            new PublicKey(TestKeys.Ecdsa, "ES256"),
            new PublicKey(alternateEcdsaKey, "ES256")));
        parsed.Should().HaveCount(2);
        parsed[0].Should().Be(new ParsedKey("1", "ES256", $"0x{TestKeys.Ecdsa}"));
        parsed[1].Should().Be(new ParsedKey("2", "ES256", $"0x{alternateEcdsaKey}"));
    }

    [Fact]
    public void EmptyInput()
    {
        var parsed = Parse(Generator.Generate());
        parsed.Should().BeEmpty();
    }

    [Fact]
    public void InvalidAlgorithm_Throws()
    {
        Action act = () => Generator.Generate(new PublicKey(TestKeys.Rsassa, "INVALID_ALGORITHM"));
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void PreservesKeyOrder()
    {
        var parsed = Parse(Generator.Generate(
            new PublicKey(TestKeys.Ed25519, "EdDSA"),
            new PublicKey(TestKeys.Rsassa, "RS256"),
            new PublicKey(TestKeys.Ecdsa, "ES256")));
        parsed.Should().HaveCount(3);
        parsed[0].Alg.Should().Be("EdDSA");
        parsed[1].Alg.Should().Be("RS256");
        parsed[2].Alg.Should().Be("ES256");
    }

    [Fact]
    public void SequentialIdsRegardlessOfAlgorithm()
    {
        var parsed = Parse(Generator.Generate(
            new PublicKey(TestKeys.Ed25519, "EdDSA"),
            new PublicKey(TestKeys.RsaPss, "PS256"),
            new PublicKey(TestKeys.Rsassa, "RS256"),
            new PublicKey(TestKeys.Ecdsa, "ES256")));
        parsed.Select(k => k.Id).Should().Equal("1", "2", "3", "4");
    }

    [Fact]
    public void PreservesExisting0xPrefix()
    {
        string keyWithPrefix = $"0x{TestKeys.Ecdsa}";
        var parsed = Parse(Generator.Generate(new PublicKey(keyWithPrefix, "ES256")));
        parsed.Should().HaveCount(1);
        parsed[0].Should().Be(new ParsedKey("1", "ES256", keyWithPrefix));
    }

    [Fact]
    public void Adds0xPrefixWhenMissing()
    {
        var parsed = Parse(Generator.Generate(new PublicKey(TestKeys.Ecdsa, "ES256")));
        parsed.Should().HaveCount(1);
        parsed[0].Should().Be(new ParsedKey("1", "ES256", $"0x{TestKeys.Ecdsa}"));
    }

    [Fact]
    public void MixedKeysWithAndWithout0xPrefix()
    {
        string keyWithPrefix = $"0x{TestKeys.Rsassa}";
        var parsed = Parse(Generator.Generate(
            new PublicKey(keyWithPrefix, "RS256"),
            new PublicKey(TestKeys.Ecdsa, "ES256")));
        parsed.Should().HaveCount(2);
        parsed[0].Should().Be(new ParsedKey("1", "RS256", keyWithPrefix));
        parsed[1].Should().Be(new ParsedKey("2", "ES256", $"0x{TestKeys.Ecdsa}"));
    }

    [Fact]
    public void DoesNotCreateDouble0xPrefix()
    {
        string keyWithPrefix = $"0x{TestKeys.Ed25519}";
        var parsed = Parse(Generator.Generate(new PublicKey(keyWithPrefix, "EdDSA")));
        parsed.Should().HaveCount(1);
        parsed[0].PublicKey.Should().Be(keyWithPrefix);
        parsed[0].PublicKey.Should().NotContain("0x0x");
    }

    [Fact]
    public void ThrowsForNonHexCharacters()
    {
        Action act = () => Generator.Generate(new PublicKey("not-hex", "ES256"));
        act.Should().Throw<Exception>().WithMessage("*Invalid hex string*");
    }

    [Fact]
    public void ThrowsForInvalidHexWith0xPrefix()
    {
        Action act = () => Generator.Generate(new PublicKey("0xzzzz", "ES256"));
        act.Should().Throw<Exception>().WithMessage("*Invalid hex string*");
    }
}
