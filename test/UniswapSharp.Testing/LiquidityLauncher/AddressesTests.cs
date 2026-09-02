using UniswapSharp.Core.Utils;
using UniswapSharp.LiquidityLauncher;

namespace UniswapSharp.Testing.LiquidityLauncher;

// Ported from sdks/liquidity-launcher-sdk/src/addresses.test.ts.
public class AddressesTests
{
    private static string GetAddress(string a) => AddressValidator.GetAddress(a);

    // ---- getLauncherAddresses ----

    [Fact]
    public void GetLauncherAddresses_ReturnsUnichainLbpStrategySingleton()
    {
        var addresses = Addresses.GetLauncherAddresses((int)SupportedChainId.UNICHAIN);
        Assert.Equal(GetAddress("0x298ea05d0356b2ae5ccaa3169e471783ee9ea000"), addresses?.LbpStrategy);
    }

    [Fact]
    public void GetLauncherAddresses_UsesSameLiquidityLauncherCreate2AddressOnEveryChain()
    {
        var mainnet = Addresses.GetLauncherAddresses((int)SupportedChainId.MAINNET);
        var unichain = Addresses.GetLauncherAddresses((int)SupportedChainId.UNICHAIN);
        Assert.Equal(mainnet?.LiquidityLauncher, unichain!.LiquidityLauncher);
    }

    [Fact]
    public void GetLauncherAddresses_ReturnsNullForUnsupportedChain() =>
        Assert.Null(Addresses.GetLauncherAddresses(999999));

    [Fact]
    public void GetLauncherAddresses_ReturnsPerChainLbpStrategySingletons()
    {
        Assert.Equal(GetAddress("0x57bd0a9cd933c89ba55e086d53031367b6406000"),
            Addresses.GetLauncherAddresses((int)SupportedChainId.AVALANCHE)?.LbpStrategy);
        Assert.Equal(GetAddress("0x58df162ff41e5cb42b8515f75f90c1841938a000"),
            Addresses.GetLauncherAddresses((int)SupportedChainId.XLAYER)?.LbpStrategy);
        Assert.Equal(GetAddress("0x05d552391067389ee44fec3924157ed33f976000"),
            Addresses.GetLauncherAddresses((int)SupportedChainId.ROBINHOOD)?.LbpStrategy);
        Assert.Equal(GetAddress("0xe9f36bcc222a6d2e459529d787f8c060d543a000"),
            Addresses.GetLauncherAddresses((int)SupportedChainId.ARC)?.LbpStrategy);
    }

    // ---- the #223/#227 redeployed launcher (upstream 35c4e35) ----

    [Fact]
    public void GetLauncherAddresses_UsesTheRedeployedLauncherOnRobinhoodAndArc()
    {
        string redeployed = GetAddress("0x0000ffffbe8efe702c8703ae3477ff5de3d319c0");
        Assert.Equal(redeployed, Addresses.GetLauncherAddresses((int)SupportedChainId.ROBINHOOD)?.LiquidityLauncher);
        Assert.Equal(redeployed, Addresses.GetLauncherAddresses((int)SupportedChainId.ARC)?.LiquidityLauncher);

        // every other chain keeps the original mined vanity address
        Assert.NotEqual(redeployed, Addresses.GetLauncherAddresses((int)SupportedChainId.MAINNET)?.LiquidityLauncher);
    }

    [Fact]
    public void GetLauncherAddresses_CarriesUniversalRouterStrategyOnlyWhereDeployed()
    {
        Assert.Equal(GetAddress("0x1242c9439d589cae85e121b1f79f2af51e91dcee"),
            Addresses.GetLauncherAddresses((int)SupportedChainId.ROBINHOOD)?.UniversalRouterStrategy);
        Assert.Equal(GetAddress("0x0a122717bc36e3c7a7958128a5c789e0b070b3ae"),
            Addresses.GetLauncherAddresses((int)SupportedChainId.ARC)?.UniversalRouterStrategy);
        Assert.Null(Addresses.GetLauncherAddresses((int)SupportedChainId.MAINNET)?.UniversalRouterStrategy);
    }

    [Fact]
    public void GetLauncherAddresses_UsesTheBlocknumberishAwareCcaFactoryOnEveryChain()
    {
        // the 2026-07-09 v1.1.0 redeploy is now the ccaFactory everywhere, not just Robinhood
        string current = GetAddress("0x000000001f26a0044baa66024e7b6599c61963f8");
        foreach (var chain in new[]
        {
            SupportedChainId.MAINNET, SupportedChainId.ROBINHOOD, SupportedChainId.ARC, SupportedChainId.BASE,
        })
        {
            Assert.Equal(current, Addresses.GetLauncherAddresses((int)chain)?.CcaFactory);
        }
    }

    [Fact]
    public void GetTickDataLensForFactory_StillResolvesTheLegacyCcaFactory()
    {
        // retained so auctions created before the redeploy can still be read
        Assert.Equal(Addresses.TICK_DATA_LENS_V2,
            Addresses.GetTickDataLensForFactory("0x00cca200bf124dbfa848937c553864f4b4ce0632"));
    }

