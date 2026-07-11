using UniswapSharp.Core.Entities;
using UniswapSharp.V3.Utils;
using UniswapSharp.V4;
using UniswapSharp.V4.Entities;
using Pool = UniswapSharp.V4.Entities.Pool;
using Tick = UniswapSharp.V3.Entities.Tick;

namespace UniswapSharp.Testing.V4.Entities;

// Ported 1:1 from sdks/v4-sdk/src/entities/route.test.ts
public class RouteTests
{
    private static readonly Ether eth = Ether.OnChain(1);
    private static readonly Token currency0 = new(1, "0x0000000000000000000000000000000000000001", 18, "t0");
    private static readonly Token currency1 = new(1, "0x0000000000000000000000000000000000000002", 18, "t1");
    private static readonly Token currency2 = new(1, "0x0000000000000000000000000000000000000003", 18, "t2");
    private static readonly Token currency3 = new(1, "0xD000000000000000000000000000000000000000", 18, "t3");
    private static readonly Token weth = Weth9.Tokens[1];

    private const int MEDIUM = Constants.FEE_AMOUNT_MEDIUM;
    private const int SPACING = Constants.TICK_SPACING_TEN;
    private const string ZERO = Constants.ADDRESS_ZERO;

    private static Pool MakePool(BaseCurrency a, BaseCurrency b, int a1 = 1, int a0 = 1)
    {
        var sqrt = EncodeSqrtRatioX96.Encode(a1, a0);
        int tick = (a1 == 1 && a0 == 1) ? 0 : TickMath.GetTickAtSqrtRatio(sqrt);
        return new Pool(a, b, MEDIUM, SPACING, ZERO, sqrt, 0, tick, new List<Tick>());
    }

    private static readonly Pool pool_0_1 = MakePool(currency0, currency1);
    private static readonly Pool pool_0_eth = MakePool(currency0, eth);
    private static readonly Pool pool_1_eth = MakePool(currency1, eth);
    private static readonly Pool pool_0_weth = MakePool(currency0, weth);
    private static readonly Pool pool_eth_weth = MakePool(eth, weth);

    private static void AssertCurrency(BaseCurrency expected, BaseCurrency actual) => Assert.True(actual.Equals(expected));

    // ---- path ----
    [Fact]
    public void ConstructsPathFromCurrencies()
    {
        var route = new Route<Token, Token>(new List<Pool> { pool_0_1 }, currency0, currency1);
        Assert.Equal(new List<Pool> { pool_0_1 }, route.Pools);
        Assert.Equal(2, route.CurrencyPath.Count);
        AssertCurrency(currency0, route.CurrencyPath[0]);
        AssertCurrency(currency1, route.CurrencyPath[1]);
        AssertCurrency(currency0, route.Input);
        AssertCurrency(currency1, route.Output);
        Assert.Equal(1, route.ChainId);
    }

