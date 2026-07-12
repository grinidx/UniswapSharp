using System.Numerics;

namespace UniswapSharp.UniswapX;

/// <summary>The kind of a UniswapX order (uniswapx-sdk <c>OrderType</c>).</summary>
public enum OrderType
{
    Dutch,
    Relay,
    Dutch_V2,
    Dutch_V3,
    Limit,
    Priority,
    V4,
    Hybrid,
}

/// <summary>Which contract interface a permissioned token exposes (uniswapx-sdk <c>PermissionedTokenInterface</c>).</summary>
public enum PermissionedTokenInterface
{
    DSTokenInterface,
    ISuperstateTokenV4,
}

/// <summary>The proxy pattern a permissioned token is deployed behind (uniswapx-sdk <c>PermissionedTokenProxyType</c>).</summary>
public enum PermissionedTokenProxyType
{
    None,
    Standard,
    ERC1967,
}

/// <summary>A permissioned token entry (uniswapx-sdk <c>PermissionedToken</c>).</summary>
public sealed record PermissionedToken(
    string Address,
    int ChainId,
    string Symbol,
    PermissionedTokenInterface Interface,
    PermissionedTokenProxyType? ProxyType = null);

/// <summary>The reverse-mapping value: the order type a reactor/resolver address resolves to.</summary>
public sealed record OrderTypeMapping(OrderType OrderType);

/// <summary>Port of uniswapx-sdk <c>constants.ts</c> (and <c>constants/v4.ts</c> in <see cref="ConstantsV4"/>).</summary>
public static class Constants
{
    // ChainId.MAINNET, GOERLI, POLYGON, BASE, UNICHAIN.
    private static readonly int[] NetworksWithSameAddress = { 1, 5, 137, 8453, 130 };

    /// <summary>Builds a map that assigns <paramref name="address"/> to every network sharing the canonical address, plus <paramref name="additionalNetworks"/>.</summary>
    public static Dictionary<int, T> ConstructSameAddressMap<T>(T address, params int[] additionalNetworks)
    {
        var memo = new Dictionary<int, T>();
        foreach (var chainId in NetworksWithSameAddress.Concat(additionalNetworks))
        {
            memo[chainId] = address;
        }
        return memo;
    }

    public static readonly IReadOnlyDictionary<int, string> Permit2Mapping = BuildPermit2Mapping();

    private static Dictionary<int, string> BuildPermit2Mapping()
    {
        var map = ConstructSameAddressMap(
            "0x000000000022d473030f116ddee9f6b43ac78ba3",
            11155111, 10, 30, 56, 100, 143, 196, 480, 1284, 1868, 4217, 4326,
            4663, 5042, 10143, 42161, 42220, 43114, 59144, 81457, 84532,
            421614, 7777777, 11155420, 999999999);
        map[12341234] = "0x000000000022d473030f116ddee9f6b43ac78ba3";
        map[1301] = "0x000000000022d473030f116ddee9f6b43ac78ba3";
        map[324] = "0x0000000000225e31D15943971F47aD3022F714Fa";
        return map;
    }

    public static readonly IReadOnlyDictionary<int, string> UniswapxOrderQuoterMapping = BuildOrderQuoterMapping();

    private static Dictionary<int, string> BuildOrderQuoterMapping()
    {
        var map = ConstructSameAddressMap("0x54539967a06Fc0E3C3ED0ee320Eb67362D13C5fF");
        map[11155111] = "0xAA6187C48096e093c37d2cF178B1e8534A6934f7";
        map[12341234] = "0xbea0901A41177811b099F787D753436b2c47690E";
        map[1] = "0xc6ef4C96Ee89e48Eff1C35545DBEED4Ad8dAC9D4";
        map[10] = "0xc6ef4C96Ee89e48Eff1C35545DBEED4Ad8dAC9D4";
        map[8453] = "0xc6ef4C96Ee89e48Eff1C35545DBEED4Ad8dAC9D4";
        map[130] = "0xc6ef4C96Ee89e48Eff1C35545DBEED4Ad8dAC9D4";
        map[42161] = "0xc6ef4C96Ee89e48Eff1C35545DBEED4Ad8dAC9D4";
        map[1301] = "0xBFE64A14130054E1C3aB09287bc69E7148471636";
        map[56] = "0x00000000a3db63Df9078cBF3dF88B4CAdD5a7F58";
        map[137] = "0x00000000a3db63Df9078cBF3dF88B4CAdD5a7F58";
        map[143] = "0x00000000a3db63Df9078cBF3dF88B4CAdD5a7F58";
        map[196] = "0x00000000a3db63Df9078cBF3dF88B4CAdD5a7F58";
        map[480] = "0x00000000a3db63Df9078cBF3dF88B4CAdD5a7F58";
        map[1868] = "0x00000000a3db63Df9078cBF3dF88B4CAdD5a7F58";
        map[4217] = "0x00000000a3db63Df9078cBF3dF88B4CAdD5a7F58";
        map[4663] = "0x00000000a3db63Df9078cBF3dF88B4CAdD5a7F58";
        map[5042] = "0x00000000a3db63Df9078cBF3dF88B4CAdD5a7F58";
        map[42220] = "0x00000000a3db63Df9078cBF3dF88B4CAdD5a7F58";
        map[43114] = "0x00000000a3db63Df9078cBF3dF88B4CAdD5a7F58";
        map[81457] = "0x00000000a3db63Df9078cBF3dF88B4CAdD5a7F58";
        map[7777777] = "0x00000000a3db63Df9078cBF3dF88B4CAdD5a7F58";
        return map;
    }

