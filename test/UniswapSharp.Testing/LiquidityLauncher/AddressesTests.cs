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
        Assert.Equal(GetAddress("0x824a3ecde463dd45cc156b64cefa132596c9a000"), addresses?.LbpStrategy);
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
        Assert.Equal(GetAddress("0xcacd77134b072b4ad5621f585b0b422c6da4e000"),
            Addresses.GetLauncherAddresses((int)SupportedChainId.AVALANCHE)?.LbpStrategy);
        Assert.Equal(GetAddress("0x95bcb80e3804a085d23778f2956c305d6488e000"),
            Addresses.GetLauncherAddresses((int)SupportedChainId.XLAYER)?.LbpStrategy);
        Assert.Equal(GetAddress("0x843747f4c08e3393e55508f577296ba48e8ca000"),
            Addresses.GetLauncherAddresses((int)SupportedChainId.ROBINHOOD)?.LbpStrategy);
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
