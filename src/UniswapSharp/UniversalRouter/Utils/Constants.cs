using System.Numerics;

namespace UniswapSharp.UniversalRouter.Utils;

/// <summary>
/// Port of universal-router-sdk <c>utils/constants.ts</c> (<c>UniversalRouterVersion</c>).
/// Backing enum ordering (V1_2 &lt; V2_0 &lt; V2_1_1 &lt; V2_2_0) matches the upstream numeric string ordering.
/// </summary>
public enum UniversalRouterVersion
{
    V1_2 = 0,
    V2_0 = 1,
    V2_1_1 = 2,
    V2_2_0 = 3,
}

/// <summary>Helpers over <see cref="UniversalRouterVersion"/>.</summary>
public static class UniversalRouterVersionExtensions
{
    /// <summary>The upstream string value ('1.2', '2.0', '2.1.1', '2.2.0'); numeric fallback for unknown values.</summary>
    public static string Value(this UniversalRouterVersion version) => version switch
    {
        UniversalRouterVersion.V1_2 => "1.2",
        UniversalRouterVersion.V2_0 => "2.0",
        UniversalRouterVersion.V2_1_1 => "2.1.1",
        UniversalRouterVersion.V2_2_0 => "2.2.0",
        _ => "9." + ((int)version).ToString(),
    };
}

/// <summary>A deployed Universal Router (address + creation block) on a chain.</summary>
public sealed record RouterConfig(string Address, int CreationBlock);

/// <summary>Per-chain configuration: WETH, router configs by version, and optional SwapProxy.</summary>
public sealed record ChainConfig(
    string Weth,
    IReadOnlyDictionary<UniversalRouterVersion, RouterConfig> RouterConfigs,
    string? SwapProxy = null);

/// <summary>
/// Port of universal-router-sdk <c>utils/constants.ts</c>: chain configs, address/block lookups, and
/// the well-known address / uint sentinels used throughout the SDK.
/// </summary>
public static class Constants
{
    /// <summary>Check if a <see cref="UniversalRouterVersion"/> is at least V2_1_1.</summary>
    public static bool IsAtLeastV2_1_1(UniversalRouterVersion? version) =>
        version.HasValue && version.Value >= UniversalRouterVersion.V2_1_1;

    private const string WETH_NOT_SUPPORTED_ON_CHAIN = "0x0000000000000000000000000000000000000000";
    private const string SWAP_PROXY_DEPLOY_ADDRESS = "0x02E5be68D46DAc0B524905bfF209cf47EE6dB2a9";

    private static Dictionary<UniversalRouterVersion, RouterConfig> Rc(
        params (UniversalRouterVersion, string, int)[] entries)
    {
        var d = new Dictionary<UniversalRouterVersion, RouterConfig>();
        foreach (var (v, addr, block) in entries)
        {
            d[v] = new RouterConfig(addr, block);
        }
        return d;
    }