    public static readonly IReadOnlyDictionary<int, string> UniswapxV4OrderQuoterMapping = BuildV4OrderQuoterMapping();

    private static Dictionary<int, string> BuildV4OrderQuoterMapping()
    {
        var map = ConstructSameAddressMap("0x0000000000000000000000000000000000000000");
        map[1301] = "0x8166d8286Ec24E1D17A054088B2a71470527BFf8";
        return map;
    }

    public static readonly IReadOnlyDictionary<int, string> UniswapxV4TokenTransferHookMapping = BuildV4TokenTransferHookMapping();

    private static Dictionary<int, string> BuildV4TokenTransferHookMapping()
    {
        var map = ConstructSameAddressMap("0x0000000000000000000000000000000000000000");
        map[1301] = "0xBc879Fa59f5F99eb7C3FA0F87c41457773C4adB3";
        return map;
    }

    public static readonly IReadOnlyDictionary<int, string> ExclusiveFillerValidationMapping = BuildExclusiveFillerValidationMapping();

    private static Dictionary<int, string> BuildExclusiveFillerValidationMapping()
    {
        var map = ConstructSameAddressMap("0x8A66A74e15544db9688B68B06E116f5d19e5dF90");
        map[5] = "0x0000000000000000000000000000000000000000";
        map[11155111] = "0x0000000000000000000000000000000000000000";
        map[42161] = "0x0000000000000000000000000000000000000000";
        map[12341234] = "0x8A66A74e15544db9688B68B06E116f5d19e5dF90";
        map[10] = "0x0000000000000000000000000000000000000000";
        map[56] = "0x0000000000000000000000000000000000000000";
        map[143] = "0x0000000000000000000000000000000000000000";
        map[196] = "0x0000000000000000000000000000000000000000";
        map[480] = "0x0000000000000000000000000000000000000000";
        map[1868] = "0x0000000000000000000000000000000000000000";
        map[4217] = "0x0000000000000000000000000000000000000000";
        map[4663] = "0x0000000000000000000000000000000000000000";
        map[5042] = "0x0000000000000000000000000000000000000000";
        map[42220] = "0x0000000000000000000000000000000000000000";
        map[43114] = "0x0000000000000000000000000000000000000000";
        map[81457] = "0x0000000000000000000000000000000000000000";
        map[7777777] = "0x0000000000000000000000000000000000000000";
        return map;
    }

    /// <summary>The ERC20 <c>Transfer</c> event topic (uniswapx-sdk <c>KNOWN_EVENT_SIGNATURES.ERC20_TRANSFER</c>).</summary>
    public const string Erc20TransferEventSignature = "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef";

    public static readonly IReadOnlyDictionary<int, IReadOnlyDictionary<OrderType, string>> ReactorAddressMapping = BuildReactorAddressMapping();

