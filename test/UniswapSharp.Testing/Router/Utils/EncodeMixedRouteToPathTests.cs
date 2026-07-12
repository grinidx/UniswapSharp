using System.Numerics;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.Router;
using UniswapSharp.Router.Entities.MixedRoute;
using UniswapSharp.Router.Utils;
using UniswapSharp.V3.Utils;
using V2Pair = UniswapSharp.V2.Entities.Pair;
using V3Pool = UniswapSharp.V3.Entities.Pool;
using V4Pool = UniswapSharp.V4.Entities.Pool;
using Tick = UniswapSharp.V3.Entities.Tick;
using FeeAmount = UniswapSharp.V3.Constants.FeeAmount;

namespace UniswapSharp.Testing.Router.Utils;

// Ported 1:1 from sdks/router-sdk/src/utils/encodeMixedRouteToPath.test.ts
public class EncodeMixedRouteToPathTests
{
    private static readonly Ether ETHER = Ether.OnChain(1);
    private static readonly Token token0 = new(1, "0x0000000000000000000000000000000000000001", 18, "t0", "token0");
    private static readonly Token token1 = new(1, "0x0000000000000000000000000000000000000002", 18, "t1", "token1");
    private static readonly Token token2 = new(1, "0x0000000000000000000000000000000000000003", 18, "t2", "token2");
    private static readonly Token weth = Weth9.Tokens[1];

    private static BigInteger Sqrt1 => EncodeSqrtRatioX96.Encode(1, 1);
    private const string ZERO = Constants.ADDRESS_ZERO;

    private static readonly V3Pool pool_V3_0_1_medium = new(token0, token1, FeeAmount.MEDIUM, EncodeSqrtRatioX96.Encode(1, 1), 0, 0, new List<Tick>());
    private static readonly V3Pool pool_V3_1_2_low = new(token1, token2, FeeAmount.LOW, EncodeSqrtRatioX96.Encode(1, 1), 0, 0, new List<Tick>());
    private static readonly V3Pool pool_V3_0_weth = new(token0, weth, FeeAmount.MEDIUM, EncodeSqrtRatioX96.Encode(1, 1), 0, 0, new List<Tick>());
    private static readonly V3Pool pool_V3_1_weth = new(token1, weth, FeeAmount.MEDIUM, EncodeSqrtRatioX96.Encode(1, 1), 0, 0, new List<Tick>());

    private static readonly V4Pool pool_V4_0_1 = new(token0, token1, 3000, 30, ZERO, EncodeSqrtRatioX96.Encode(1, 1), 0, 0, new List<Tick>());
    private static readonly V4Pool pool_V4_0_eth = new(token0, ETHER, 3000, 30, ZERO, EncodeSqrtRatioX96.Encode(1, 1), 0, 0, new List<Tick>());
    private static readonly V4Pool fake_v4_eth_weth_pool = new(weth, ETHER, 3000, 0, ZERO, EncodeSqrtRatioX96.Encode(1, 1), 0, 0);

    private static V2Pair Pair(Token a, int aAmt, Token b, int bAmt) =>
        new(CurrencyAmount<Token>.FromRawAmount(a, aAmt), CurrencyAmount<Token>.FromRawAmount(b, bAmt));

    private static readonly V2Pair pair_0_1 = Pair(token0, 100, token1, 200);
    private static readonly V2Pair pair_1_2 = Pair(token1, 150, token2, 150);
    private static readonly V2Pair pair_0_weth = Pair(token0, 100, weth, 100);
    private static readonly V2Pair pair_1_weth = Pair(token1, 175, weth, 100);
    private static readonly V2Pair pair_2_weth = Pair(token2, 150, weth, 100);

    private static MixedRouteSDK<BaseCurrency, BaseCurrency> Route(IEnumerable<object> pools, BaseCurrency input, BaseCurrency output, bool retain = false) =>
        new(pools.ToList(), input, output, retain);

    private static readonly MixedRouteSDK<BaseCurrency, BaseCurrency> route_0_V3_1 = Route(new object[] { pool_V3_0_1_medium }, token0, token1);
    private static readonly MixedRouteSDK<BaseCurrency, BaseCurrency> route_0_V3_1_V3_2 = Route(new object[] { pool_V3_0_1_medium, pool_V3_1_2_low }, token0, token2);
    private static readonly MixedRouteSDK<BaseCurrency, BaseCurrency> route_0_V3_weth = Route(new object[] { pool_V3_0_weth }, token0, ETHER);
    private static readonly MixedRouteSDK<BaseCurrency, BaseCurrency> route_0_V3_1_V3_weth = Route(new object[] { pool_V3_0_1_medium, pool_V3_1_weth }, token0, ETHER);
    private static readonly MixedRouteSDK<BaseCurrency, BaseCurrency> route_weth_V3_0 = Route(new object[] { pool_V3_0_weth }, ETHER, token0);
    private static readonly MixedRouteSDK<BaseCurrency, BaseCurrency> route_weth_V3_0_V3_1 = Route(new object[] { pool_V3_0_weth, pool_V3_0_1_medium }, ETHER, token1);