    /// <summary>Port of upstream <c>CHAIN_CONFIGS</c>.</summary>
    public static readonly IReadOnlyDictionary<int, ChainConfig> CHAIN_CONFIGS = new Dictionary<int, ChainConfig>
    {
        // mainnet
        [1] = new("0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2", Rc(
            (UniversalRouterVersion.V1_2, "0x3fC91A3afd70395Cd496C647d5a6CC9D4B2b7FAD", 17143817),
            (UniversalRouterVersion.V2_0, "0x66a9893cc07d91d95644aedd05d03f95e1dba8af", 21689092),
            (UniversalRouterVersion.V2_1_1, "0x4C82D1fBFe28C977cBB58D8C7FF8FCF9F70a2cCA", 24680568),
            (UniversalRouterVersion.V2_2_0, "0xCb640A86855f1A828c27241bA364348de28abe66", 25195294)),
            SWAP_PROXY_DEPLOY_ADDRESS),
        // goerli
        [5] = new("0xb4fbf271143f4fbf7b91a5ded31805e42b2208d6", Rc(
            (UniversalRouterVersion.V1_2, "0x3fC91A3afd70395Cd496C647d5a6CC9D4B2b7FAD", 8940568),
            (UniversalRouterVersion.V2_0, "0x3fC91A3afd70395Cd496C647d5a6CC9D4B2b7FAD", 8940568))),
        // sepolia
        [11155111] = new("0xfFf9976782d46CC05630D1f6eBAb18b2324d6B14", Rc(
            (UniversalRouterVersion.V1_2, "0x3fC91A3afd70395Cd496C647d5a6CC9D4B2b7FAD", 3543575),
            (UniversalRouterVersion.V2_0, "0x3a9d48ab9751398bbfa63ad67599bb04e4bdf98b", 7259601),
            (UniversalRouterVersion.V2_1_1, "0x8B844f885672f333Bc0042cB669255f93a4C1E6b", 10470160),
            (UniversalRouterVersion.V2_2_0, "0xB0C89059d7190EDb17eFF19829cc009cEe923916", 10941522)),
            SWAP_PROXY_DEPLOY_ADDRESS),
        // polygon
        [137] = new("0x0d500B1d8E8eF31E21C99d1Db9A6444d3ADf1270", Rc(
            (UniversalRouterVersion.V1_2, "0xec7BE89e9d109e7e3Fec59c222CF297125FEFda2", 52210153),
            (UniversalRouterVersion.V2_0, "0x1095692a6237d83c6a72f3f5efedb9a670c49223", 66980401),
            (UniversalRouterVersion.V2_1_1, "0x8B844f885672f333Bc0042cB669255f93a4C1E6b", 84336468)),
            SWAP_PROXY_DEPLOY_ADDRESS),
        // polygon mumbai
        [80001] = new("0x9c3C9283D3e44854697Cd22D3Faa240Cfb032889", Rc(
            (UniversalRouterVersion.V1_2, "0x3fC91A3afd70395Cd496C647d5a6CC9D4B2b7FAD", 35176052),
            (UniversalRouterVersion.V2_0, "0x3fC91A3afd70395Cd496C647d5a6CC9D4B2b7FAD", 35176052))),
        // optimism
        [10] = new("0x4200000000000000000000000000000000000006", Rc(
            (UniversalRouterVersion.V1_2, "0xCb1355ff08Ab38bBCE60111F1bb2B784bE25D7e8", 114702266),
            (UniversalRouterVersion.V2_0, "0x851116d9223fabed8e56c0e6b8ad0c31d98b3507", 130947687),
            (UniversalRouterVersion.V2_1_1, "0x8B844f885672f333Bc0042cB669255f93a4C1E6b", 149097062)),
            SWAP_PROXY_DEPLOY_ADDRESS),
        // optimism goerli
        [420] = new("0x4200000000000000000000000000000000000006", Rc(
            (UniversalRouterVersion.V1_2, "0x3fC91A3afd70395Cd496C647d5a6CC9D4B2b7FAD", 8887728),
            (UniversalRouterVersion.V2_0, "0x3fC91A3afd70395Cd496C647d5a6CC9D4B2b7FAD", 8887728))),
        // arbitrum
        [42161] = new("0x82aF49447D8a07e3bd95BD0d56f35241523fBab1", Rc(
            (UniversalRouterVersion.V1_2, "0x5E325eDA8064b456f4781070C0738d849c824258", 169472836),
            (UniversalRouterVersion.V2_0, "0xa51afafe0263b40edaef0df8781ea9aa03e381a3", 297842906),
            (UniversalRouterVersion.V2_1_1, "0x8B844f885672f333Bc0042cB669255f93a4C1E6b", 442902181)),
            SWAP_PROXY_DEPLOY_ADDRESS),
        // arbitrum goerli
        [421613] = new("0xe39Ab88f8A4777030A534146A9Ca3B52bd5D43A3", Rc(
            (UniversalRouterVersion.V1_2, "0x3fC91A3afd70395Cd496C647d5a6CC9D4B2b7FAD", 18815277),
            (UniversalRouterVersion.V2_0, "0x3fC91A3afd70395Cd496C647d5a6CC9D4B2b7FAD", 18815277))),
        // celo
        [42220] = new(WETH_NOT_SUPPORTED_ON_CHAIN, Rc(
            (UniversalRouterVersion.V1_2, "0x643770e279d5d0733f21d6dc03a8efbabf3255b4", 21407637),
            (UniversalRouterVersion.V2_0, "0xcb695bc5D3Aa22cAD1E6DF07801b061a05A0233A", 47387857),
            (UniversalRouterVersion.V2_1_1, "0x8B844f885672f333Bc0042cB669255f93a4C1E6b", 61893766)),
            SWAP_PROXY_DEPLOY_ADDRESS),
        // celo alfajores
        [44787] = new(WETH_NOT_SUPPORTED_ON_CHAIN, Rc(
            (UniversalRouterVersion.V1_2, "0x3fC91A3afd70395Cd496C647d5a6CC9D4B2b7FAD", 17566658),
            (UniversalRouterVersion.V2_0, "0x3fC91A3afd70395Cd496C647d5a6CC9D4B2b7FAD", 17566658))),
        // binance smart chain
        [56] = new("0xbb4CdB9CBd36B01bD1cBaEBF2De08d9173bc095c", Rc(
            (UniversalRouterVersion.V1_2, "0x4Dae2f939ACf50408e13d58534Ff8c2776d45265", 35160263),
            (UniversalRouterVersion.V2_0, "0x1906c1d672b88cd1b9ac7593301ca990f94eae07", 45970616),
            (UniversalRouterVersion.V2_1_1, "0x8B844f885672f333Bc0042cB669255f93a4C1E6b", 87208607)),
            SWAP_PROXY_DEPLOY_ADDRESS),
        // avalanche
        [43114] = new("0xB31f66AA3C1e785363F0875A1B74E27b85FD66c7", Rc(
            (UniversalRouterVersion.V1_2, "0x4Dae2f939ACf50408e13d58534Ff8c2776d45265", 40237257),
            (UniversalRouterVersion.V2_0, "0x94b75331ae8d42c1b61065089b7d48fe14aa73b7", 56195395),
            (UniversalRouterVersion.V2_1_1, "0x8B844f885672f333Bc0042cB669255f93a4C1E6b", 80616324)),
            SWAP_PROXY_DEPLOY_ADDRESS),
        // base goerli
        [84531] = new("0x4200000000000000000000000000000000000006", Rc(
            (UniversalRouterVersion.V1_2, "0xd0872d928672ae2ff74bdb2f5130ac12229cafaf", 6915289),
            (UniversalRouterVersion.V2_0, "0xd0872d928672ae2ff74bdb2f5130ac12229cafaf", 6915289))),
        // base mainnet
        [8453] = new("0x4200000000000000000000000000000000000006", Rc(
            (UniversalRouterVersion.V1_2, "0x3fC91A3afd70395Cd496C647d5a6CC9D4B2b7FAD", 9107268),
            (UniversalRouterVersion.V2_0, "0x6ff5693b99212da76ad316178a184ab56d299b43", 25350999),
            (UniversalRouterVersion.V2_1_1, "0xfdf682f51fe81aa4898f0ae2163d8a55c127fbc7", 43501203)),
            SWAP_PROXY_DEPLOY_ADDRESS),
        // blast
        [81457] = new("0x4300000000000000000000000000000000000004", Rc(
            (UniversalRouterVersion.V1_2, "0x643770E279d5D0733F21d6DC03A8efbABf3255B4", 1116444),
            (UniversalRouterVersion.V2_0, "0xeabbcb3e8e415306207ef514f660a3f820025be3", 14377319),
            (UniversalRouterVersion.V2_1_1, "0x8B844f885672f333Bc0042cB669255f93a4C1E6b", 32492192)),
            SWAP_PROXY_DEPLOY_ADDRESS),
        // zora
        [7777777] = new("0x4200000000000000000000000000000000000006", Rc(
            (UniversalRouterVersion.V1_2, "0x2986d9721A49838ab4297b695858aF7F17f38014", 11832155),
            (UniversalRouterVersion.V2_0, "0x3315ef7ca28db74abadc6c44570efdf06b04b020", 25434544),
            (UniversalRouterVersion.V2_1_1, "0xFdf682F51FE81Aa4898F0AE2163d8A55c127fbC7", 43550401)),
            SWAP_PROXY_DEPLOY_ADDRESS),
        // zksync
        [324] = new("0x5aea5775959fbc2557cc8789bc1bf90a239d9a91", Rc(
            (UniversalRouterVersion.V1_2, "0x28731BCC616B5f51dD52CF2e4dF0E78dD1136C06", 12640979),
            (UniversalRouterVersion.V2_0, "0x28731BCC616B5f51dD52CF2e4dF0E78dD1136C06", 12640979)),
            SWAP_PROXY_DEPLOY_ADDRESS),
        // worldchain
        [480] = new("0x4200000000000000000000000000000000000006", Rc(
            (UniversalRouterVersion.V1_2, "0x7a250d5630B4cF539739dF2C5dAcb4c659F2488D", 4063979),
            (UniversalRouterVersion.V2_0, "0x8ac7bee993bb44dab564ea4bc9ea67bf9eb5e743", 9111895),
            (UniversalRouterVersion.V2_1_1, "0x8B844f885672f333Bc0042cB669255f93a4C1E6b", 27228771)),
            SWAP_PROXY_DEPLOY_ADDRESS),
        // unichain sepolia
        [1301] = new("0x4200000000000000000000000000000000000006", Rc(
            (UniversalRouterVersion.V1_2, "0x8909Dc15e40173Ff4699343b6eB8132c65e18eC6", 1241811),
            (UniversalRouterVersion.V2_0, "0xf70536b3bcc1bd1a972dc186a2cf84cc6da6be5d", 7100543),
            (UniversalRouterVersion.V2_1_1, "0x8B844f885672f333Bc0042cB669255f93a4C1E6b", 46987997)),
            SWAP_PROXY_DEPLOY_ADDRESS),
        // unichain mainnet
        [130] = new("0x4200000000000000000000000000000000000006", Rc(
            (UniversalRouterVersion.V1_2, "0x4D73A4411CA1c660035e4AECC8270E5DdDEC8C17", 23678),
            (UniversalRouterVersion.V2_0, "0xef740bf23acae26f6492b10de645d6b98dc8eaf3", 6819690),
            (UniversalRouterVersion.V2_1_1, "0xFdf682F51FE81Aa4898F0AE2163d8A55c127fbC7", 43044663)),
            SWAP_PROXY_DEPLOY_ADDRESS),
        // monad testnet
        [10143] = new("0x760AfE86e5de5fa0Ee542fc7B7B713e1c5425701", Rc(
            (UniversalRouterVersion.V1_2, "0x3ae6d8a282d67893e17aa70ebffb33ee5aa65893", 23678),
            (UniversalRouterVersion.V2_0, "0x3ae6d8a282d67893e17aa70ebffb33ee5aa65893", 23678)),
            SWAP_PROXY_DEPLOY_ADDRESS),
        // base sepolia
        [84532] = new("0x4200000000000000000000000000000000000006", Rc(
            (UniversalRouterVersion.V1_2, "0x492e6456d9528771018deb9e87ef7750ef184104", 20216585),
            (UniversalRouterVersion.V2_0, "0x492e6456d9528771018deb9e87ef7750ef184104", 20216585),
            (UniversalRouterVersion.V2_1_1, "0x8B844f885672f333Bc0042cB669255f93a4C1E6b", 39035624)),
            SWAP_PROXY_DEPLOY_ADDRESS),
        // soneium
        [1868] = new("0x4200000000000000000000000000000000000006", Rc(
            (UniversalRouterVersion.V1_2, "0x0e2850543f69f678257266e0907ff9a58b3f13de", 3254782),
            (UniversalRouterVersion.V2_0, "0x0e2850543f69f678257266e0907ff9a58b3f13de", 3254782),
            (UniversalRouterVersion.V2_1_1, "0x8B844f885672f333Bc0042cB669255f93a4C1E6b", 20352508)),
            SWAP_PROXY_DEPLOY_ADDRESS),
        // monad
        [143] = new("0x3bd359C1119dA7Da1D913D1C4D2B7c461115433A", Rc(
            (UniversalRouterVersion.V1_2, "0x0d97dc33264bfc1c226207428a79b26757fb9dc3", 29255937),
            (UniversalRouterVersion.V2_0, "0x0d97dc33264bfc1c226207428a79b26757fb9dc3", 29255937),
            (UniversalRouterVersion.V2_1_1, "0xFdf682F51FE81Aa4898F0AE2163d8A55c127fbC7", 62239835)),
            SWAP_PROXY_DEPLOY_ADDRESS),
        // linea
        [59144] = new("0xe5D7C2a44FfDDf6b295A15c148167daaAf5Cf34f", Rc(
            (UniversalRouterVersion.V2_0, "0x661e93cca42afacb172121ef892830ca3b70f08d", 28974980),
            (UniversalRouterVersion.V2_1_1, "0x8B844f885672f333Bc0042cB669255f93a4C1E6b", 29782392)),
            SWAP_PROXY_DEPLOY_ADDRESS),
        // tempo
        [4217] = new(WETH_NOT_SUPPORTED_ON_CHAIN, Rc(
            (UniversalRouterVersion.V2_0, "0x1febb76be10aaf3a1402f04e8e835f2c382f7914", 6458546),
            (UniversalRouterVersion.V2_1_1, "0xFdf682F51FE81Aa4898F0AE2163d8A55c127fbC7", 10065560)),
            SWAP_PROXY_DEPLOY_ADDRESS),
        // megaeth
        [4326] = new("0x4200000000000000000000000000000000000006", Rc(
            (UniversalRouterVersion.V2_0, "0x48fd03529d2a91be835f07f6b72f53b4aad6093d", 7009661),
            (UniversalRouterVersion.V2_1_1, "0x47837eb80db5908eabba9105626d9b348bea7b02", 7009661)),
            SWAP_PROXY_DEPLOY_ADDRESS),
        // arc
        [5042] = new(WETH_NOT_SUPPORTED_ON_CHAIN, Rc(
            (UniversalRouterVersion.V2_1_1, "0x4fca4a51ab4f23a7447b3284fbd7d73289a89fb1", 1950059)),
            SWAP_PROXY_DEPLOY_ADDRESS),
        // robinhood
        [4663] = new("0x0Bd7D308f8E1639FAb988df18A8011f41EAcAD73", Rc(
            (UniversalRouterVersion.V2_1_1, "0x8876789976decbfcbbbe364623c63652db8c0904", 18127)),
            SWAP_PROXY_DEPLOY_ADDRESS),
        // ink
        [57073] = new("0x4200000000000000000000000000000000000006", Rc(
            (UniversalRouterVersion.V2_0, "0x112908dac86e20e7241b0927479ea3bf935d1fa0", 4580586),
            (UniversalRouterVersion.V2_1_1, "0x28bd21bb4ea4fda370d8d7544992038375d8d456", 47542762),
            (UniversalRouterVersion.V2_2_0, "0x28bd21bb4ea4fda370d8d7544992038375d8d456", 47542762)),
            "0x0000000085E102724e78eCd2F45DC9cA239Affad"),
        // xlayer
        [196] = new("0xe538905cf8410324e03A5A23C1c177a474D59b2b", Rc(
            (UniversalRouterVersion.V2_0, "0x5507749f2c558bb3e162c6e90c314c092e7372ff", 47680350),
            (UniversalRouterVersion.V2_1_1, "0x8B844f885672f333Bc0042cB669255f93a4C1E6b", 55072165)),
            SWAP_PROXY_DEPLOY_ADDRESS),
    };

