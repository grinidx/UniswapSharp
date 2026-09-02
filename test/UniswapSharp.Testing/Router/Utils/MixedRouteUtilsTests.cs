using System.Numerics;
using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.Router.Entities.MixedRoute;
using UniswapSharp.Router.Utils;
using UniswapSharp.V3.Utils;
using V2Pair = UniswapSharp.V2.Entities.Pair;
using V4Pool = UniswapSharp.V4.Entities.Pool;

namespace UniswapSharp.Testing.Router.Utils;

// Ported 1:1 from sdks/router-sdk/src/utils/index.test.ts
public class MixedRouteUtilsTests
{
    private static readonly BigInteger SQRT_RATIO_ONE = EncodeSqrtRatioX96.Encode(1, 1);
    private static readonly Ether ETHER = Ether.OnChain(1);
    private static readonly Token weth = Weth9.Tokens[1];
    private static readonly Token token0 = new(1, "0x0000000000000000000000000000000000000001", 18, "t0");
    private static readonly Token token1 = new(1, "0x0000000000000000000000000000000000000002", 18, "t1");

    private const string ZERO = UniswapSharp.Router.Constants.ADDRESS_ZERO;

    private static readonly V4Pool pool_v4_0_eth = new(token0, ETHER, 0, 60, ZERO, SQRT_RATIO_ONE, 0, 0);
    private static readonly V4Pool pool_v4_1_weth = new(token1, weth, 0, 60, ZERO, SQRT_RATIO_ONE, 0, 0);
    private static readonly V4Pool pool_v4_eth_weth = new(ETHER, weth, 0, 60, ZERO, SQRT_RATIO_ONE, 0, 0);

    private static V2Pair Pair(Token a, int aAmt, Token b, int bAmt) =>
        new(CurrencyAmount<Token>.FromRawAmount(a, aAmt), CurrencyAmount<Token>.FromRawAmount(b, bAmt));

    private static readonly V2Pair pair_0_weth = Pair(token0, 100, weth, 100);
    private static readonly V2Pair pair_1_weth = Pair(token1, 100, weth, 100);

    private static MixedRouteSDK<BaseCurrency, BaseCurrency> Route(IEnumerable<object> pools, BaseCurrency input, BaseCurrency output) =>
        new(pools.ToList(), input, output);

    // ---- #partitionMixedRouteByProtocol ----

    [Fact]
    public void Partition_SplitsTwoV4Pools_AtNativeWrappedBoundary()
    {
        var route = Route(new object[] { pool_v4_0_eth, pool_v4_1_weth }, token0, token1);

        var sections = MixedRouteUtils.PartitionMixedRouteByProtocol(route);

        Assert.Equal(2, sections.Count);
        Assert.Same(pool_v4_0_eth, Assert.Single(sections[0]));
        Assert.Same(pool_v4_1_weth, Assert.Single(sections[1]));
    }

    [Fact]
    public void Partition_KeepsV4SectionTogether_WhenConnectedThroughGenuineEthWethPool()
    {
        var route = Route(new object[] { pool_v4_0_eth, pool_v4_eth_weth, pool_v4_1_weth }, token0, token1);

        var sections = MixedRouteUtils.PartitionMixedRouteByProtocol(route);

        var only = Assert.Single(sections);
        Assert.Equal(3, only.Count);
        Assert.Same(pool_v4_0_eth, only[0]);
        Assert.Same(pool_v4_eth_weth, only[1]);
        Assert.Same(pool_v4_1_weth, only[2]);
    }

    [Fact]
    public void Partition_StillSplitsByProtocol()
    {
        var route = Route(new object[] { pool_v4_0_eth, pair_1_weth }, token0, token1);

        var sections = MixedRouteUtils.PartitionMixedRouteByProtocol(route);

        Assert.Equal(2, sections.Count);
        Assert.Same(pool_v4_0_eth, Assert.Single(sections[0]));
        Assert.Same(pair_1_weth, Assert.Single(sections[1]));
    }

    // ---- #getOutputOfPools ----

    [Fact]
    public void GetOutputOfPools_WalksExactMatches()
    {
        Assert.True(MixedRouteUtils.GetOutputOfPools(new object[] { pool_v4_0_eth }, token0).Equals(ETHER));
    }

    [Fact]
    public void GetOutputOfPools_BridgesNativeWrappedBoundary()
    {
        Assert.True(MixedRouteUtils.GetOutputOfPools(new object[] { pool_v4_0_eth, pool_v4_1_weth }, token0).Equals(token1));
        Assert.True(MixedRouteUtils.GetOutputOfPools(new object[] { pair_0_weth }, ETHER).Equals(token0));
    }

    [Fact]
    public void GetOutputOfPools_PrefersExactSideOfGenuineEthWethPool()
    {
        Assert.True(MixedRouteUtils.GetOutputOfPools(new object[] { pool_v4_eth_weth }, ETHER).Equals(weth));
        Assert.True(MixedRouteUtils.GetOutputOfPools(new object[] { pool_v4_eth_weth }, weth).Equals(ETHER));
    }

    [Fact]
    public void GetOutputOfPools_ThrowsForUnrelatedInput()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            MixedRouteUtils.GetOutputOfPools(new object[] { pool_v4_1_weth }, token0));
        Assert.Contains("PATH", ex.Message);
    }
}