    private static readonly MixedRouteSDK<BaseCurrency, BaseCurrency> route_0_V4_1 = Route(new object[] { pool_V4_0_1 }, token0, token1);

    private static readonly MixedRouteSDK<BaseCurrency, BaseCurrency> route_0_V2_1 = Route(new object[] { pair_0_1 }, token0, token1);
    private static readonly MixedRouteSDK<BaseCurrency, BaseCurrency> route_0_V2_1_V2_2 = Route(new object[] { pair_0_1, pair_1_2 }, token0, token2);
    private static readonly MixedRouteSDK<BaseCurrency, BaseCurrency> route_weth_V2_0 = Route(new object[] { pair_0_weth }, ETHER, token0);
    private static readonly MixedRouteSDK<BaseCurrency, BaseCurrency> route_weth_V2_0_V2_1 = Route(new object[] { pair_0_weth, pair_0_1 }, ETHER, token1);
    private static readonly MixedRouteSDK<BaseCurrency, BaseCurrency> route_0_V2_weth = Route(new object[] { pair_0_weth }, token0, ETHER);
    private static readonly MixedRouteSDK<BaseCurrency, BaseCurrency> route_0_V2_1_V2_weth = Route(new object[] { pair_0_1, pair_1_weth }, token0, ETHER);

    private static readonly MixedRouteSDK<BaseCurrency, BaseCurrency> route_0_V3_1_V2_weth = Route(new object[] { pool_V3_0_1_medium, pair_1_weth }, token0, ETHER);
    private static readonly MixedRouteSDK<BaseCurrency, BaseCurrency> route_0_V3_weth_V2_1_V2_2 = Route(new object[] { pool_V3_0_weth, pair_1_weth, pair_1_2 }, token0, token2);
    private static readonly MixedRouteSDK<BaseCurrency, BaseCurrency> route_0_V3_1_v3_weth_V2_2 = Route(new object[] { pool_V3_0_1_medium, pool_V3_1_weth, pair_2_weth }, token0, token2);
    private static readonly MixedRouteSDK<BaseCurrency, BaseCurrency> route_0_V3_weth_V4_1 = Route(new object[] { pool_V3_0_weth, pool_V4_0_1 }, ETHER, token1);
    private static readonly MixedRouteSDK<BaseCurrency, BaseCurrency> route_eth_V4_0_V3_1 = Route(new object[] { pool_V4_0_eth, pool_V3_0_1_medium }, ETHER, token1);
    private static readonly MixedRouteSDK<BaseCurrency, BaseCurrency> route_eth_V3_0_V4_1 = Route(new object[] { pool_V3_0_weth, pool_V4_0_1 }, ETHER, token1);

    private static readonly MixedRouteSDK<BaseCurrency, BaseCurrency> route_1_v2_weth_v0_eth_v4_token0 =
        Route(new object[] { pair_1_weth, fake_v4_eth_weth_pool, pool_V4_0_eth }, token1, token0, true);

    private static string Encode(MixedRouteSDK<BaseCurrency, BaseCurrency> route) => EncodeMixedRouteToPath.Encode(route);

    // ---- pure V3 ----
    [Fact]
    public void PureV3_ExactInputSingleHop() =>
        Assert.Equal("0x0000000000000000000000000000000000000001000bb80000000000000000000000000000000000000002", Encode(route_0_V3_1));

    [Fact]
    public void PureV3_MultihopExactInput() =>
        Assert.Equal("0x0000000000000000000000000000000000000001000bb800000000000000000000000000000000000000020001f40000000000000000000000000000000000000003", Encode(route_0_V3_1_V3_2));

    [Fact]
    public void PureV3_WrapEtherInputSingleHop() =>
        Assert.Equal("0xc02aaa39b223fe8d0a0e5c4f27ead9083c756cc2000bb80000000000000000000000000000000000000001", Encode(route_weth_V3_0));

    [Fact]
    public void PureV3_WrapEtherInputMultihop() =>
        Assert.Equal("0xc02aaa39b223fe8d0a0e5c4f27ead9083c756cc2000bb80000000000000000000000000000000000000001000bb80000000000000000000000000000000000000002", Encode(route_weth_V3_0_V3_1));

    [Fact]
    public void PureV3_WrapEtherOutputSingleHop() =>
        Assert.Equal("0x0000000000000000000000000000000000000001000bb8c02aaa39b223fe8d0a0e5c4f27ead9083c756cc2", Encode(route_0_V3_weth));

