using UniswapSharp.Core.Utils;

namespace UniswapSharp.LiquidityLauncher;

/// <summary>Per-chain addresses of the Liquidity Launcher stack. Keyed by numeric chain id.</summary>
public record LauncherAddresses(
    // LiquidityLauncher singleton (the multicall entry point a wallet calls).
    string LiquidityLauncher,
    // LBPStrategy singleton (passed as Distribution.Strategy; owns registeredPoolIds).
    string LbpStrategy,
    // TokenSplitter strategy: routes the creator's un-auctioned portion (returned supply).
    string TokenSplitter,
    // ContinuousClearingAuction factory.
    string CcaFactory,
    // Permit2 (canonical address on every chain).
    string Permit2,
    // UniversalRouterStrategy singleton: the launcher-gated strategy that runs a caller-supplied
    // Universal Router route, so a launch and a creator buy fit in one multicall. Optional —
    // deployed only on chains carrying the redeployed launcher.
    string? UniversalRouterStrategy = null,
    // uERC20 factory (Ethereum-style chains). Optional.
    string? Uerc20Factory = null,
    // super-uERC20 factory (superchains). Optional.
    string? Usuperc20Factory = null,
    // Canonical Uniswap v4 PositionManager. Optional: lock is only offered where it's set.
    string? PositionManager = null);

/// <summary>Which token standard a new-token launch targets (selects its address-derivation scheme).</summary>
public enum TokenFactoryKind
{
    Uerc20,
    Usuperc20,
}

public record SelectedTokenFactory(string Factory, TokenFactoryKind Kind);

/// <summary>One historical auction-factory deployment, paired with the lens that reads its auctions.</summary>
public record AuctionFactoryDeployment(string Factory, string TickDataLens, string Description);

/// <summary>
/// Per-chain launcher addresses and the auction-factory deployment registry. Ported from
/// sdks/liquidity-launcher-sdk/src/addresses.ts.
/// </summary>
public static class Addresses
{
    private static string GetAddress(string address) => AddressValidator.GetAddress(address);

    private static readonly string PERMIT2 = GetAddress("0x000000000022D473030F116dDEE9F6B43aC78BA3");

    // Deployed at the same CREATE2 address on every supported chain.
    private static readonly string LIQUIDITY_LAUNCHER = GetAddress("0x00004c4ccc709Ef590F7C81102C0689F0263D4e9");
    // The #223/#227 redeployed launcher: the redeploy changed the launcher's bytecode, so the original
    // mined vanity salt no longer resolves to LIQUIDITY_LAUNCHER. Deployed on Robinhood (4663, the
    // 2026-08-05 full redeploy) and Arc (5042); once it is on every chain this collapses back into
    // LIQUIDITY_LAUNCHER.
    private static readonly string LIQUIDITY_LAUNCHER_REDEPLOYED = GetAddress("0x0000FffFBE8efE702c8703aE3477FF5dE3d319C0");
    // UniversalRouterStrategy, pinned to LIQUIDITY_LAUNCHER_REDEPLOYED as a constructor immutable.
    private static readonly string UNIVERSAL_ROUTER_STRATEGY_ROBINHOOD = GetAddress("0x1242c9439d589cAE85E121B1f79f2aF51e91DCEE");
    // Arc's deploy sits at a different address than Robinhood's despite the same launcher immutable.
    private static readonly string UNIVERSAL_ROUTER_STRATEGY_ARC = GetAddress("0x0A122717bc36E3C7A7958128a5C789E0b070b3Ae");
    // Current CCA factory: the 2026-07-09 redeploy built against blocknumberish v1.1.0, which translates
    // block.number on every chain that needs it. Every v3.1.0 LBPStrategy creates its auction through
    // this factory, so it is the ccaFactory for all chains.
    private static readonly string CCA_FACTORY = GetAddress("0x000000001F26a0044BaA66024e7b6599c61963F8");
    // Legacy CCA factory (blocknumberish v1.0.x), used by the pre-v3.1.0 strategies. Retained only so
    // the auction-factory registry can still resolve auctions created before the redeploy.
    private static readonly string CCA_FACTORY_LEGACY = GetAddress("0x00cCa200BF124dBfA848937c553864f4B4CE0632");
    private static readonly string TOKEN_SPLITTER = GetAddress("0x8B7DCeb5639DB986FCf86606C74e6300C40FE3cd");
    // 2026-08-05 full-redeploy TokenSplitter, chain 4663 only.
    private static readonly string TOKEN_SPLITTER_ROBINHOOD = GetAddress("0x4F5E3FBb9745358A92Da5674305FAb8D2B8a73cE");

