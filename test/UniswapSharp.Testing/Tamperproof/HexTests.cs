using AwesomeAssertions;
using UniswapSharp.Tamperproof.Constants;
using UniswapSharp.Tamperproof.Utils;

namespace UniswapSharp.Testing.Tamperproof;

// Ported from sdks/tamperproof-transactions/src/utils/hex.test.ts.
public class HexTests
{
    // --- fromHex: valid hex strings ---

    [Fact]
    public void FromHex_EmptyStringToEmptyArray()
    {
        Hex.FromHex("").Should().Equal(Array.Empty<byte>());
    }

    [Fact]
    public void FromHex_SimpleString()
    {
        Hex.FromHex("48656c6c6f").Should().Equal(new byte[] { 0x48, 0x65, 0x6c, 0x6c, 0x6f });
    }

    [Fact]
    public void FromHex_UppercaseString()
    {
        Hex.FromHex("48656C6C6F").Should().Equal(new byte[] { 0x48, 0x65, 0x6c, 0x6c, 0x6f });
    }

    [Fact]
    public void FromHex_MixedCaseString()
    {
        Hex.FromHex("48656c6C6F").Should().Equal(new byte[] { 0x48, 0x65, 0x6c, 0x6c, 0x6f });
    }

    [Fact]
    public void FromHex_AllByteValues()
    {
        Hex.FromHex("00ff8001").Should().Equal(new byte[] { 0x00, 0xff, 0x80, 0x01 });
    }

    [Fact]
    public void FromHex_CleansWhitespace()
    {
        Hex.FromHex("48 65 6c 6c 6f").Should().Equal(new byte[] { 0x48, 0x65, 0x6c, 0x6c, 0x6f });
    }

    [Fact]
    public void FromHex_CleansTabsAndNewlines()
    {
        Hex.FromHex("48\t65\n6c\r6c\n6f").Should().Equal(new byte[] { 0x48, 0x65, 0x6c, 0x6c, 0x6f });
    }

    // --- fromHex: 0x prefix ---

    [Fact]
    public void FromHex_0xLowercase()
    {
        Hex.FromHex("0x48656c6c6f").Should().Equal(new byte[] { 0x48, 0x65, 0x6c, 0x6c, 0x6f });
    }

    [Fact]
    public void FromHex_0XUppercase()
    {
        Hex.FromHex("0X48656c6c6f").Should().Equal(new byte[] { 0x48, 0x65, 0x6c, 0x6c, 0x6f });
    }

    [Fact]
    public void FromHex_0xMixedCase()
    {
        Hex.FromHex("0x48656C6C6F").Should().Equal(new byte[] { 0x48, 0x65, 0x6c, 0x6c, 0x6f });
    }

    [Fact]
    public void FromHex_0xEmpty()
    {
        Hex.FromHex("0x").Should().Equal(Array.Empty<byte>());
    }

    [Fact]
    public void FromHex_0xAllByteValues()
    {
        Hex.FromHex("0x00ff8001").Should().Equal(new byte[] { 0x00, 0xff, 0x80, 0x01 });
    }

    [Fact]
    public void FromHex_0xWithWhitespaceAfterPrefix()
    {
        Hex.FromHex("0x48 65 6c 6c 6f").Should().Equal(new byte[] { 0x48, 0x65, 0x6c, 0x6c, 0x6f });
    }

    [Fact]
    public void FromHex_0xOddLengthThrows()
    {
        Action act = () => Hex.FromHex("0x48656c6c6");
        act.Should().Throw<TamperproofException>().WithMessage("Invalid hex string: length must be even");
    }

    [Fact]
    public void FromHex_0xInvalidCharsThrows()
    {
        Action act = () => Hex.FromHex("0x48656g6c6f");
        act.Should().Throw<TamperproofException>().WithMessage("Invalid hex string: 48656g6c6f");
    }

    // --- fromHex: invalid strings ---

    [Fact]
    public void FromHex_OddLengthThrows()
    {
        Action act = () => Hex.FromHex("48656c6c6");
        act.Should().Throw<TamperproofException>().WithMessage("Invalid hex string: length must be even");
    }

    [Fact]
    public void FromHex_OddLengthAfterWhitespaceThrows()
    {
        Action act = () => Hex.FromHex("48 65 6c 6c 6");
        act.Should().Throw<TamperproofException>().WithMessage("Invalid hex string: length must be even");
    }

    [Fact]
    public void FromHex_PartiallyInvalidThrows()
    {
        Action act = () => Hex.FromHex("48656g6c6f");
        act.Should().Throw<TamperproofException>().WithMessage("Invalid hex string: 48656g6c6f");
    }