    private static Dictionary<int, IReadOnlyDictionary<OrderType, string>> BuildReactorAddressMapping()
    {
        static IReadOnlyDictionary<OrderType, string> Reactors(params (OrderType, string)[] entries)
        {
            var d = new Dictionary<OrderType, string>();
            foreach (var (k, v) in entries)
            {
                d[k] = v;
            }
            return d;
        }

        var defaults = Reactors(
            (OrderType.Dutch, "0x6000da47483062A0D734Ba3dc7576Ce6A0B645C4"),
            (OrderType.Dutch_V2, "0x0000000000000000000000000000000000000000"),
            (OrderType.Relay, "0x0000000000A4e21E2597DCac987455c48b12edBF"));

        var map = new Dictionary<int, IReadOnlyDictionary<OrderType, string>>();
        foreach (var chainId in NetworksWithSameAddress)
        {
            map[chainId] = defaults;
        }

        map[1] = Reactors(
            (OrderType.Dutch, "0x6000da47483062A0D734Ba3dc7576Ce6A0B645C4"),
            (OrderType.Dutch_V2, "0x00000011F84B9aa48e5f8aA8B9897600006289Be"),
            (OrderType.Priority, "0x0000000000000000000000000000000000000000"),
            (OrderType.Relay, "0x0000000000A4e21E2597DCac987455c48b12edBF"),
            (OrderType.Dutch_V3, "0x0000000015757c461808EA25Eb309638B62681cf"));
        map[12341234] = Reactors(
            (OrderType.Dutch, "0xbD7F9D0239f81C94b728d827a87b9864972661eC"),
            (OrderType.Dutch_V2, "0x0000000000000000000000000000000000000000"),
            (OrderType.Relay, "0x0000000000A4e21E2597DCac987455c48b12edBF"));
        map[11155111] = Reactors(
            (OrderType.Dutch_V2, "0x0e22B6638161A89533940Db590E67A52474bEBcd"),
            (OrderType.Dutch, "0xD6c073F2A3b676B8f9002b276B618e0d8bA84Fad"),
            (OrderType.Relay, "0x0000000000A4e21E2597DCac987455c48b12edBF"));
        map[42161] = Reactors(
            (OrderType.Dutch_V2, "0x1bd1aAdc9E230626C44a139d7E70d842749351eb"),
            (OrderType.Dutch, "0x0000000000000000000000000000000000000000"),
            (OrderType.Relay, "0x0000000000000000000000000000000000000000"),
            (OrderType.Dutch_V3, "0xB274d5F4b833b61B340b654d600A864fB604a87c"));
        map[8453] = Reactors(
            (OrderType.Dutch, "0x0000000000000000000000000000000000000000"),
            (OrderType.Dutch_V2, "0x0000000000000000000000000000000000000000"),
            (OrderType.Relay, "0x0000000000000000000000000000000000000000"),
            (OrderType.Priority, "0x000000001Ec5656dcdB24D90DFa42742738De729"),
            (OrderType.Dutch_V3, "0x000000008a8330B5d1F43A62Bf4C673A49f27ba0"));
        map[130] = Reactors(
            (OrderType.Dutch, "0x0000000000000000000000000000000000000000"),
            (OrderType.Dutch_V2, "0x0000000000000000000000000000000000000000"),
            (OrderType.Relay, "0x0000000000000000000000000000000000000000"),
            (OrderType.Priority, "0x00000006021a6Bce796be7ba509BBBA71e956e37"),
            (OrderType.Dutch_V3, "0x000000005aF66799D1a6317714D66800f9CA1406"));
        map[1301] = Reactors(
            (OrderType.Hybrid, "0x000000000C75276D956cc35218ca8f132D877957"),
            (OrderType.Dutch, "0x0000000000000000000000000000000000000000"),
            (OrderType.Dutch_V2, "0x0000000000000000000000000000000000000000"),
            (OrderType.Relay, "0x0000000000000000000000000000000000000000"),
            (OrderType.Priority, "0x0000000000000000000000000000000000000000"));
        map[10] = Reactors((OrderType.Dutch_V3, "0x000000000923439A92daE8930613568824108631"));
        map[56] = Reactors((OrderType.Dutch_V3, "0x00000000a55e50C71b70Db3C8B58749cd1E18eB2"));
        map[137] = Reactors(
            (OrderType.Dutch, "0x6000da47483062A0D734Ba3dc7576Ce6A0B645C4"),
            (OrderType.Dutch_V2, "0x0000000000000000000000000000000000000000"),
            (OrderType.Relay, "0x0000000000A4e21E2597DCac987455c48b12edBF"),
            (OrderType.Dutch_V3, "0x00000000bAB6E234db8AD638B6A6395b7c499Bc4"));
        map[143] = Reactors((OrderType.Dutch_V3, "0x000000000Ac008F7e07210CFb6648e40249232c2"));
        map[196] = Reactors((OrderType.Dutch_V3, "0x000000005aF66799D1a6317714D66800f9CA1406"));
        map[480] = Reactors((OrderType.Dutch_V3, "0x00000000d714EA34028930b762E96bFBe50F42C2"));
        map[1868] = Reactors((OrderType.Dutch_V3, "0x000000005aF66799D1a6317714D66800f9CA1406"));
        map[4217] = Reactors((OrderType.Dutch_V3, "0x00000000fc1E66C9f582566EAd00108e55F1c0C6"));
        map[4663] = Reactors((OrderType.Dutch_V3, "0x000000007A1C8e570011EeDF86A2A35593013cBA"));
        map[5042] = Reactors((OrderType.Dutch_V3, "0x0000000015134054eA82AE0bb9fda66b36402C36"));
        map[42220] = Reactors((OrderType.Dutch_V3, "0x00000000B8077fdf2281A80bE96f6c282B5d943A"));
        map[43114] = Reactors((OrderType.Dutch_V3, "0x00000000862cCF095823fc7576Fa6C7e6b7385ef"));
        map[81457] = Reactors((OrderType.Dutch_V3, "0x0000000086f50C5E1a2500602183D4390A7FFc98"));
        map[7777777] = Reactors((OrderType.Dutch_V3, "0x000000002C9A3812e15cf233190992E9a57EDB56"));
        return map;
    }

