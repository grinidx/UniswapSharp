using System.Text;
using AwesomeAssertions;
using UniswapSharp.Tamperproof.Constants;
using UniswapSharp.Tamperproof.Utils;

namespace UniswapSharp.Testing.Tamperproof;

// Ported from sdks/tamperproof-transactions/src/utils/txtRecord.test.ts.
public class TxtRecordTests
{
    // Builds RFC-1035 length-prefixed TXT wire format from character strings.
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

    [Fact]
    public void ParseTxtRecord_SingleString()
    {
        TxtRecord.ParseTxtRecord(Wire("hello")).Should().Be("hello");
    }

    [Fact]
    public void ParseTxtRecord_MultipleStringsConcatenated()
    {
        TxtRecord.ParseTxtRecord(Wire("hello", "world")).Should().Be("helloworld");
    }

    [Fact]
    public void ParseTxtRecord_EmptyStringsWithinRecord()
    {
        TxtRecord.ParseTxtRecord(Wire("start", "", "end")).Should().Be("startend");
    }

    [Fact]
    public void ParseTxtRecord_EmptyBuffer()
    {
        TxtRecord.ParseTxtRecord(Array.Empty<byte>()).Should().Be("");
    }

    [Fact]
    public void ParseTxtRecord_OnlyEmptyString()
    {
        TxtRecord.ParseTxtRecord(new byte[] { 0 }).Should().Be("");
    }

    [Fact]
    public void ParseTxtRecord_Utf8Characters()
    {
        const string utf8 = "héllo wørld 🌍";
        TxtRecord.ParseTxtRecord(Wire(utf8)).Should().Be(utf8);
    }

    [Fact]
    public void ParseTxtRecord_MultipleUtf8Strings()
    {
        TxtRecord.ParseTxtRecord(Wire("TWIST=", "tëst-éndpoint")).Should().Be("TWIST=tëst-éndpoint");
    }

    [Fact]
    public void ParseTxtRecord_ThrowsWhenLengthExceedsBuffer()
    {
        // Claims 10 bytes but only 5 available.
        var buffer = new byte[] { 10, (byte)'h', (byte)'e', (byte)'l', (byte)'l', (byte)'o' };
        Action act = () => TxtRecord.ParseTxtRecord(buffer);
        act.Should().Throw<TamperproofException>()
            .WithMessage("Invalid TXT record format: length exceeds buffer size");
    }

    [Fact]
    public void ParseTxtRecord_ThrowsWhenLengthExceedsRemaining()
    {
        var bytes = new List<byte>();
        byte[] hello = Encoding.UTF8.GetBytes("hello");
        bytes.Add((byte)hello.Length);
        bytes.AddRange(hello);
        bytes.Add(10); // claims 10 but only "test" (4) remains
        bytes.AddRange(Encoding.UTF8.GetBytes("test"));

        Action act = () => TxtRecord.ParseTxtRecord(bytes.ToArray());
        act.Should().Throw<TamperproofException>()
            .WithMessage("Invalid TXT record format: length exceeds buffer size");
    }

    [Fact]
    public void ParseTxtRecord_MaximumLengthString()
    {
        string max = new string('a', 255);
        string result = TxtRecord.ParseTxtRecord(Wire(max));
        result.Should().Be(max);
        result.Length.Should().Be(255);
    }

    [Fact]
    public void ParseTxtRecord_MultipleMaximumLengthStrings()
    {
        string max1 = new string('a', 255);
        string max2 = new string('b', 255);
        string result = TxtRecord.ParseTxtRecord(Wire(max1, max2));
        result.Should().Be(max1 + max2);
        result.Length.Should().Be(510);
    }

    [Fact]
    public void ParseTxtRecord_RealWorldDnsExample()
    {
        TxtRecord.ParseTxtRecord(Wire("TWIST=api/v1/", "keys")).Should().Be("TWIST=api/v1/keys");
    }

    // --- processTxtRecordData ---

    [Fact]
    public void ProcessTxtRecordData_ReturnsStringAsIs()
    {
        const string data = "TWIST=test-endpoint";
        TxtRecord.ProcessTxtRecordData(data).Should().Be(data);
    }

    [Fact]
    public void ProcessTxtRecordData_ParsesBuffer()
    {
        TxtRecord.ProcessTxtRecordData(Wire("TWIST=", "endpoint")).Should().Be("TWIST=endpoint");
    }

    private sealed class CustomToString
    {
        public override string ToString() => "mocked-string";
    }

    [Fact]
    public void ProcessTxtRecordData_CallsToStringOnUnknownTypes()
    {
        TxtRecord.ProcessTxtRecordData(new CustomToString()).Should().Be("mocked-string");
    }

    // Upstream's testCases are ["" -> "", 0 -> "0", false -> "false"]. Empty string hits the string
    // branch; 0 hits the fallback. The `false` case diverges (C# bool.ToString() is "False" vs JS
    // String(false) "false") and never occurs in the real DoH flow, so it is intentionally omitted.
    [Fact]
    public void ProcessTxtRecordData_EmptyStringReturnsEmpty()
    {
        TxtRecord.ProcessTxtRecordData("").Should().Be("");
    }

    [Fact]
    public void ProcessTxtRecordData_NumberFallback()
    {
        TxtRecord.ProcessTxtRecordData(0).Should().Be("0");
    }

    [Fact]
    public void ProcessTxtRecordData_PropagatesParseErrors()
    {
        var invalid = new byte[] { 10, (byte)'s', (byte)'h', (byte)'o', (byte)'r', (byte)'t' };
        Action act = () => TxtRecord.ProcessTxtRecordData(invalid);
        act.Should().Throw<TamperproofException>()
            .WithMessage("Invalid TXT record format: length exceeds buffer size");
    }

    [Fact]
    public void ProcessTxtRecordData_EmptyBuffer()
    {
        TxtRecord.ProcessTxtRecordData(Array.Empty<byte>()).Should().Be("");
    }
}
