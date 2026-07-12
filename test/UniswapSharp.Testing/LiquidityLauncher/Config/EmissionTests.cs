using System.Numerics;
using UniswapSharp.LiquidityLauncher;
using UniswapSharp.LiquidityLauncher.Config;

namespace UniswapSharp.Testing.LiquidityLauncher.Config;

// Ported from sdks/liquidity-launcher-sdk/src/config/emission.test.ts.
public class EmissionTests
{
    private static readonly BigInteger StartBlock = 1_000;
    private static readonly BigInteger EndBlock = 1_100;
    private static readonly IReadOnlyList<AuctionStepInput> Steps = Emission.DeriveConvexAuctionSteps(StartBlock, EndBlock);

    [Fact]
    public void DeriveConvexAuctionSteps_EmitsPerBlockMpsSummingToExactlyMpsTotal()
    {
        BigInteger total = Steps.Aggregate(BigInteger.Zero, (sum, s) => sum + s.Mps * (s.EndBlock - s.StartBlock));
        Assert.Equal(new BigInteger(Constants.MPS_TOTAL), total);
    }

    [Fact]
    public void DeriveConvexAuctionSteps_CoversTheWholeWindowContiguously()
    {
        Assert.Equal(StartBlock, Steps[0].StartBlock);
        Assert.Equal(EndBlock, Steps[^1].EndBlock);
        for (int i = 1; i < Steps.Count; i++)
        {
            Assert.Equal(Steps[i - 1].EndBlock, Steps[i].StartBlock);
        }
    }

    [Fact]
    public void DeriveConvexAuctionSteps_EndsWithASingleLargeFinalBlock()
    {
        var finalStep = Steps[^1];
        Assert.Equal(BigInteger.One, finalStep.EndBlock - finalStep.StartBlock);
        // The final block anchors ~30% of supply, far above any ramp step.
        Assert.True(finalStep.Mps > Steps[0].Mps);
    }

    [Fact]
    public void DeriveConvexAuctionSteps_RejectsAWindowTooShortToRamp() =>
        Assert.Throws<LauncherSdkError>(() => Emission.DeriveConvexAuctionSteps(0, 1));
}
