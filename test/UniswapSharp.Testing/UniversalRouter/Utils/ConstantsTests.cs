using System.Text.RegularExpressions;
using UniswapSharp.UniversalRouter.Utils;

namespace UniswapSharp.Testing.UniversalRouter.Utils;

// Ported from sdks/universal-router-sdk/test/utils/constants.test.ts
public class ConstantsTests
{
    private static readonly int[] V211OnlyChainIds = { 5042, 4663 };

    [Fact]
    public void UniversalRouterAddress_ReturnsValidAddressForEveryDeployedVersionAndChain()
    {
        foreach (var (chainId, config) in Constants.CHAIN_CONFIGS)
        {
            foreach (var version in config.RouterConfigs.Keys)
            {
                string address = Constants.UNIVERSAL_ROUTER_ADDRESS(version, chainId);
                Assert.Matches(new Regex("^0x[a-fA-F0-9]{40}$"), address);
                Assert.Equal(address, Constants.UNIVERSAL_ROUTER_ADDRESS(version, chainId));
            }
        }
    }

    [Fact]
    public void UniversalRouterAddress_ThrowsForUnsupportedChain()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => Constants.UNIVERSAL_ROUTER_ADDRESS(UniversalRouterVersion.V1_2, 999999));
        Assert.Equal("Universal Router not deployed on chain 999999", ex.Message);
    }

    [Fact]
    public void UniversalRouterAddress_ThrowsForVersionNotDeployedOnExistingChain()
    {
        foreach (int chainId in new[] { 59144, 4326, 57073 })
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => Constants.UNIVERSAL_ROUTER_ADDRESS(UniversalRouterVersion.V1_2, chainId));
            Assert.Equal($"Universal Router version 1.2 not deployed on chain {chainId}", ex.Message);
        }

        foreach (int chainId in V211OnlyChainIds)
        {
            Assert.Equal($"Universal Router version 1.2 not deployed on chain {chainId}",
                Assert.Throws<InvalidOperationException>(
                    () => Constants.UNIVERSAL_ROUTER_ADDRESS(UniversalRouterVersion.V1_2, chainId)).Message);
            Assert.Equal($"Universal Router version 2.0 not deployed on chain {chainId}",
                Assert.Throws<InvalidOperationException>(
                    () => Constants.UNIVERSAL_ROUTER_ADDRESS(UniversalRouterVersion.V2_0, chainId)).Message);
        }
    }

    [Fact]
    public void UniversalRouterAddress_CorrectV2_1_1ForArcAndRobinhood()
    {
        Assert.Equal("0x4fca4a51ab4f23a7447b3284fbd7d73289a89fb1",
            Constants.UNIVERSAL_ROUTER_ADDRESS(UniversalRouterVersion.V2_1_1, 5042));
        Assert.Equal("0x8876789976decbfcbbbe364623c63652db8c0904",
            Constants.UNIVERSAL_ROUTER_ADDRESS(UniversalRouterVersion.V2_1_1, 4663));
    }

    [Fact]
    public void UniversalRouterAddress_AliasesInkV2_1_1ToDeployedV2_2_0()
    {
        Assert.Equal("0x28bd21bb4ea4fda370d8d7544992038375d8d456",
            Constants.UNIVERSAL_ROUTER_ADDRESS(UniversalRouterVersion.V2_1_1, 57073));
        Assert.Equal(Constants.UNIVERSAL_ROUTER_ADDRESS(UniversalRouterVersion.V2_2_0, 57073),
            Constants.UNIVERSAL_ROUTER_ADDRESS(UniversalRouterVersion.V2_1_1, 57073));
    }

    [Fact]
    public void UniversalRouterCreationBlock_ReturnsPositiveForEveryDeployedVersionAndChain()
    {
        foreach (var (chainId, config) in Constants.CHAIN_CONFIGS)
        {
            foreach (var version in config.RouterConfigs.Keys)
            {
                int block = Constants.UNIVERSAL_ROUTER_CREATION_BLOCK(version, chainId);
                Assert.True(block > 0);
            }
        }
    }

    [Fact]
    public void UniversalRouterCreationBlock_ThrowsForUnsupportedChain()
    {
        Assert.Equal("Universal Router not deployed on chain 999999",
            Assert.Throws<InvalidOperationException>(
                () => Constants.UNIVERSAL_ROUTER_CREATION_BLOCK(UniversalRouterVersion.V1_2, 999999)).Message);
    }

    [Fact]
    public void UniversalRouterCreationBlock_CorrectV2_1_1ForArcAndRobinhood()
    {
        Assert.Equal(1950059, Constants.UNIVERSAL_ROUTER_CREATION_BLOCK(UniversalRouterVersion.V2_1_1, 5042));
        Assert.Equal(18127, Constants.UNIVERSAL_ROUTER_CREATION_BLOCK(UniversalRouterVersion.V2_1_1, 4663));
    }

    [Fact]
    public void UniversalRouterCreationBlock_AliasesInkV2_1_1ToV2_2_0()
    {
        Assert.Equal(47542762, Constants.UNIVERSAL_ROUTER_CREATION_BLOCK(UniversalRouterVersion.V2_1_1, 57073));
        Assert.Equal(Constants.UNIVERSAL_ROUTER_CREATION_BLOCK(UniversalRouterVersion.V2_2_0, 57073),
            Constants.UNIVERSAL_ROUTER_CREATION_BLOCK(UniversalRouterVersion.V2_1_1, 57073));
    }

    [Fact]
    public void SwapProxyAddress_ReturnsInkAddress()
    {
        Assert.Equal("0x0000000085E102724e78eCd2F45DC9cA239Affad", Constants.SWAP_PROXY_ADDRESS(57073));
    }

    [Fact]
    public void SwapProxyAddress_ThrowsForUnsupportedChain()
    {
        Assert.Equal("SwapProxy not deployed on chain 999999",
            Assert.Throws<InvalidOperationException>(() => Constants.SWAP_PROXY_ADDRESS(999999)).Message);
    }

    [Fact]
    public void WethAddress_ThrowsForArcBecauseWethUnsupported()
    {
        Assert.Equal("Chain 5042 does not have WETH",
            Assert.Throws<InvalidOperationException>(() => Constants.WETH_ADDRESS(5042)).Message);
    }

    [Fact]
    public void WethAddress_ReturnsRobinhoodWeth()
    {
        Assert.Equal("0x0Bd7D308f8E1639FAb988df18A8011f41EAcAD73", Constants.WETH_ADDRESS(4663));
    }
}
