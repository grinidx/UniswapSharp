using System.Numerics;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.Router.Entities;
using UniswapSharp.V3.Utils;
using FeeAmount = UniswapSharp.V3.Constants.FeeAmount;
using Tick = UniswapSharp.V3.Entities.Tick;
using V2Pair = UniswapSharp.V2.Entities.Pair;
using V2Route = UniswapSharp.V2.Entities.Route<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;
using V3Pool = UniswapSharp.V3.Entities.Pool;
using V3Route = UniswapSharp.V3.Entities.Route<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;

namespace UniswapSharp.Testing.Router.Entities;

// Ported 1:1 from sdks/router-sdk/src/entities/route.test.ts
public class RouteTests
{
    private static readonly Ether ETHER = Ether.OnChain(1);
    private static readonly Token token0 = new(1, "0x0000000000000000000000000000000000000001", 18, "t0");
    private static readonly Token token1 = new(1, "0x0000000000000000000000000000000000000002", 18, "t1");
    private static readonly Token token2 = new(1, "0x0000000000000000000000000000000000000003", 18, "t2");
    private static readonly Token weth = Weth9.Tokens[1];

    private static BigInteger S(int a, int b) => EncodeSqrtRatioX96.Encode(a, b);
    private static List<Tick> NoTicks() => new();

    private static readonly V3Pool pool_0_1 = new(token0, token1, FeeAmount.MEDIUM, S(1, 1), 0, 0, NoTicks());
    private static readonly V3Pool pool_0_weth = new(token0, weth, FeeAmount.MEDIUM, S(1, 1), 0, 0, NoTicks());
    private static readonly V3Pool pool_1_weth = new(token1, weth, FeeAmount.MEDIUM, S(1, 1), 0, 0, NoTicks());