    /// <summary>Alias for <see cref="ReactorAddressMapping"/> (uniswapx-sdk <c>REACTOR_CONTRACT_MAPPING</c>).</summary>
    public static readonly IReadOnlyDictionary<int, IReadOnlyDictionary<OrderType, string>> ReactorContractMapping = ReactorAddressMapping;

    /// <summary>The mds1 multicall3 address for the given chain (zkSync = 324 differs).</summary>
    public static string MulticallAddressOn(int chainId = 1) => chainId switch
    {
        324 => "0xF9cda624FBC7e059355ce98a31693d299FACd963",
        _ => "0xcA11bde05977b3631167028862bE2a173976CA11",
    };

    public const string RelaySentinelRecipient = "0x0000000000000000000000000000000000000000";

    /// <summary>Reverse lookup from a lower-cased reactor address to its order type. Mutable to mirror the JS object the tests patch.</summary>
    public static readonly Dictionary<string, OrderTypeMapping> ReverseReactorMapping = BuildReverseReactorMapping();

    private static Dictionary<string, OrderTypeMapping> BuildReverseReactorMapping()
    {
        var acc = new Dictionary<string, OrderTypeMapping>();
        // JS iterates integer-like object keys in ascending numeric order.
        foreach (var chainId in ReactorAddressMapping.Keys.OrderBy(k => k))
        {
            foreach (var (orderType, reactorAddress) in ReactorAddressMapping[chainId])
            {
                acc[reactorAddress.ToLowerInvariant()] = new OrderTypeMapping(orderType);
            }
        }
        return acc;
    }

    public const int Bps = 10000;

    public static readonly BigInteger Mps = BigInteger.Pow(10, 7);

    public static readonly IReadOnlyList<PermissionedToken> PermissionedTokens = new List<PermissionedToken>
    {
        new(
            "0x7712c34205737192402172409a8F7ccef8aA2AEc",
            1,
            "BUIDL",
            PermissionedTokenInterface.DSTokenInterface,
            PermissionedTokenProxyType.None),
        new(
            "0x14d60E7FDC0D71d8611742720E4C50E7a974020c",
            1,
            "USCC",
            PermissionedTokenInterface.ISuperstateTokenV4,
            PermissionedTokenProxyType.ERC1967),
    };

    public static readonly IReadOnlyDictionary<int, string> HybridResolverAddressMapping = new Dictionary<int, string>
    {
        [1301] = "0x57c48a70bd9f34fd902dde5bb4dbe25d2c931c62",
    };

    /// <summary>Reverse lookup from a lower-cased resolver address to its order type. Mutable to mirror the JS object the tests patch.</summary>
    public static readonly Dictionary<string, OrderTypeMapping> ReverseResolverMapping = BuildReverseResolverMapping();

    private static Dictionary<string, OrderTypeMapping> BuildReverseResolverMapping()
    {
        var acc = new Dictionary<string, OrderTypeMapping>();
        foreach (var resolverAddress in HybridResolverAddressMapping.Values)
        {
            acc[resolverAddress.ToLowerInvariant()] = new OrderTypeMapping(OrderType.Hybrid);
        }
        return acc;
    }
}