    [Fact]
    public void FailsIfInputNotInFirstPool()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => new Route<Ether, Token>(new List<Pool> { pool_0_1 }, eth, currency1));
        Assert.Equal("Expected currency ETH to be either t0 or t1", ex.Message);
    }

    [Fact]
    public void FailsIfOutputNotInLastPool()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => new Route<Token, Ether>(new List<Pool> { pool_0_1 }, currency0, eth));
        Assert.Equal("Expected currency ETH to be either t0 or t1", ex.Message);
    }

    [Fact]
    public void CurrencyAsBothInputAndOutput()
    {
        var route = new Route<Ether, Ether>(new List<Pool> { pool_0_eth, pool_0_1, pool_1_eth }, eth, eth);
        Assert.Equal(new List<Pool> { pool_0_eth, pool_0_1, pool_1_eth }, route.Pools);
        AssertCurrency(eth, route.Input);
        AssertCurrency(eth, route.Output);
    }

    [Fact]
    public void SupportsEtherInput()
    {
        var route = new Route<Ether, Token>(new List<Pool> { pool_0_eth }, eth, currency0);
        Assert.Equal(new List<Pool> { pool_0_eth }, route.Pools);
        AssertCurrency(eth, route.Input);
        AssertCurrency(currency0, route.Output);
    }

    [Fact]
    public void EtherInput_EthWethFirst_EthSecond()
    {
        var route = new Route<Ether, Token>(new List<Pool> { pool_eth_weth, pool_0_eth }, eth, currency0);
        Assert.Equal(new List<Pool> { pool_eth_weth, pool_0_eth }, route.Pools);
        AssertCurrency(eth, route.Input);
        AssertCurrency(currency0, route.Output);
    }

    [Fact]
    public void WethInput_EthWethFirst_WethSecond()
    {
        var route = new Route<Token, Token>(new List<Pool> { pool_eth_weth, pool_0_weth }, weth, currency0);
        Assert.Equal(new List<Pool> { pool_eth_weth, pool_0_weth }, route.Pools);
        AssertCurrency(weth, route.Input);
        AssertCurrency(currency0, route.Output);
    }

    [Fact]
    public void EtherInput_EthWethFirst_WethSecond()
    {
        var route = new Route<Ether, Token>(new List<Pool> { pool_eth_weth, pool_0_weth }, eth, currency0);
        Assert.Equal(new List<Pool> { pool_eth_weth, pool_0_weth }, route.Pools);
        AssertCurrency(eth, route.Input);
        AssertCurrency(currency0, route.Output);
    }

    [Fact]
    public void WethInput_EthWethFirst_EthSecond()
    {
        var route = new Route<Token, Token>(new List<Pool> { pool_eth_weth, pool_0_eth }, weth, currency0);
        Assert.Equal(new List<Pool> { pool_eth_weth, pool_0_eth }, route.Pools);
        AssertCurrency(weth, route.Input);
        AssertCurrency(currency0, route.Output);
    }

    [Fact]
    public void EthWeth_EthInput()
    {
        var route = new Route<Ether, Token>(new List<Pool> { pool_eth_weth }, eth, weth);
        AssertCurrency(eth, route.Input);
        AssertCurrency(weth, route.Output);
    }

    [Fact]
    public void EthWeth_WethInput()
    {
        var route = new Route<Token, Ether>(new List<Pool> { pool_eth_weth }, weth, eth);
        AssertCurrency(weth, route.Input);
        AssertCurrency(eth, route.Output);
    }

    [Fact]
    public void SupportsEtherOutput()
    {
        var route = new Route<Token, Ether>(new List<Pool> { pool_0_eth }, currency0, eth);
        AssertCurrency(currency0, route.Input);
        AssertCurrency(eth, route.Output);
    }

    [Fact]
    public void NoWethToEthWithoutEthWethPool()
    {
        Assert.Equal("PATH", Assert.Throws<ArgumentException>(() =>
            new Route<Token, Token>(new List<Pool> { pool_0_weth, pool_1_eth }, currency0, currency1)).Message);
    }

    [Fact]
    public void NoEthToWethWithoutEthWethPool()
    {
        Assert.Equal("PATH", Assert.Throws<ArgumentException>(() =>
            new Route<Token, Token>(new List<Pool> { pool_1_eth, pool_0_weth }, currency1, currency0)).Message);
    }

    [Fact]
    public void SupportsTradingThroughEthWethPools()
    {
        var route = new Route<Token, Token>(new List<Pool> { pool_0_weth, pool_eth_weth, pool_1_eth }, currency0, currency1);
        Assert.Equal(new List<Pool> { pool_0_weth, pool_eth_weth, pool_1_eth }, route.Pools);
        AssertCurrency(currency0, route.Input);
        AssertCurrency(currency1, route.Output);
    }

    // ---- midPrice ----
    private static readonly Pool mp_0_1 = MakePool(currency0, currency1, 1, 5);
    private static readonly Pool mp_1_2 = MakePool(currency1, currency2, 15, 30);
    private static readonly Pool mp_0_eth = MakePool(currency0, eth, 3, 1);
    private static readonly Pool mp_1_eth = MakePool(currency1, eth, 1, 7);
    private static readonly Pool mp_3_weth = MakePool(weth, currency3, 1, 5);
    private static readonly Pool mp_0_3 = MakePool(currency0, currency3, 1, 5);

    [Fact]
    public void MidPrice_0_to_1()
    {
        var price = new Route<Token, Token>(new List<Pool> { mp_0_1 }, currency0, currency1).MidPrice;
        Assert.Equal("0.2000", price.ToFixed(4));
        AssertCurrency(currency0, price.BaseCurrency);
        AssertCurrency(currency1, price.QuoteCurrency);
    }

    [Fact]
    public void MidPrice_IsCached()
    {
        var route = new Route<Token, Token>(new List<Pool> { mp_0_1 }, currency0, currency1);
        Assert.Same(route.MidPrice, route.MidPrice);
    }

    [Fact]
    public void MidPrice_1_to_0()
    {
        var price = new Route<Token, Token>(new List<Pool> { mp_0_1 }, currency1, currency0).MidPrice;
        Assert.Equal("5.0000", price.ToFixed(4));
        AssertCurrency(currency1, price.BaseCurrency);
        AssertCurrency(currency0, price.QuoteCurrency);
    }

    [Fact]
    public void MidPrice_0_1_2()
    {
        var price = new Route<Token, Token>(new List<Pool> { mp_0_1, mp_1_2 }, currency0, currency2).MidPrice;
        Assert.Equal("0.1000", price.ToFixed(4));
        AssertCurrency(currency0, price.BaseCurrency);
        AssertCurrency(currency2, price.QuoteCurrency);
    }

    [Fact]
    public void MidPrice_2_1_0()
    {
        var price = new Route<Token, Token>(new List<Pool> { mp_1_2, mp_0_1 }, currency2, currency0).MidPrice;
        Assert.Equal("10.0000", price.ToFixed(4));
        AssertCurrency(currency2, price.BaseCurrency);
        AssertCurrency(currency0, price.QuoteCurrency);
    }

    [Fact]
    public void MidPrice_ether_to_0()
    {
        var price = new Route<Ether, Token>(new List<Pool> { mp_0_eth }, eth, currency0).MidPrice;
        Assert.Equal("3.0000", price.ToFixed(4));
        AssertCurrency(eth, price.BaseCurrency);
        AssertCurrency(currency0, price.QuoteCurrency);
    }

    [Fact]
    public void MidPrice_1_to_eth()
    {
        var price = new Route<Token, Ether>(new List<Pool> { mp_1_eth }, currency1, eth).MidPrice;
        Assert.Equal("7.0000", price.ToFixed(4));
        AssertCurrency(currency1, price.BaseCurrency);
        AssertCurrency(eth, price.QuoteCurrency);
    }

    [Fact]
    public void MidPrice_ether_0_1_eth()
    {
        var price = new Route<Ether, Ether>(new List<Pool> { mp_0_eth, mp_0_1, mp_1_eth }, eth, eth).MidPrice;
        Assert.Equal("4.2", price.ToSignificant(4));
        AssertCurrency(eth, price.BaseCurrency);
        AssertCurrency(eth, price.QuoteCurrency);
    }

    [Fact]
    public void MidPrice_ethInput_wethPathInput()
    {
        var price = new Route<Ether, Token>(new List<Pool> { mp_3_weth }, eth, currency3).MidPrice;
        Assert.Equal("0.2", price.ToSignificant(4));
        AssertCurrency(eth, price.BaseCurrency);
        AssertCurrency(currency3, price.QuoteCurrency);
    }

    [Fact]
    public void MidPrice_ethInput_wethPathInput_multiplePools()
    {
        var price = new Route<Ether, Token>(new List<Pool> { mp_3_weth, mp_0_3 }, eth, currency0).MidPrice;
        Assert.Equal("1", price.ToSignificant(4));
        AssertCurrency(eth, price.BaseCurrency);
        AssertCurrency(currency0, price.QuoteCurrency);
    }

    // ---- pathInput / pathOutput ----
    [Fact]
    public void EtherInputOnWethPool()
    {
        var route = new Route<Ether, Token>(new List<Pool> { pool_0_weth }, eth, currency0);
        AssertCurrency(eth, route.Input);
        AssertCurrency(weth, route.PathInput);
        AssertCurrency(currency0, route.Output);
        AssertCurrency(currency0, route.PathOutput);
    }

    [Fact]
    public void WethInputOnEthPool()
    {
        var route = new Route<Token, Token>(new List<Pool> { pool_0_eth }, weth, currency0);
        AssertCurrency(weth, route.Input);
        AssertCurrency(eth, route.PathInput);
        AssertCurrency(currency0, route.Output);
        AssertCurrency(currency0, route.PathOutput);
    }

    [Fact]
    public void EtherOutputOnWethPool()
    {
        var route = new Route<Token, Ether>(new List<Pool> { pool_0_weth }, currency0, eth);
        AssertCurrency(currency0, route.Input);
        AssertCurrency(currency0, route.PathInput);
        AssertCurrency(eth, route.Output);
        AssertCurrency(weth, route.PathOutput);
    }

    [Fact]
    public void WethOutputOnEthPool()
    {
        var route = new Route<Token, Token>(new List<Pool> { pool_0_eth }, currency0, weth);
        AssertCurrency(currency0, route.Input);
        AssertCurrency(currency0, route.PathInput);
        AssertCurrency(weth, route.Output);
        AssertCurrency(eth, route.PathOutput);
    }
}