    private static V3Route MakeV3(IEnumerable<V3Pool> pools, BaseCurrency input, BaseCurrency output) => new(pools.ToList(), input, output);

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
            Assert.True(actual[i].Equals(expected[i]));
        }
    }

    // ---- RouteV3 ----
    [Fact]
    public void RouteV3_Path_WrapsAndConstructsPath()
    {
        var route = new RouteV3<BaseCurrency, BaseCurrency>(MakeV3(new[] { pool_0_1 }, token0, token1));
        AssertPools(new object[] { pool_0_1 }, route.Pools);
        AssertPath(new BaseCurrency[] { token0, token1 }, route.Path);
        Assert.True(route.Input.Equals(token0));
        Assert.True(route.Output.Equals(token1));
        Assert.Equal(1, route.ChainId);
    }

    [Fact]
    public void RouteV3_AssignsProtocol()
    {
        var route = new RouteV3<BaseCurrency, BaseCurrency>(MakeV3(new[] { pool_0_1 }, token0, token1));
        Assert.Equal(Protocol.V3, route.Protocol);
    }

    [Fact]
    public void RouteV3_InheritsParameters()
    {
        var original = MakeV3(new[] { pool_0_1 }, token0, token1);
        var route = new RouteV3<BaseCurrency, BaseCurrency>(original);
        AssertPools(original.Pools.Cast<object>().ToArray(), route.Pools);
        AssertPath(original.TokenPath.Cast<BaseCurrency>().ToArray(), route.Path);
        Assert.True(route.Input.Equals(original.Input));
        Assert.True(route.Output.Equals(original.Output));
        Assert.True(route.MidPrice.Equals(original.MidPrice));
        Assert.Equal(original.ChainId, route.ChainId);
    }

    [Fact]
    public void RouteV3_TokenAsBothInputAndOutput()
    {
        var route = new RouteV3<BaseCurrency, BaseCurrency>(MakeV3(new[] { pool_0_weth, pool_0_1, pool_1_weth }, weth, weth));
        AssertPools(new object[] { pool_0_weth, pool_0_1, pool_1_weth }, route.Pools);
        Assert.True(route.Input.Equals(weth));
        Assert.True(route.Output.Equals(weth));
    }

    [Fact]
    public void RouteV3_SupportsEtherInput()
    {
        var route = new RouteV3<BaseCurrency, BaseCurrency>(MakeV3(new[] { pool_0_weth }, ETHER, token0));
        AssertPools(new object[] { pool_0_weth }, route.Pools);
        Assert.True(route.Input.Equals(ETHER));
        Assert.True(route.Output.Equals(token0));
    }

    [Fact]
    public void RouteV3_SupportsEtherOutput()
    {
        var route = new RouteV3<BaseCurrency, BaseCurrency>(MakeV3(new[] { pool_0_weth }, token0, ETHER));
        AssertPools(new object[] { pool_0_weth }, route.Pools);
        Assert.True(route.Input.Equals(token0));
        Assert.True(route.Output.Equals(ETHER));
    }

    // ---- RouteV3 #midPrice ----
    private static readonly V3Pool mp_0_1 = new(token0, token1, FeeAmount.MEDIUM, S(1, 5), 0, TickMath.GetTickAtSqrtRatio(S(1, 5)), NoTicks());
    private static readonly V3Pool mp_1_2 = new(token1, token2, FeeAmount.MEDIUM, S(15, 30), 0, TickMath.GetTickAtSqrtRatio(S(15, 30)), NoTicks());
    private static readonly V3Pool mp_0_weth = new(token0, weth, FeeAmount.MEDIUM, S(3, 1), 0, TickMath.GetTickAtSqrtRatio(S(3, 1)), NoTicks());
    private static readonly V3Pool mp_1_weth = new(token1, weth, FeeAmount.MEDIUM, S(1, 7), 0, TickMath.GetTickAtSqrtRatio(S(1, 7)), NoTicks());

    [Fact]
    public void RouteV3_MidPrice_0to1()
    {
        var price = new RouteV3<BaseCurrency, BaseCurrency>(MakeV3(new[] { mp_0_1 }, token0, token1)).MidPrice;
        Assert.Equal("0.2000", price.ToFixed(4));
        Assert.True(price.BaseCurrency.Equals(token0));
        Assert.True(price.QuoteCurrency.Equals(token1));
    }

    [Fact]
    public void RouteV3_MidPrice_IsCached()
    {
        var route = new RouteV3<BaseCurrency, BaseCurrency>(MakeV3(new[] { mp_0_1 }, token0, token1));
        Assert.Same(route.MidPrice, route.MidPrice);
    }

    [Fact]
    public void RouteV3_MidPrice_1to0()
    {
        var price = new RouteV3<BaseCurrency, BaseCurrency>(MakeV3(new[] { mp_0_1 }, token1, token0)).MidPrice;
        Assert.Equal("5.0000", price.ToFixed(4));
        Assert.True(price.BaseCurrency.Equals(token1));
        Assert.True(price.QuoteCurrency.Equals(token0));
    }

    [Fact]
    public void RouteV3_MidPrice_0to1to2()
    {
        var price = new RouteV3<BaseCurrency, BaseCurrency>(MakeV3(new[] { mp_0_1, mp_1_2 }, token0, token2)).MidPrice;
        Assert.Equal("0.1000", price.ToFixed(4));
        Assert.True(price.BaseCurrency.Equals(token0));
        Assert.True(price.QuoteCurrency.Equals(token2));
    }

    [Fact]
    public void RouteV3_MidPrice_2to1to0()
    {
        var price = new RouteV3<BaseCurrency, BaseCurrency>(MakeV3(new[] { mp_1_2, mp_0_1 }, token2, token0)).MidPrice;
        Assert.Equal("10.0000", price.ToFixed(4));
        Assert.True(price.BaseCurrency.Equals(token2));
        Assert.True(price.QuoteCurrency.Equals(token0));
    }

    [Fact]
    public void RouteV3_MidPrice_EtherTo0()
    {
        var price = new RouteV3<BaseCurrency, BaseCurrency>(MakeV3(new[] { mp_0_weth }, ETHER, token0)).MidPrice;
        Assert.Equal("0.3333", price.ToFixed(4));
        Assert.True(price.BaseCurrency.Equals(ETHER));
        Assert.True(price.QuoteCurrency.Equals(token0));
    }

    [Fact]
    public void RouteV3_MidPrice_EtherTo0to1toWeth()
    {
        var price = new RouteV3<BaseCurrency, BaseCurrency>(MakeV3(new[] { mp_0_weth, mp_0_1, mp_1_weth }, ETHER, weth)).MidPrice;
        Assert.Equal("0.009524", price.ToSignificant(4));
        Assert.True(price.BaseCurrency.Equals(ETHER));
        Assert.True(price.QuoteCurrency.Equals(weth));
    }

    [Fact]
    public void RouteV3_MidPrice_WethTo0to1toEther()
    {
        var price = new RouteV3<BaseCurrency, BaseCurrency>(MakeV3(new[] { mp_0_weth, mp_0_1, mp_1_weth }, weth, ETHER)).MidPrice;
        Assert.Equal("0.009524", price.ToSignificant(4));
        Assert.True(price.BaseCurrency.Equals(weth));
        Assert.True(price.QuoteCurrency.Equals(ETHER));
    }

    // ---- RouteV2 ----
    private static V2Pair Pair(Token a, int aAmt, Token b, int bAmt) =>
        new(CurrencyAmount<Token>.FromRawAmount(a, aAmt), CurrencyAmount<Token>.FromRawAmount(b, bAmt));

    private static readonly V2Pair pair_0_1 = Pair(token0, 100, token1, 200);
    private static readonly V2Pair pair_0_weth = Pair(token0, 100, weth, 100);
    private static readonly V2Pair pair_1_weth = Pair(token1, 175, weth, 100);

    private static V2Route MakeV2(IEnumerable<V2Pair> pairs, BaseCurrency input, BaseCurrency output) => new(pairs.ToList(), input, output);

    [Fact]
    public void RouteV2_AssignsProtocol()
    {
        var route = new RouteV2<BaseCurrency, BaseCurrency>(MakeV2(new[] { pair_0_1 }, token0, token1));
        Assert.Equal(Protocol.V2, route.Protocol);
    }

    [Fact]
    public void RouteV2_InheritsParameters()
    {
        var original = MakeV2(new[] { pair_0_1 }, token0, token1);
        var route = new RouteV2<BaseCurrency, BaseCurrency>(original);
        AssertPools(original.Pairs.Cast<object>().ToArray(), route.Pools);
        AssertPath(original.Path.Cast<BaseCurrency>().ToArray(), route.Path);
        Assert.True(route.Input.Equals(original.Input));
        Assert.True(route.Output.Equals(original.Output));
        Assert.True(route.MidPrice.Equals(original.MidPrice));
        Assert.Equal(original.ChainId, route.ChainId);
    }

    [Fact]
    public void RouteV2_ConstructsPath()
    {
        var route = new RouteV2<BaseCurrency, BaseCurrency>(MakeV2(new[] { pair_0_1 }, token0, token1));
        AssertPools(new object[] { pair_0_1 }, route.Pools);
        AssertPath(new BaseCurrency[] { token0, token1 }, route.Path);
        Assert.True(route.Input.Equals(token0));
        Assert.True(route.Output.Equals(token1));
        Assert.Equal(1, route.ChainId);
    }

    [Fact]
    public void RouteV2_TokenAsBothInputAndOutput()
    {
        var route = new RouteV2<BaseCurrency, BaseCurrency>(MakeV2(new[] { pair_0_weth, pair_0_1, pair_1_weth }, weth, weth));
        AssertPools(new object[] { pair_0_weth, pair_0_1, pair_1_weth }, route.Pools);
        Assert.True(route.Input.Equals(weth));
        Assert.True(route.Output.Equals(weth));
    }

    [Fact]
    public void RouteV2_SupportsEtherInput()
    {
        var route = new RouteV2<BaseCurrency, BaseCurrency>(MakeV2(new[] { pair_0_weth }, ETHER, token0));
        AssertPools(new object[] { pair_0_weth }, route.Pools);
        Assert.True(route.Input.Equals(ETHER));
        Assert.True(route.Output.Equals(token0));
    }

    [Fact]
    public void RouteV2_SupportsEtherOutput()
    {
        var route = new RouteV2<BaseCurrency, BaseCurrency>(MakeV2(new[] { pair_0_weth }, token0, ETHER));
        AssertPools(new object[] { pair_0_weth }, route.Pools);
        Assert.True(route.Input.Equals(token0));
        Assert.True(route.Output.Equals(ETHER));
    }

    [Fact]
    public void RouteV2_AssignsPathInputAndPathOutput()
    {
        var route = new RouteV2<BaseCurrency, BaseCurrency>(MakeV2(new[] { pair_0_weth }, token0, ETHER));
        Assert.True(route.PathInput.Equals(token0));
        Assert.True(route.PathOutput.Equals(weth));
    }
}
