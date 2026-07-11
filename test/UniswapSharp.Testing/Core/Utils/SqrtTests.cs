using System.Numerics;
using UniswapSharp.Core.Utils;

namespace UniswapSharp.Testing.Core.Utils;

public class SqrtTests
{
    public static IEnumerable<object[]> Data => new List<object[]> { new object[] { Enumerable.Range(0, 256).Select(i => i * 2).ToArray() } };

    [Fact]
    public void CorrectFor0To1000()
    {
        for (var i = 0; i < 1000; i++)
        {
            Assert.Equal(BigInteger.Parse(Math.Floor(Math.Sqrt(i)).ToString()), new BigInteger(i).Sqrt());
        }
    }

    [Theory]
    [MemberData(nameof(Data))]
    public void CorrectForAllEvenPowersOf2(int[] power)
    {
        foreach (var i in power)
        {
            var root = BigInteger.Pow(2, i);
            var rootSquared = root * root;

            Assert.Equal(root, rootSquared.Sqrt());
        }
    }

    [Fact]
    public void CorrectForMaxUint256()
    {
        var maxUint256 = BigInteger.Pow(2, 256) - 1;
        Assert.Equal(BigInteger.Parse("340282366920938463463374607431768211455"), maxUint256.Sqrt());
    }

    [Fact]
    public void CorrectForNonPerfectSquareAboveSafeInteger()
    {
        // k^2 - 1 (k = 94906267) is 9007199515875288, which lies in [2^53, 2^63).
        // Its exact floor(sqrt) is k - 1, but the double fast path rounds up to k — so the
        // built-in path must not be taken above Number.MAX_SAFE_INTEGER (2^53 - 1), matching
        // upstream which switches to Newton's method there.
        var k = new BigInteger(94906267);
        var value = k * k - 1;
        Assert.Equal(k - 1, value.Sqrt());
    }
}