    private static readonly string UERC20_FACTORY = GetAddress("0x000000e200088D55C39a11F609E5F667729ad49b");
    private static readonly string USUPERC20_FACTORY = GetAddress("0xeEeeEEE204Afb6BABb1287ffed52cCD6BA0b0fb2");
    // Arc's uERC20 factory is not at the shared CREATE2 address.
    private static readonly string UERC20_FACTORY_ARC = GetAddress("0xFf99D8f6C994607576eB652EDCf12E04a7EbfBf6");

    private static readonly string POSITION_MANAGER_MAINNET = GetAddress("0xbD216513d74C8cf14cf4747E6AaA6420FF64ee9e");
    private static readonly string POSITION_MANAGER_UNICHAIN = GetAddress("0x4529A01c7A0410167c5740C487A8DE60232617bf");
    private static readonly string POSITION_MANAGER_BASE = GetAddress("0x7C5f5A4bBd8fD63184577525326123B519429bDc");
    private static readonly string POSITION_MANAGER_ARBITRUM = GetAddress("0xd88F38F930b7952f2DB2432Cb002E7abbF3dD869");
    private static readonly string POSITION_MANAGER_AVALANCHE = GetAddress("0xB74b1F14d2754AcfcbBe1a221023a5cf50Ab8ACD");
    private static readonly string POSITION_MANAGER_XLAYER = GetAddress("0xcF1EAFC6928dC385A342E7C6491d371d2871458b");
    private static readonly string POSITION_MANAGER_ROBINHOOD = GetAddress("0x58daec3116aae6D93017bAAea7749052E8a04fA7");
    private static readonly string POSITION_MANAGER_ARC = GetAddress("0x6049c9a0e26405C0985f9E3685C87d0aE917f82B");
    private static readonly string POSITION_MANAGER_SEPOLIA = GetAddress("0x429ba70129df741B2Ca2a85BC3A2a3328e5c09b4");
    private static readonly string POSITION_MANAGER_BASE_SEPOLIA = GetAddress("0x4B2C77d209D3405F41a037Ec6c77F7F5b8e2ca80");

