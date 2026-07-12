using System.Numerics;
using AwesomeAssertions;
using UniswapSharp.UniswapX.Utils;

namespace UniswapSharp.Testing.UniswapX;

// Port of uniswapx-sdk src/utils/NonceManager.test.ts (pure functions).
public class NonceManagerTests
{
    private static readonly BigInteger MaxUint256 = (BigInteger.One << 256) - 1;

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0, 1, 1)]
    [InlineData(0, 12, 12)]
    [InlineData(1, 0, 256)]
    [InlineData(1, 1, 257)]
    [InlineData(4, 1, 1025)]
    [InlineData(8, 1, 2049)]
    public void BuildNonce(int word, int bitPos, int expected) =>
        NonceManager.BuildNonce(word, bitPos).Should().Be(expected);

    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(0, 1, 2)]
    [InlineData(0, 8, 256)]
    [InlineData(1, 0, 1)]
    [InlineData(1, 1, 3)]
    [InlineData(16756735, 12, 16760831)]
    public void SetBit(long bitmap, int bitPos, long expected) =>
        NonceManager.SetBit(bitmap, bitPos).Should().Be(expected);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 0)]
    [InlineData(128, 0)]
    [InlineData(127, 7)]
    [InlineData(16756735, 12)]
    public void GetFirstUnsetBit(long bitmap, int expected) =>
        NonceManager.GetFirstUnsetBit(bitmap).Should().Be(expected);

    [Fact]
    public void GetFirstUnsetBit_MaxUint256() =>
        NonceManager.GetFirstUnsetBit(MaxUint256).Should().Be(-1);

    [Theory]
    [InlineData(0, "0", "1")]
    [InlineData(1, "0", "2")]
    [InlineData(2, "0", "4")]
    [InlineData(3, "0", "8")]
    [InlineData(256, "1", "1")]
    [InlineData(257, "1", "2")]
    public void GetCancelSingleParams(int nonce, string word, string mask)
    {
        var p = NonceManager.GetCancelSingleParams(nonce);
        p.Word.ToString().Should().Be(word);
        p.Mask.ToString().Should().Be(mask);
    }

    [Fact]
    public void GetCancelMultipleParams_SameWord()
    {
        var result = NonceManager.GetCancelMultipleParams(new BigInteger[] { 0, 1 });
        result.Should().HaveCount(1);
        result[0].Word.ToString().Should().Be("0");
        result[0].Mask.ToString().Should().Be("3");
    }

    [Fact]
    public void GetCancelMultipleParams_DifferentWords()
    {
        var result = NonceManager.GetCancelMultipleParams(new BigInteger[] { 0, 256 });
        result.Should().HaveCount(2);
        result[0].Word.ToString().Should().Be("0");
        result[1].Word.ToString().Should().Be("1");
        result[0].Mask.ToString().Should().Be("1");
        result[1].Mask.ToString().Should().Be("1");
    }
}
