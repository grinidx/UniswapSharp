using System.Numerics;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.Router;
using UniswapSharp.Router.Entities;
using UniswapSharp.Router.Entities.MixedRoute;
using UniswapSharp.Router.Utils;
using UniswapSharp.V3.Utils;
using V2Pair = UniswapSharp.V2.Entities.Pair;
using V3Pool = UniswapSharp.V3.Entities.Pool;
using V3Route = UniswapSharp.V3.Entities.Route<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;
using V4Pool = UniswapSharp.V4.Entities.Pool;
using Tick = UniswapSharp.V3.Entities.Tick;
using FeeAmount = UniswapSharp.V3.Constants.FeeAmount;

namespace UniswapSharp.Testing.Router.Entities.MixedRoute;

// Ported 1:1 from sdks/router-sdk/src/entities/mixedRoute/route.test.ts
public class MixedRouteTests
{
    private static readonly Ether ETHER = Ether.OnChain(1);
    private static readonly Token token0 = new(1, "0x0000000000000000000000000000000000000001", 18, "t0");
    private static readonly Token token1 = new(1, "0x0000000000000000000000000000000000000002", 18, "t1");
    private static readonly Token token2 = new(1, "0x0000000000000000000000000000000000000003", 18, "t2");
    private static readonly Token token3 = new(1, "0x0000000000000000000000000000000000000004", 18, "t3");
    private static readonly Token weth = Weth9.Tokens[1];

    private static BigInteger S(int a, int b) => EncodeSqrtRatioX96.Encode(a, b);
    private const string ZERO = Constants.ADDRESS_ZERO;
    private static List<Tick> NoTicks() => new();

    private static readonly V4Pool pool_v4_0_weth = new(token0, weth, 3000, 60, ZERO, S(1, 1), 0, 0, NoTicks());
    private static readonly V4Pool pool_v4_1_eth = new(token1, ETHER, 3000, 60, ZERO, S(1, 1), 0, 0, NoTicks());
    private static readonly V4Pool pool_v4_0_1 = new(token0, token1, 3000, 60, ZERO, S(1, 1), 0, 0, NoTicks());
    private static readonly V4Pool pool_v4_weth_eth = new(weth, ETHER, 0, 0, ZERO, S(1, 1), 0, 0);

    private static readonly V3Pool pool_v3_0_1 = new(token0, token1, FeeAmount.MEDIUM, S(1, 1), 0, 0, NoTicks());
    private static readonly V3Pool pool_v3_0_weth = new(token0, weth, FeeAmount.MEDIUM, S(1, 1), 0, 0, NoTicks());
    private static readonly V3Pool pool_v3_1_weth = new(token1, weth, FeeAmount.MEDIUM, S(1, 1), 0, 0, NoTicks());
    private static readonly V3Pool pool_v3_2_weth = new(token2, weth, FeeAmount.MEDIUM, S(1, 1), 0, 0, NoTicks());
    private static readonly V3Pool pool_v3_2_3 = new(token2, token3, FeeAmount.MEDIUM, S(1, 1), 0, 0, NoTicks());

    private static V2Pair Pair(Token a, int aAmt, Token b, int bAmt) =>
        new(CurrencyAmount<Token>.FromRawAmount(a, aAmt), CurrencyAmount<Token>.FromRawAmount(b, bAmt));

    private static readonly V2Pair pair_0_1 = Pair(token0, 100, token1, 200);
    private static readonly V2Pair pair_0_weth = Pair(token0, 100, weth, 100);
    private static readonly V2Pair pair_1_weth = Pair(token1, 175, weth, 100);
    private static readonly V2Pair pair_weth_2 = Pair(weth, 200, token2, 150);
    private static readonly V2Pair pair_2_3 = Pair(token2, 100, token3, 200);

    private static MixedRouteSDK<BaseCurrency, BaseCurrency> Route(IEnumerable<object> pools, BaseCurrency input, BaseCurrency output, bool retain = false) =>
        new(pools.ToList(), input, output, retain);