    [Fact]
    public void FromHex_NonHexThrows()
    {
        Action act = () => Hex.FromHex("hello!");
        act.Should().Throw<TamperproofException>().WithMessage("Invalid hex string: hello!");
    }

    [Fact]
    public void FromHex_MixedValidInvalidThrows()
    {
        Action act = () => Hex.FromHex("48656c6x6f");
        act.Should().Throw<TamperproofException>().WithMessage("Invalid hex string: 48656c6x6f");
    }

    // --- toHex ---

    [Fact]
    public void ToHex_EmptyArray()
    {
        Hex.ToHex(Array.Empty<byte>()).Should().Be("");
    }

    [Fact]
    public void ToHex_Bytes()
    {
        Hex.ToHex(new byte[] { 0x48, 0x65, 0x6c, 0x6c, 0x6f }).Should().Be("48656c6c6f");
    }

    [Fact]
    public void ToHex_AllByteValues()
    {
        Hex.ToHex(new byte[] { 0x00, 0xff, 0x80, 0x01 }).Should().Be("00ff8001");
    }

    [Fact]
    public void ToHex_PadsSingleDigit()
    {
        Hex.ToHex(new byte[] { 0x01, 0x02, 0x0f }).Should().Be("01020f");
    }

    // --- round-trip ---

    [Fact]
    public void RoundTrip_HexToBytesToHex()
    {
        const string original = "48656c6c6f776f726c64";
        Hex.ToHex(Hex.FromHex(original)).Should().Be(original);
    }

    [Fact]
    public void RoundTrip_NormalizesCase()
    {
        const string mixedCase = "48656C6C6F576F726C64";
        Hex.ToHex(Hex.FromHex(mixedCase)).Should().Be(mixedCase.ToLowerInvariant());
    }

    [Fact]
    public void RoundTrip_AllByteValues()
    {
        const string original =
            "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f202122232425262728292a2b2c2d2e2f303132333435363738393a3b3c3d3e3f404142434445464748494a4b4c4d4e4f505152535455565758595a5b5c5d5e5f606162636465666768696a6b6c6d6e6f707172737475767778797a7b7c7d7e7f808182838485868788898a8b8c8d8e8f909192939495969798999a9b9c9d9e9fa0a1a2a3a4a5a6a7a8a9aaabacadaeafb0b1b2b3b4b5b6b7b8b9babbbcbdbebfc0c1c2c3c4c5c6c7c8c9cacbcccdcecfd0d1d2d3d4d5d6d7d8d9dadbdcdddedfe0e1e2e3e4e5e6e7e8e9eaebecedeeeff0f1f2f3f4f5f6f7f8f9fafbfcfdfeff";
        Hex.ToHex(Hex.FromHex(original)).Should().Be(original);
    }

    [Fact]
    public void RoundTrip_WhitespaceInOriginal()
    {
        Hex.ToHex(Hex.FromHex("48 65 6c 6c 6f")).Should().Be("48656c6c6f");
    }

    [Fact]
    public void RoundTrip_BytesToHexToBytes()
    {
        byte[] original = { 0x48, 0x65, 0x6c, 0x6c, 0x6f };
        Hex.FromHex(Hex.ToHex(original)).Should().Equal(original);
    }

    [Fact]
    public void RoundTrip_Empty()
    {
        Hex.ToHex(Hex.FromHex("")).Should().Be("");
    }

    [Fact]
    public void RoundTrip_With0xPrefix()
    {
        Hex.ToHex(Hex.FromHex("0x48656c6c6f776f726c64")).Should().Be("48656c6c6f776f726c64");
    }

    [Fact]
    public void RoundTrip_BytesToHexAndBackWith0x()
    {
        byte[] original = { 0x48, 0x65, 0x6c, 0x6c, 0x6f };
        Hex.FromHex("0x" + Hex.ToHex(original)).Should().Equal(original);
    }

    // --- fromBase64 (platform-shim branches omitted; see Hex.FromBase64 doc) ---

    [Fact]
    public void FromBase64_Decodes()
    {
        Hex.FromBase64("SGVsbG8=").Should().Equal(new byte[] { 0x48, 0x65, 0x6c, 0x6c, 0x6f });
    }

    [Fact]
    public void FromBase64_StripsWhitespace()
    {
        Hex.FromBase64("S GV s\n bG8=  ").Should().Equal(new byte[] { 0x48, 0x65, 0x6c, 0x6c, 0x6f });
    }
}
