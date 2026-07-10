using System.Numerics;
using UniswapSharp.V3.Utils;

namespace UniswapSharp.Testing.V3.Utils;

// Ported from sdks/v3-sdk/src/utils/position.test.ts (getTokensOwed),
// plus a wraparound case pinning the underlying subIn256 (tickLibrary.ts).
public class PositionLibraryTests
{
    private static readonly BigInteger Q128 = BigInteger.Pow(2, 128);

    [Fact]
    public void GetTokensOwed_Zero()
    {
        var (owed0, owed1) = PositionLibrary.GetTokensOwed(0, 0, 0, 0, 0);
        Assert.Equal(BigInteger.Zero, owed0);
        Assert.Equal(BigInteger.Zero, owed1);
    }

    [Fact]
    public void GetTokensOwed_NonZero()
    {
        var (owed0, owed1) = PositionLibrary.GetTokensOwed(0, 0, 1, Q128, Q128);
        Assert.Equal(BigInteger.One, owed0);
        Assert.Equal(BigInteger.One, owed1);
    }

    [Fact]
    public void GetTokensOwed_WrapsAroundOnUnderflow()
    {
        // feeGrowthInside < feeGrowthInsideLast: subIn256 wraps by +2^256.
        // subIn256(0, 1) = 2^256 - 1; * liquidity(1) / 2^128 = 2^128 - 1.
        var (owed0, owed1) = PositionLibrary.GetTokensOwed(1, 1, 1, 0, 0);
        Assert.Equal(Q128 - 1, owed0);
        Assert.Equal(Q128 - 1, owed1);
    }
}