    private static void AssertPools(object[] expected, List<object> actual)
    {
        Assert.Equal(expected.Length, actual.Count);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Same(expected[i], actual[i]);
        }
    }

    private static void AssertPath(BaseCurrency[] expected, List<BaseCurrency> actual)
    {
        Assert.Equal(expected.Length, actual.Count);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.True(actual[i].Equals(expected[i]), $"path[{i}] mismatch");
        }
    }

    // ---- path ----
    [Fact]
    public void Path_RealV3WethPool_FakeV4EthWethPool()
    {
        var route = Route(new object[] { pool_v3_0_weth, pool_v4_weth_eth }, token0, ETHER);
        AssertPath(new BaseCurrency[] { token0, weth }, route.Path);
        AssertPools(new object[] { pool_v3_0_weth }, route.Pools);
    }

    [Fact]
    public void Path_RealV3WethPool_RealV4EthPool()
    {
        var route = Route(new object[] { pool_v3_0_weth, pool_v4_weth_eth, pool_v4_1_eth }, token0, token1);
        AssertPath(new BaseCurrency[] { token0, weth, token1 }, route.Path);
        AssertPools(new object[] { pool_v3_0_weth, pool_v4_1_eth }, route.Pools);
    }

    [Fact]
    public void Path_WrapsPureV3RouteObject()
    {
        var routeOriginal = Route(new object[] { pool_v3_0_1 }, token0, token1);
        var route = new MixedRoute<BaseCurrency, BaseCurrency>(routeOriginal);
        AssertPools(new object[] { pool_v3_0_1 }, route.Pools);
        AssertPath(new BaseCurrency[] { token0, token1 }, route.Path);
        Assert.True(route.Input.Equals(token0));
        Assert.True(route.Output.Equals(token1));
        Assert.Equal(1, route.ChainId);
    }

    [Fact]
    public void Path_WrapsPureV2RouteObject()
    {
        var route = Route(new object[] { pair_0_1 }, token0, token1);
        AssertPools(new object[] { pair_0_1 }, route.Pools);
        AssertPath(new BaseCurrency[] { token0, token1 }, route.Path);
        Assert.True(route.Input.Equals(token0));
        Assert.True(route.Output.Equals(token1));
        Assert.Equal(1, route.ChainId);
    }

    [Fact]
    public void Path_WrapsPureV4RouteObjectIncludingEther()
    {
        var route = Route(new object[] { pool_v4_0_1, pool_v4_1_eth }, token0, ETHER);
        AssertPools(new object[] { pool_v4_0_1, pool_v4_1_eth }, route.Pools);
        AssertPath(new BaseCurrency[] { token0, token1, ETHER }, route.Path);
        Assert.True(route.Input.Equals(token0));
        Assert.True(route.Output.Equals(ETHER));
        Assert.Equal(1, route.ChainId);
    }

    [Fact]
    public void Path_WrapsMixedRouteObject()
    {
        var route = Route(new object[] { pool_v3_0_1, pair_1_weth }, token0, weth);
        AssertPools(new object[] { pool_v3_0_1, pair_1_weth }, route.Pools);
        AssertPath(new BaseCurrency[] { token0, token1, weth }, route.Path);
        Assert.True(route.Input.Equals(token0));
        Assert.True(route.Output.Equals(weth));
        Assert.Equal(1, route.ChainId);
    }

    [Fact]
    public void Path_WrapsMixedRouteObjectWithV4()
    {
        var route = Route(new object[] { pool_v3_0_1, pool_v4_0_weth }, token1, weth);
        AssertPools(new object[] { pool_v3_0_1, pool_v4_0_weth }, route.Pools);
        AssertPath(new BaseCurrency[] { token1, token0, weth }, route.Path);
        Assert.True(route.Input.Equals(token1));
        Assert.True(route.Output.Equals(weth));
        Assert.True(route.PathInput.Equals(token1));
        Assert.True(route.PathOutput.Equals(weth));
        Assert.Equal(1, route.ChainId);
    }

    [Fact]
    public void Path_MixedV4RouteConvertsWethToEth()
    {
        var route = Route(new object[] { pool_v3_0_weth, pool_v4_weth_eth, pool_v4_1_eth }, token0, token1, true);
        AssertPools(new object[] { pool_v3_0_weth, pool_v4_weth_eth, pool_v4_1_eth }, route.Pools);
        AssertPath(new BaseCurrency[] { token0, weth, ETHER, token1 }, route.Path);
        Assert.True(route.Input.Equals(token0));
        Assert.True(route.Output.Equals(token1));
        Assert.True(route.PathInput.Equals(token0));
        Assert.True(route.PathOutput.Equals(token1));
        Assert.Equal(1, route.ChainId);
    }

    [Fact]
    public void Path_MixedV4RouteConvertsEthToWeth()
    {
        var route = Route(new object[] { pool_v4_1_eth, pool_v3_0_weth }, token1, token0);
        AssertPools(new object[] { pool_v4_1_eth, pool_v3_0_weth }, route.Pools);
        AssertPath(new BaseCurrency[] { token1, ETHER, token0 }, route.Path);
        Assert.True(route.Input.Equals(token1));
        Assert.True(route.Output.Equals(token0));
        Assert.Equal(1, route.ChainId);
    }

    [Fact]
    public void Path_PureV4RouteConvertsEthToWeth()
    {
        var route = Route(new object[] { pool_v4_1_eth, pool_v4_0_weth }, token1, token0);
        AssertPools(new object[] { pool_v4_1_eth, pool_v4_0_weth }, route.Pools);
        AssertPath(new BaseCurrency[] { token1, ETHER, token0 }, route.Path);
        Assert.True(route.Input.Equals(token1));
        Assert.True(route.Output.Equals(token0));
        Assert.Equal(1, route.ChainId);
    }

    [Fact]
    public void Path_PureV4RouteConvertsWethToEth()
    {
        var route = Route(new object[] { pool_v4_0_weth, pool_v4_1_eth }, token0, token1);
        AssertPools(new object[] { pool_v4_0_weth, pool_v4_1_eth }, route.Pools);
        AssertPath(new BaseCurrency[] { token0, weth, token1 }, route.Path);
        Assert.True(route.Input.Equals(token0));
        Assert.True(route.Output.Equals(token1));
        Assert.Equal(1, route.ChainId);
    }

    [Fact]
    public void Path_ComplexMixedRoute()
    {
        var route = Route(new object[] { pool_v3_0_1, pair_1_weth, pair_weth_2 }, token0, token2);
        AssertPools(new object[] { pool_v3_0_1, pair_1_weth, pair_weth_2 }, route.Pools);
        AssertPath(new BaseCurrency[] { token0, token1, weth, token2 }, route.Path);
        Assert.True(route.Input.Equals(token0));
        Assert.True(route.Output.Equals(token2));
        Assert.Equal(1, route.ChainId);
    }

    [Fact]
    public void Path_ComplexMixedRoute_MultihopV3Beginning()
    {
        var route = Route(new object[] { pool_v3_0_1, pool_v3_1_weth, pair_weth_2 }, token0, token2);
        AssertPools(new object[] { pool_v3_0_1, pool_v3_1_weth, pair_weth_2 }, route.Pools);
        AssertPath(new BaseCurrency[] { token0, token1, weth, token2 }, route.Path);
        Assert.True(route.Input.Equals(token0));
        Assert.True(route.Output.Equals(token2));
        Assert.Equal(1, route.ChainId);
    }

    [Fact]
    public void Path_ComplexMixedRoute_UnwrapsWethToEthAtEnd()
    {
        var route = Route(new object[] { pool_v3_0_1, pool_v3_1_weth }, token0, ETHER);
        AssertPools(new object[] { pool_v3_0_1, pool_v3_1_weth }, route.Pools);
        AssertPath(new BaseCurrency[] { token0, token1, weth }, route.Path);
        Assert.True(route.Input.Equals(token0));
        Assert.True(route.Output.Equals(ETHER));
        Assert.True(route.PathInput.Equals(token0));
        Assert.True(route.PathOutput.Equals(weth));
        Assert.Equal(1, route.ChainId);
    }

    [Fact]
    public void Path_ComplexMixedRoute_MultihopV2Beginning()
    {
        var route = Route(new object[] { pair_0_1, pair_1_weth, pool_v3_2_weth }, token0, token2);
        AssertPools(new object[] { pair_0_1, pair_1_weth, pool_v3_2_weth }, route.Pools);
        AssertPath(new BaseCurrency[] { token0, token1, weth, token2 }, route.Path);
        Assert.True(route.Input.Equals(token0));
        Assert.True(route.Output.Equals(token2));
        Assert.Equal(1, route.ChainId);
    }

    [Fact]
    public void Path_ComplexMixedRoute_ConsecutiveV3Middle()
    {
        var route = Route(new object[] { pair_0_1, pool_v3_1_weth, pool_v3_2_weth, pair_2_3 }, token0, token3);
        AssertPools(new object[] { pair_0_1, pool_v3_1_weth, pool_v3_2_weth, pair_2_3 }, route.Pools);
        AssertPath(new BaseCurrency[] { token0, token1, weth, token2, token3 }, route.Path);
        Assert.True(route.Input.Equals(token0));
        Assert.True(route.Output.Equals(token3));
        Assert.Equal(1, route.ChainId);
    }

    [Fact]
    public void Path_ComplexMixedRoute_ConsecutiveV2Middle()
    {
        var route = Route(new object[] { pool_v3_0_1, pair_1_weth, pair_weth_2, pool_v3_2_3 }, token0, token3);
        AssertPools(new object[] { pool_v3_0_1, pair_1_weth, pair_weth_2, pool_v3_2_3 }, route.Pools);
        AssertPath(new BaseCurrency[] { token0, token1, weth, token2, token3 }, route.Path);
        Assert.True(route.Input.Equals(token0));
        Assert.True(route.Output.Equals(token3));
        Assert.Equal(1, route.ChainId);
    }

    [Fact]
    public void CanHaveTokenAsBothInputAndOutput()
    {
        var route = Route(new object[] { pair_0_weth, pair_0_1, pair_1_weth }, weth, weth);
        AssertPools(new object[] { pair_0_weth, pair_0_1, pair_1_weth }, route.Pools);
        Assert.True(route.Input.Equals(weth));
        Assert.True(route.Output.Equals(weth));
    }

    // ---- backwards compatible with a 100% V3 route ----
    private static V3Route MakeV3Route(IEnumerable<V3Pool> pools, BaseCurrency input, BaseCurrency output) =>
        new(pools.ToList(), input, output);

    [Fact]
    public void V3Compat_AssignsProtocol()
    {
        var route = new RouteV3<BaseCurrency, BaseCurrency>(MakeV3Route(new[] { pool_v3_0_1 }, token0, token1));
        Assert.Equal(Protocol.V3, route.Protocol);
    }

    [Fact]
    public void V3Compat_InheritsParameters()
    {
        var original = MakeV3Route(new[] { pool_v3_0_1 }, token0, token1);
        var route = new RouteV3<BaseCurrency, BaseCurrency>(original);
        AssertPools(original.Pools.Cast<object>().ToArray(), route.Pools);
        AssertPath(original.TokenPath.Cast<BaseCurrency>().ToArray(), route.Path);
        Assert.True(route.Input.Equals(original.Input));
        Assert.True(route.Output.Equals(original.Output));
        Assert.True(route.MidPrice.Equals(original.MidPrice));
        Assert.Equal(original.ChainId, route.ChainId);
    }

    [Fact]
    public void V3Compat_TokenAsBothInputAndOutput()
    {
        var original = MakeV3Route(new[] { pool_v3_0_weth, pool_v3_0_1, pool_v3_1_weth }, weth, weth);
        var route = new RouteV3<BaseCurrency, BaseCurrency>(original);
        AssertPools(new object[] { pool_v3_0_weth, pool_v3_0_1, pool_v3_1_weth }, route.Pools);
        Assert.True(route.Input.Equals(weth));
        Assert.True(route.Output.Equals(weth));
    }

    [Fact]
    public void V3Compat_SupportsEtherInput()
    {
        var route = new RouteV3<BaseCurrency, BaseCurrency>(MakeV3Route(new[] { pool_v3_0_weth }, ETHER, token0));
        AssertPools(new object[] { pool_v3_0_weth }, route.Pools);
        Assert.True(route.Input.Equals(ETHER));
        Assert.True(route.Output.Equals(token0));
    }

    [Fact]
    public void V3Compat_SupportsEtherOutput()
    {
        var route = new RouteV3<BaseCurrency, BaseCurrency>(MakeV3Route(new[] { pool_v3_0_weth }, token0, ETHER));
        AssertPools(new object[] { pool_v3_0_weth }, route.Pools);
        Assert.True(route.Input.Equals(token0));
        Assert.True(route.Output.Equals(ETHER));
    }

    // ---- #midPrice ----
    private static readonly V3Pool mp_v3_0_1 = new(token0, token1, FeeAmount.MEDIUM, S(1, 5), 0, TickMath.GetTickAtSqrtRatio(S(1, 5)), NoTicks());
    private static readonly V3Pool mp_v3_1_2 = new(token1, token2, FeeAmount.MEDIUM, S(15, 30), 0, TickMath.GetTickAtSqrtRatio(S(15, 30)), NoTicks());
    private static readonly V3Pool mp_v3_0_weth = new(token0, weth, FeeAmount.MEDIUM, S(3, 1), 0, TickMath.GetTickAtSqrtRatio(S(3, 1)), NoTicks());
    private static readonly V3Pool mp_v3_1_weth = new(token1, weth, FeeAmount.MEDIUM, S(1, 7), 0, TickMath.GetTickAtSqrtRatio(S(1, 7)), NoTicks());
    private static readonly V3Pool mp_v3_2_weth = new(token2, weth, FeeAmount.MEDIUM, S(1, 8), 0, TickMath.GetTickAtSqrtRatio(S(1, 8)), NoTicks());

    private static readonly V2Pair mp_pair_0_1 = Pair(token0, 100, token1, 200);
    private static readonly V2Pair mp_pair_1_2 = Pair(token1, 200, token2, 150);
    private static readonly V2Pair mp_pair_0_2 = Pair(token0, 200, token2, 150);
    private static readonly V2Pair mp_pair_0_weth = Pair(token0, 100, weth, 100);
    private static readonly V2Pair mp_pair_1_weth = Pair(token1, 175, weth, 100);

    [Fact]
    public void MidPrice_V3_0to1()
    {
        var v3 = MakeV3Route(new[] { mp_v3_0_1 }, token0, token1);
        var route = Route(new object[] { mp_v3_0_1 }, token0, token1);
        Assert.Equal(v3.MidPrice.ToFixed(4), route.MidPrice.ToFixed(4));
        Assert.Equal("0.2000", route.MidPrice.ToFixed(4));
        Assert.True(route.MidPrice.BaseCurrency.Equals(token0));
        Assert.True(route.MidPrice.QuoteCurrency.Equals(token1));
    }

    [Fact]
    public void MidPrice_IsCached()
    {
        var route = new MixedRoute<BaseCurrency, BaseCurrency>(Route(new object[] { mp_v3_0_1 }, token0, token1));
        Assert.Same(route.MidPrice, route.MidPrice);
    }

    [Fact]
    public void MidPrice_V3_1to0()
    {
        var v3 = MakeV3Route(new[] { mp_v3_0_1 }, token1, token0);
        var route = Route(new object[] { mp_v3_0_1 }, token1, token0);
        Assert.Equal(v3.MidPrice.ToFixed(4), route.MidPrice.ToFixed(4));
        Assert.Equal("5.0000", route.MidPrice.ToFixed(4));
        Assert.True(route.MidPrice.BaseCurrency.Equals(token1));
        Assert.True(route.MidPrice.QuoteCurrency.Equals(token0));
    }

    [Fact]
    public void MidPrice_V3_0to1to2()
    {
        var v3 = MakeV3Route(new[] { mp_v3_0_1, mp_v3_1_2 }, token0, token2);
        var route = Route(new object[] { mp_v3_0_1, mp_v3_1_2 }, token0, token2);
        Assert.Equal(v3.MidPrice.ToFixed(4), route.MidPrice.ToFixed(4));
        Assert.Equal("0.1000", route.MidPrice.ToFixed(4));
        Assert.True(route.MidPrice.BaseCurrency.Equals(token0));
        Assert.True(route.MidPrice.QuoteCurrency.Equals(token2));
    }

    [Fact]
    public void MidPrice_V3_2to1to0()
    {
        var v3 = MakeV3Route(new[] { mp_v3_1_2, mp_v3_0_1 }, token2, token0);
        var route = Route(new object[] { mp_v3_1_2, mp_v3_0_1 }, token2, token0);
        Assert.Equal(v3.MidPrice.ToFixed(4), route.MidPrice.ToFixed(4));
        Assert.Equal("10.0000", route.MidPrice.ToFixed(4));
        Assert.True(route.MidPrice.BaseCurrency.Equals(token2));
        Assert.True(route.MidPrice.QuoteCurrency.Equals(token0));
    }

    [Fact]
    public void MidPrice_V3_EtherTo0()
    {
        var v3 = MakeV3Route(new[] { mp_v3_0_weth }, ETHER, token0);
        var route = Route(new object[] { mp_v3_0_weth }, ETHER, token0);
        Assert.Equal(v3.MidPrice.ToFixed(4), route.MidPrice.ToFixed(4));
        Assert.Equal("0.3333", route.MidPrice.ToFixed(4));
        Assert.True(route.MidPrice.BaseCurrency.Equals(ETHER));
        Assert.True(route.MidPrice.QuoteCurrency.Equals(token0));
    }

    [Fact]
    public void MidPrice_V3_1toWeth()
    {
        var v3 = MakeV3Route(new[] { mp_v3_1_weth }, token1, weth);
        var route = Route(new object[] { mp_v3_1_weth }, token1, weth);
        Assert.Equal(v3.MidPrice.ToFixed(4), route.MidPrice.ToFixed(4));
        Assert.Equal("0.1429", route.MidPrice.ToFixed(4));
        Assert.True(route.MidPrice.BaseCurrency.Equals(token1));
        Assert.True(route.MidPrice.QuoteCurrency.Equals(weth));
    }

    [Fact]
    public void MidPrice_V3_EtherTo0to1toWeth()
    {
        var v3 = MakeV3Route(new[] { mp_v3_0_weth, mp_v3_0_1, mp_v3_1_weth }, ETHER, weth);
        var route = Route(new object[] { mp_v3_0_weth, mp_v3_0_1, mp_v3_1_weth }, ETHER, weth);
        Assert.Equal(v3.MidPrice.ToFixed(4), route.MidPrice.ToFixed(4));
        Assert.Equal("0.009524", route.MidPrice.ToSignificant(4));
        Assert.True(route.MidPrice.BaseCurrency.Equals(ETHER));
        Assert.True(route.MidPrice.QuoteCurrency.Equals(weth));
    }

    [Fact]
    public void MidPrice_V3_WethTo0to1toEther()
    {
        var v3 = MakeV3Route(new[] { mp_v3_0_weth, mp_v3_0_1, mp_v3_1_weth }, weth, ETHER);
        var route = Route(new object[] { mp_v3_0_weth, mp_v3_0_1, mp_v3_1_weth }, weth, ETHER);
        Assert.Equal(v3.MidPrice.ToFixed(4), route.MidPrice.ToFixed(4));
        Assert.Equal("0.009524", route.MidPrice.ToSignificant(4));
        Assert.True(route.MidPrice.BaseCurrency.Equals(weth));
        Assert.True(route.MidPrice.QuoteCurrency.Equals(ETHER));
    }

    [Fact]
    public void MidPrice_V2_0to1()
    {
        var route = Route(new object[] { mp_pair_0_1 }, token0, token1);
        Assert.Equal("2.0000", route.MidPrice.ToFixed(4));
    }

    [Fact]
    public void MidPrice_V2_IsCached()
    {
        var route = Route(new object[] { mp_pair_0_1 }, token0, token1);
        Assert.Same(route.MidPrice, route.MidPrice);
    }

    [Fact]
    public void MidPrice_V2_1to0()
    {
        var route = Route(new object[] { mp_pair_0_1 }, token1, token0);
        Assert.Equal("0.5000", route.MidPrice.ToFixed(4));
        Assert.True(route.MidPrice.BaseCurrency.Equals(token1));
        Assert.True(route.MidPrice.QuoteCurrency.Equals(token0));
    }

    [Fact]
    public void MidPrice_V2_0to1to2()
    {
        var route = Route(new object[] { mp_pair_0_1, mp_pair_1_2 }, token0, token2);
        Assert.Equal("1.5000", route.MidPrice.ToFixed(4));
        Assert.True(route.MidPrice.BaseCurrency.Equals(token0));
        Assert.True(route.MidPrice.QuoteCurrency.Equals(token2));
    }

    [Fact]
    public void MidPrice_V2_2to1to0()
    {
        var route = Route(new object[] { mp_pair_1_2, mp_pair_0_1 }, token2, token0);
        Assert.Equal("0.6667", route.MidPrice.ToFixed(4));
        Assert.True(route.MidPrice.BaseCurrency.Equals(token2));
        Assert.True(route.MidPrice.QuoteCurrency.Equals(token0));
    }

    [Fact]
    public void MidPrice_V2_EtherTo0()
    {
        var route = Route(new object[] { mp_pair_0_weth }, ETHER, token0);
        Assert.Equal("1.0000", route.MidPrice.ToFixed(4));
        Assert.True(route.MidPrice.BaseCurrency.Equals(ETHER));
        Assert.True(route.MidPrice.QuoteCurrency.Equals(token0));
    }

    [Fact]
    public void MidPrice_V2_1toWeth()
    {
        var route = Route(new object[] { mp_pair_1_weth }, token1, weth);
        Assert.Equal("0.5714", route.MidPrice.ToFixed(4));
        Assert.True(route.MidPrice.BaseCurrency.Equals(token1));
        Assert.True(route.MidPrice.QuoteCurrency.Equals(weth));
    }

    [Fact]
    public void MidPrice_V2_EtherTo0to1toWeth()
    {
        var route = Route(new object[] { mp_pair_0_weth, mp_pair_0_1, mp_pair_1_weth }, ETHER, weth);
        Assert.Equal("1.143", route.MidPrice.ToSignificant(4));
        Assert.True(route.MidPrice.BaseCurrency.Equals(ETHER));
        Assert.True(route.MidPrice.QuoteCurrency.Equals(weth));
    }

    [Fact]
    public void MidPrice_V2_WethTo0to1toEther()
    {
        var route = Route(new object[] { mp_pair_0_weth, mp_pair_0_1, mp_pair_1_weth }, weth, ETHER);
        Assert.Equal("1.143", route.MidPrice.ToSignificant(4));
        Assert.True(route.MidPrice.BaseCurrency.Equals(weth));
        Assert.True(route.MidPrice.QuoteCurrency.Equals(ETHER));
    }

    [Fact]
    public void MidPrice_Mixed_0V3_1V2_2()
    {
        var route = Route(new object[] { mp_v3_0_1, mp_pair_1_2 }, token0, token2);
        Assert.Equal("0.1500", route.MidPrice.ToFixed(4));
    }

    [Fact]
    public void MidPrice_Mixed_0V3_1V2_2_1for2()
    {
        var route = Route(new object[] { mp_v3_0_1, mp_pair_0_2 }, token1, token2);
        Assert.Equal("3.7500", route.MidPrice.ToFixed(4));
    }

    [Fact]
    public void MidPrice_Mixed_0V2_1V2_wethV3_2()
    {
        var route = Route(new object[] { mp_pair_0_1, mp_pair_1_weth, mp_v3_2_weth }, token0, token2);
        Assert.Equal("9.1429", route.MidPrice.ToFixed(4));
    }

    // ---- partitionMixedRouteByProtocol ----
    [Fact]
    public void Partition_SingleV3Pool()
    {
        var route = Route(new object[] { pool_v3_0_1 }, token0, token1);
        var result = MixedRouteUtils.PartitionMixedRouteByProtocol(route);
        Assert.Single(result);
        AssertPools(new object[] { pool_v3_0_1 }, result[0]);
    }

    [Fact]
    public void Partition_SingleV2Pair()
    {
        var route = Route(new object[] { pair_0_1 }, token0, token1);
        var result = MixedRouteUtils.PartitionMixedRouteByProtocol(route);
        Assert.Single(result);
        AssertPools(new object[] { pair_0_1 }, result[0]);
    }

    [Fact]
    public void Partition_AllV3Pools()
    {
        var route = Route(new object[] { pool_v3_0_1, pool_v3_1_weth, pool_v3_2_weth }, token0, token2);
        var result = MixedRouteUtils.PartitionMixedRouteByProtocol(route);
        Assert.Single(result);
        Assert.Equal(3, result[0].Count);
        AssertPools(new object[] { pool_v3_0_1, pool_v3_1_weth, pool_v3_2_weth }, result[0]);
    }

    [Fact]
    public void Partition_ConsecutivePairInMiddle()
    {
        var route = Route(new object[] { pool_v3_0_1, pair_1_weth, pair_weth_2, pool_v3_2_3 }, token0, token3);
        var result = MixedRouteUtils.PartitionMixedRouteByProtocol(route);
        Assert.Equal(3, result.Count);
        Assert.Same(pool_v3_0_1, result[0][0]);
        Assert.Equal(2, result[1].Count);
        AssertPools(new object[] { pair_1_weth, pair_weth_2 }, result[1]);
        Assert.Same(pool_v3_2_3, result[2][0]);
    }

    [Fact]
    public void Partition_ConsecutivePairAtEnd()
    {
        var route = Route(new object[] { pool_v3_0_1, pair_1_weth, pair_weth_2, pair_2_3 }, token0, token3);
        var result = MixedRouteUtils.PartitionMixedRouteByProtocol(route);
        Assert.Equal(2, result.Count);
        Assert.Same(pool_v3_0_1, result[0][0]);
        AssertPools(new object[] { pair_1_weth, pair_weth_2, pair_2_3 }, result[1]);
    }

    [Fact]
    public void Partition_ConsecutivePairAtBeginning()
    {
        var route = Route(new object[] { pair_0_1, pair_1_weth, pair_weth_2, pool_v3_2_3 }, token0, token3);
        var result = MixedRouteUtils.PartitionMixedRouteByProtocol(route);
        Assert.Equal(2, result.Count);
        AssertPools(new object[] { pair_0_1, pair_1_weth, pair_weth_2 }, result[0]);
        Assert.Same(pool_v3_2_3, result[1][0]);
    }

    [Fact]
    public void Partition_WithV4Pool()
    {
        var route = Route(new object[] { pool_v4_0_1, pool_v4_1_eth, pair_weth_2, pool_v3_2_3 }, token0, token3);
        var result = MixedRouteUtils.PartitionMixedRouteByProtocol(route);
        Assert.Equal(3, result.Count);
        Assert.Equal(2, result[0].Count);
        Assert.Single(result[1]);
        Assert.Single(result[2]);
        Assert.Same(pool_v4_0_1, result[0][0]);
        Assert.Same(pool_v4_1_eth, result[0][1]);
        Assert.Same(pair_weth_2, result[1][0]);
        Assert.Same(pool_v3_2_3, result[2][0]);
    }
}