    public static string UNIVERSAL_ROUTER_ADDRESS(UniversalRouterVersion version, int chainId)
    {
        if (!CHAIN_CONFIGS.TryGetValue(chainId, out var config))
        {
            throw new InvalidOperationException($"Universal Router not deployed on chain {chainId}");
        }
        if (!config.RouterConfigs.TryGetValue(version, out var routerConfig))
        {
            throw new InvalidOperationException($"Universal Router version {version.Value()} not deployed on chain {chainId}");
        }
        return routerConfig.Address;
    }

    public static string SWAP_PROXY_ADDRESS(int chainId)
    {
        if (!CHAIN_CONFIGS.TryGetValue(chainId, out var config))
        {
            throw new InvalidOperationException($"SwapProxy not deployed on chain {chainId}");
        }
        if (config.SwapProxy is null)
        {
            throw new InvalidOperationException($"SwapProxy not configured for chain {chainId}");
        }
        return config.SwapProxy;
    }

    public static int UNIVERSAL_ROUTER_CREATION_BLOCK(UniversalRouterVersion version, int chainId)
    {
        if (!CHAIN_CONFIGS.TryGetValue(chainId, out var config))
        {
            throw new InvalidOperationException($"Universal Router not deployed on chain {chainId}");
        }
        if (!config.RouterConfigs.TryGetValue(version, out var routerConfig))
        {
            throw new InvalidOperationException($"Universal Router version {version.Value()} not deployed on chain {chainId}");
        }
        return routerConfig.CreationBlock;
    }

    public static string WETH_ADDRESS(int chainId)
    {
        if (!CHAIN_CONFIGS.TryGetValue(chainId, out var config))
        {
            throw new InvalidOperationException($"Universal Router not deployed on chain {chainId}");
        }
        if (config.Weth == WETH_NOT_SUPPORTED_ON_CHAIN)
        {
            throw new InvalidOperationException($"Chain {chainId} does not have WETH");
        }
        return config.Weth;
    }

    public static readonly BigInteger CONTRACT_BALANCE = BigInteger.Pow(2, 255);
    public const string ETH_ADDRESS = "0x0000000000000000000000000000000000000000";
    public const string E_ETH_ADDRESS = "0xeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
    public const string ZERO_ADDRESS = "0x0000000000000000000000000000000000000000";
    public static readonly BigInteger MAX_UINT256 = BigInteger.Pow(2, 256) - 1;
    public static readonly BigInteger MAX_UINT160 = BigInteger.Pow(2, 160) - 1;

    public const string SENDER_AS_RECIPIENT = "0x0000000000000000000000000000000000000001";
    public const string ROUTER_AS_RECIPIENT = "0x0000000000000000000000000000000000000002";
}