    /// <summary>All deployed launcher stacks, keyed by numeric chain id.</summary>
    public static readonly IReadOnlyDictionary<int, LauncherAddresses> LAUNCHER_ADDRESSES =
        new Dictionary<int, LauncherAddresses>
        {
            [(int)SupportedChainId.MAINNET] = new(
                LIQUIDITY_LAUNCHER, GetAddress("0x49380c4EfaB1b491006aF7FabAB8B3459F0E6000"), TOKEN_SPLITTER,
                CCA_FACTORY, PERMIT2, UniversalRouterStrategy: null,
                Uerc20Factory: UERC20_FACTORY, Usuperc20Factory: USUPERC20_FACTORY,
                PositionManager: POSITION_MANAGER_MAINNET),
            [(int)SupportedChainId.UNICHAIN] = new(
                LIQUIDITY_LAUNCHER, GetAddress("0x298eA05D0356B2Ae5cCAa3169E471783ee9EA000"), TOKEN_SPLITTER,
                CCA_FACTORY, PERMIT2, UniversalRouterStrategy: null,
                Uerc20Factory: null, Usuperc20Factory: USUPERC20_FACTORY,
                PositionManager: POSITION_MANAGER_UNICHAIN),
            [(int)SupportedChainId.BASE] = new(
                LIQUIDITY_LAUNCHER, GetAddress("0x34385dD739FE5464892BF0bA4CC42492804dA000"), TOKEN_SPLITTER,
                CCA_FACTORY, PERMIT2, UniversalRouterStrategy: null,
                Uerc20Factory: null, Usuperc20Factory: USUPERC20_FACTORY,
                PositionManager: POSITION_MANAGER_BASE),
            [(int)SupportedChainId.ARBITRUM_ONE] = new(
                LIQUIDITY_LAUNCHER, GetAddress("0x8Af0775a70Cc94D71DFc0fE809435e833F2Fe000"), TOKEN_SPLITTER,
                CCA_FACTORY, PERMIT2, UniversalRouterStrategy: null,
                Uerc20Factory: UERC20_FACTORY, Usuperc20Factory: null,
                PositionManager: POSITION_MANAGER_ARBITRUM),
            [(int)SupportedChainId.AVALANCHE] = new(
                LIQUIDITY_LAUNCHER, GetAddress("0x57BD0A9Cd933c89Ba55e086D53031367b6406000"), TOKEN_SPLITTER,
                CCA_FACTORY, PERMIT2, UniversalRouterStrategy: null,
                Uerc20Factory: UERC20_FACTORY, Usuperc20Factory: null,
                PositionManager: POSITION_MANAGER_AVALANCHE),
            [(int)SupportedChainId.XLAYER] = new(
                LIQUIDITY_LAUNCHER, GetAddress("0x58DF162fF41e5cB42B8515f75F90C1841938A000"), TOKEN_SPLITTER,
                CCA_FACTORY, PERMIT2, UniversalRouterStrategy: null,
                Uerc20Factory: UERC20_FACTORY, Usuperc20Factory: null,
                PositionManager: POSITION_MANAGER_XLAYER),
            [(int)SupportedChainId.ROBINHOOD] = new(
                LIQUIDITY_LAUNCHER_REDEPLOYED, GetAddress("0x05d552391067389EE44fec3924157ed33F976000"),
                TOKEN_SPLITTER_ROBINHOOD, CCA_FACTORY, PERMIT2,
                UniversalRouterStrategy: UNIVERSAL_ROUTER_STRATEGY_ROBINHOOD,
                Uerc20Factory: UERC20_FACTORY, Usuperc20Factory: null,
                PositionManager: POSITION_MANAGER_ROBINHOOD),
            [(int)SupportedChainId.ARC] = new(
                LIQUIDITY_LAUNCHER_REDEPLOYED, GetAddress("0xe9f36bcc222a6d2e459529D787f8c060d543A000"),
                TOKEN_SPLITTER, CCA_FACTORY, PERMIT2,
                UniversalRouterStrategy: UNIVERSAL_ROUTER_STRATEGY_ARC,
                Uerc20Factory: UERC20_FACTORY_ARC, Usuperc20Factory: null,
                PositionManager: POSITION_MANAGER_ARC),
            [(int)SupportedChainId.SEPOLIA] = new(
                LIQUIDITY_LAUNCHER, GetAddress("0x96641d91e223c766F45b19d09494F5925C3cE000"), TOKEN_SPLITTER,
                CCA_FACTORY, PERMIT2, UniversalRouterStrategy: null,
                Uerc20Factory: UERC20_FACTORY, Usuperc20Factory: null,
                PositionManager: POSITION_MANAGER_SEPOLIA),
            [(int)SupportedChainId.BASE_SEPOLIA] = new(
                LIQUIDITY_LAUNCHER, GetAddress("0xB06428b62c259eE982cE3D9BED47391dC9A5E000"), TOKEN_SPLITTER,
                CCA_FACTORY, PERMIT2, UniversalRouterStrategy: null,
                Uerc20Factory: null, Usuperc20Factory: USUPERC20_FACTORY,
                PositionManager: POSITION_MANAGER_BASE_SEPOLIA),
        };