    [Fact]
    public void GetLauncherAddresses_UsesArcsOwnUerc20Factory()
    {
        Assert.Equal(GetAddress("0xff99d8f6c994607576eb652edcf12e04a7ebfbf6"),
            Addresses.GetLauncherAddresses((int)SupportedChainId.ARC)?.Uerc20Factory);
        Assert.NotEqual(Addresses.GetLauncherAddresses((int)SupportedChainId.ARC)?.Uerc20Factory,
            Addresses.GetLauncherAddresses((int)SupportedChainId.MAINNET)?.Uerc20Factory);
    }

    // ---- getTickDataLensForFactory ----

    [Fact]
    public void GetTickDataLensForFactory_MapsV1TwaFactoryToV1Lens() =>
        Assert.Equal(Addresses.TICK_DATA_LENS_V1,
            Addresses.GetTickDataLensForFactory("0xcccccccae7503cac057829bf2811de42e16e0bd5"));

    [Fact]
    public void GetTickDataLensForFactory_MapsEveryHistoricalCcaFactoryDeployToV2Lens()
    {
        Assert.Equal(Addresses.TICK_DATA_LENS_V2,
            Addresses.GetTickDataLensForFactory("0x088ca22b591f2f4bf0ad2780d2a44fa692e948d0"));
        Assert.Equal(Addresses.TICK_DATA_LENS_V2,
            Addresses.GetTickDataLensForFactory("0x00cCa200BF124dBfA848937c553864f4B4CE0632"));
        Assert.Equal(Addresses.TICK_DATA_LENS_V2,
            Addresses.GetTickDataLensForFactory("0x000000001F26a0044BaA66024e7b6599c61963F8"));
    }

    [Fact]
    public void GetTickDataLensForFactory_IsCaseInsensitive() =>
        Assert.Equal(Addresses.TICK_DATA_LENS_V2,
            Addresses.GetTickDataLensForFactory("0x00CCA200BF124DBFA848937C553864F4B4CE0632"));

    [Fact]
    public void GetTickDataLensForFactory_ReturnsNullForUnknownFactory() =>
        Assert.Null(Addresses.GetTickDataLensForFactory("0x0000000000000000000000000000000000000001"));

    [Fact]
    public void GetTickDataLensForFactory_CoversEveryCurrentPerChainCcaFactory()
    {
        foreach (var chainId in Enum.GetValues<SupportedChainId>())
        {
            var addresses = Addresses.GetLauncherAddresses((int)chainId)!;
            Assert.Equal(Addresses.TICK_DATA_LENS_V2, Addresses.GetTickDataLensForFactory(addresses.CcaFactory));
        }
    }

    [Fact]
    public void TickDataLensByFactory_IsDerivedFromTheDeploymentRegistry()
    {
        Assert.Equal(Addresses.AUCTION_FACTORY_DEPLOYMENTS.Count, Addresses.TICK_DATA_LENS_BY_FACTORY.Count);
        foreach (var deployment in Addresses.AUCTION_FACTORY_DEPLOYMENTS)
        {
            Assert.Equal(deployment.TickDataLens, Addresses.TICK_DATA_LENS_BY_FACTORY[deployment.Factory.ToLowerInvariant()]);
        }
    }

    // ---- selectTokenFactory ----

    [Fact]
    public void SelectTokenFactory_PrefersUerc20FactoryWhenBothArePresent()
    {
        var addresses = Addresses.GetLauncherAddresses((int)SupportedChainId.MAINNET)!;
        Assert.Equal(new SelectedTokenFactory(addresses.Uerc20Factory!, TokenFactoryKind.Uerc20),
            Addresses.SelectTokenFactory(addresses));
    }

    [Fact]
    public void SelectTokenFactory_FallsBackToSuperUerc20Factory()
    {
        var addresses = Addresses.GetLauncherAddresses((int)SupportedChainId.UNICHAIN)!;
        Assert.Equal(new SelectedTokenFactory(addresses.Usuperc20Factory!, TokenFactoryKind.Usuperc20),
            Addresses.SelectTokenFactory(addresses));
    }

    [Fact]
    public void SelectTokenFactory_SelectsUerc20FactoryOnLaunchChains()
    {
        var addresses = Addresses.GetLauncherAddresses((int)SupportedChainId.AVALANCHE)!;
        Assert.Equal(new SelectedTokenFactory(addresses.Uerc20Factory!, TokenFactoryKind.Uerc20),
            Addresses.SelectTokenFactory(addresses));
    }

    [Fact]
    public void SelectTokenFactory_ReturnsNullWhenChainDeploysNeitherFactory()
    {
        var withoutFactories = Addresses.GetLauncherAddresses((int)SupportedChainId.ROBINHOOD)!
            with
        { Uerc20Factory = null, Usuperc20Factory = null };
        Assert.Null(Addresses.SelectTokenFactory(withoutFactories));
    }
}
