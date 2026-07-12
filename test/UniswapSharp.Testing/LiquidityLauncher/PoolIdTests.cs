using UniswapSharp.LiquidityLauncher;

namespace UniswapSharp.Testing.LiquidityLauncher;

// Ported from sdks/liquidity-launcher-sdk/src/poolId.test.ts.
public class PoolIdTests
{
    private const string ZeroAddress = "0x0000000000000000000000000000000000000000";
    private const string Token = "0x15d0e0c55a3e7ee67152ad7e89acf164253ff68d";

    // Golden vector from a real Unichain launch, read back from LBPStrategy.registeredPoolIds on-chain.
    [Fact]
    public void ComputeLbpPoolId_MatchesOnChainPoolIdForCanonicalHooklessLaunchPool()
    {
        string poolId = PoolId.ComputeLbpPoolId(ZeroAddress, Token, 10001, 200, ZeroAddress);
        Assert.Equal("0xcdb29fb7957c966296b36530969aa5f6fcf936e37519fb7ff2eb6147508b9fd7", poolId);
    }

    [Fact]
    public void ComputeLbpPoolId_IsIndependentOfCurrencyTokenArgumentOrder()
    {
        string a = PoolId.ComputeLbpPoolId(ZeroAddress, Token, 10001, 200, ZeroAddress);
        string b = PoolId.ComputeLbpPoolId(Token, ZeroAddress, 10001, 200, ZeroAddress);
        Assert.Equal(a, b);
    }

    [Fact]
    public void ComputeGraffiti_IsKeccak256OfAbiEncodedAddress() =>
        Assert.Equal("0x290decd9548b62a8d60345a988386fc84ba6bc95484008f6362f93160ef3e563",
            PoolId.ComputeGraffiti(ZeroAddress));
}