    [Fact]
    public void PureV3_WrapEtherOutputMultihop() =>
        Assert.Equal("0x0000000000000000000000000000000000000001000bb80000000000000000000000000000000000000002000bb8c02aaa39b223fe8d0a0e5c4f27ead9083c756cc2", Encode(route_0_V3_1_V3_weth));

    // ---- pure v4 ----
    [Fact]
    public void PureV4_ExactInputSingleHop() =>
        Assert.Equal("0x0000000000000000000000000000000000000001400bb800001e00000000000000000000000000000000000000000000000000000000000000000000000000000002", Encode(route_0_V4_1));

    // ---- pure V2 ----
    [Fact]
    public void PureV2_ExactInputSingleHop() =>
        Assert.Equal("0x00000000000000000000000000000000000000018000000000000000000000000000000000000000000002", Encode(route_0_V2_1));

    [Fact]
    public void PureV2_MultihopExactInput() =>
        Assert.Equal("0x000000000000000000000000000000000000000180000000000000000000000000000000000000000000028000000000000000000000000000000000000000000003", Encode(route_0_V2_1_V2_2));

    [Fact]
    public void PureV2_WrapEtherInputSingleHop() =>
        Assert.Equal("0xc02aaa39b223fe8d0a0e5c4f27ead9083c756cc28000000000000000000000000000000000000000000001", Encode(route_weth_V2_0));

    [Fact]
    public void PureV2_WrapEtherInputMultihop() =>
        Assert.Equal("0xc02aaa39b223fe8d0a0e5c4f27ead9083c756cc280000000000000000000000000000000000000000000018000000000000000000000000000000000000000000002", Encode(route_weth_V2_0_V2_1));

    [Fact]
    public void PureV2_WrapEtherOutputSingleHop() =>
        Assert.Equal("0x0000000000000000000000000000000000000001800000c02aaa39b223fe8d0a0e5c4f27ead9083c756cc2", Encode(route_0_V2_weth));

    [Fact]
    public void PureV2_WrapEtherOutputMultihop() =>
        Assert.Equal("0x00000000000000000000000000000000000000018000000000000000000000000000000000000000000002800000c02aaa39b223fe8d0a0e5c4f27ead9083c756cc2", Encode(route_0_V2_1_V2_weth));

    // ---- mixed route ----
    [Fact]
    public void Mixed_V3_V2_WrappedEtherOutput() =>
        Assert.Equal("0x0000000000000000000000000000000000000001000bb80000000000000000000000000000000000000002800000c02aaa39b223fe8d0a0e5c4f27ead9083c756cc2", Encode(route_0_V3_1_V2_weth));

    [Fact]
    public void Mixed_V3_V2_V2() =>
        Assert.Equal("0x0000000000000000000000000000000000000001000bb8c02aaa39b223fe8d0a0e5c4f27ead9083c756cc280000000000000000000000000000000000000000000028000000000000000000000000000000000000000000003", Encode(route_0_V3_weth_V2_1_V2_2));

    [Fact]
    public void Mixed_V3_V3_V2() =>
        Assert.Equal("0x0000000000000000000000000000000000000001000bb80000000000000000000000000000000000000002000bb8c02aaa39b223fe8d0a0e5c4f27ead9083c756cc28000000000000000000000000000000000000000000003", Encode(route_0_V3_1_v3_weth_V2_2));

    [Fact]
    public void Mixed_V3_V4() =>
        Assert.Equal("0xc02aaa39b223fe8d0a0e5c4f27ead9083c756cc2300bb80000000000000000000000000000000000000001400bb800001e00000000000000000000000000000000000000000000000000000000000000000000000000000002", Encode(route_0_V3_weth_V4_1));

    [Fact]
    public void Mixed_NativeEth_V4_V3() =>
        Assert.Equal("0x0000000000000000000000000000000000000000400bb800001e00000000000000000000000000000000000000000000000000000000000000000000000000000001300bb80000000000000000000000000000000000000002", Encode(route_eth_V4_0_V3_1));

    [Fact]
    public void Mixed_NativeEth_V3_V4() =>
        Assert.Equal("0xc02aaa39b223fe8d0a0e5c4f27ead9083c756cc2300bb80000000000000000000000000000000000000001400bb800001e00000000000000000000000000000000000000000000000000000000000000000000000000000002", Encode(route_eth_V3_0_V4_1));

    [Fact]
    public void Mixed_UnwrapToken1_V2_V4_Token0() =>
        Assert.Equal("0x000000000000000000000000000000000000000220c02aaa39b223fe8d0a0e5c4f27ead9083c756cc2000000000000000000000000000000000000000000400bb800001e00000000000000000000000000000000000000000000000000000000000000000000000000000001", Encode(route_1_v2_weth_v0_eth_v4_token0));
}