    /// <summary>Returns the launch addresses for a chain, or <c>null</c> if the stack is not deployed there.</summary>
    public static LauncherAddresses? GetLauncherAddresses(int chainId) =>
        LAUNCHER_ADDRESSES.TryGetValue(chainId, out var addresses) ? addresses : null;

    // -----------------------------------------------------------------------
    // Auction factory deployment registry (chain-independent)
    // -----------------------------------------------------------------------

    private static readonly string TWA_FACTORY_V1 = GetAddress("0xcccccccae7503cac057829bf2811de42e16e0bd5");
    private static readonly string CCA_FACTORY_EARLY_TEST = GetAddress("0x088ca22b591f2f4bf0ad2780d2a44fa692e948d0");

    /// <summary>TickDataLens for v1 (TWA) auctions. CREATE2 — same address on every supported chain.</summary>
    public static readonly string TICK_DATA_LENS_V1 = GetAddress("0x5fAE46790F3F48A35e3792f89A9eC54FC52b207a");

    /// <summary>TickDataLens for v2 (CCA) auctions. CREATE2 — same address on every supported chain.</summary>
    public static readonly string TICK_DATA_LENS_V2 = GetAddress("0xc3C65F5453A3674aDb693cbdA3C842545cD30f53");

    /// <summary>Every auction factory ever deployed — current and historical — each paired with its lens.</summary>
    public static readonly IReadOnlyList<AuctionFactoryDeployment> AUCTION_FACTORY_DEPLOYMENTS = new[]
    {
        new AuctionFactoryDeployment(TWA_FACTORY_V1, TICK_DATA_LENS_V1, "v1 TWA auction factory"),
        new AuctionFactoryDeployment(CCA_FACTORY_EARLY_TEST, TICK_DATA_LENS_V2, "v2 CCA factory (early test deploy)"),
        new AuctionFactoryDeployment(CCA_FACTORY_LEGACY, TICK_DATA_LENS_V2, "v2 CCA factory (blocknumberish v1.0.x; superseded by the 2026-07-09 redeploy)"),
        new AuctionFactoryDeployment(CCA_FACTORY, TICK_DATA_LENS_V2, "v2 CCA factory (2026-07-09 blocknumberish v1.1.0 redeploy; current on all chains)"),
    };

    /// <summary>
    /// Factory address (lowercased) → TickDataLens, derived from <see cref="AUCTION_FACTORY_DEPLOYMENTS"/>.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> TICK_DATA_LENS_BY_FACTORY =
        AUCTION_FACTORY_DEPLOYMENTS.ToDictionary(d => d.Factory.ToLowerInvariant(), d => d.TickDataLens);

    /// <summary>Resolves the TickDataLens that reads auctions created by <paramref name="factoryAddress"/>.</summary>
    public static string? GetTickDataLensForFactory(string factoryAddress) =>
        TICK_DATA_LENS_BY_FACTORY.TryGetValue(factoryAddress.ToLowerInvariant(), out var lens) ? lens : null;

    /// <summary>
    /// Picks the new-token factory for a chain: prefer uERC20, fall back to super-uERC20; <c>null</c>
    /// when the chain deploys neither (new-token launches unsupported there).
    /// </summary>
    public static SelectedTokenFactory? SelectTokenFactory(LauncherAddresses addresses)
    {
        if (addresses.Uerc20Factory is not null)
        {
            return new SelectedTokenFactory(addresses.Uerc20Factory, TokenFactoryKind.Uerc20);
        }
        if (addresses.Usuperc20Factory is not null)
        {
            return new SelectedTokenFactory(addresses.Usuperc20Factory, TokenFactoryKind.Usuperc20);
        }
        return null;
    }
}
