using System.Numerics;
using UniswapSharp.V3.Utils;

namespace UniswapSharp.Testing.V3.Utils;

// Pins Utilities.ToHex to upstream's toHex (utils/calldata.ts): the minimal,
// even-length hex of the value with no sign nibble. The regression cases (200, 208,
// 255, ...) have a top nibble >= 8, where BigInteger.ToString("X") would otherwise
// prepend a "0" sign nibble and add a spurious leading byte.
public class ToHexTests
{
    [Theory]
    [InlineData("0", "0x00")]
    [InlineData("1", "0x01")]
    [InlineData("12", "0x0c")]
    [InlineData("100", "0x64")]      // top nibble 6 (< 8): no sign nibble
    [InlineData("200", "0xc8")]      // top nibble c (>= 8): sign-nibble regression
    [InlineData("208", "0xd0")]
    [InlineData("255", "0xff")]
    [InlineData("256", "0x0100")]
    [InlineData("3735928559", "0xdeadbeef")]
    public void ToHex_Matches(string value, string expected)
    {
        Assert.Equal(expected, Utilities.ToHex(BigInteger.Parse(value)));
    }

    [Fact]
    public void ToHex_MaxUint256()
    {
        BigInteger maxUint256 = BigInteger.Pow(2, 256) - 1;
        Assert.Equal("0x" + new string('f', 64), Utilities.ToHex(maxUint256));
    }
}
