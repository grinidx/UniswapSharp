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
}
