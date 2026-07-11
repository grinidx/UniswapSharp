using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.V2.Entities;

namespace UniswapSharp.Testing.V2;

public class RouteTests
{
    private static readonly Ether ETHER = Ether.OnChain(1);
    private static readonly Token token0 = new(1, "0x0000000000000000000000000000000000000001", 18, "t0");
    private static readonly Token token1 = new(1, "0x0000000000000000000000000000000000000002", 18, "t1");
    private static readonly Token weth = Weth9.Tokens[1];

    private static readonly Pair pair_0_1 = new(CurrencyAmount<Token>.FromRawAmount(token0, 100), CurrencyAmount<Token>.FromRawAmount(token1, 200));
    private static readonly Pair pair_0_weth = new(CurrencyAmount<Token>.FromRawAmount(token0, 100), CurrencyAmount<Token>.FromRawAmount(weth, 100));
    private static readonly Pair pair_1_weth = new(CurrencyAmount<Token>.FromRawAmount(token1, 175), CurrencyAmount<Token>.FromRawAmount(weth, 100));

    [Fact]
    public void ConstructsPathFromTokens()
    {
        var route = new Route<Token, Token>(new List<Pair> { pair_0_1 }, token0, token1);
        Assert.Equal(new List<Pair> { pair_0_1 }, route.Pairs);
        Assert.Equal(new List<Token> { token0, token1 }, route.Path);
        Assert.Equal(token0, route.Input);
        Assert.Equal(token1, route.Output);
        Assert.Equal(1, route.ChainId);
    }

    [Fact]
    public void CanHaveTokenAsBothInputAndOutput()
    {
        var route = new Route<Token, Token>(new List<Pair> { pair_0_weth, pair_0_1, pair_1_weth }, weth, weth);
        Assert.Equal(new List<Pair> { pair_0_weth, pair_0_1, pair_1_weth }, route.Pairs);
        Assert.Equal(weth, route.Input);
        Assert.Equal(weth, route.Output);
    }

    [Fact]
    public void SupportsEtherInput()
    {
        var route = new Route<Ether, Token>(new List<Pair> { pair_0_weth }, ETHER, token0);
        Assert.Equal(new List<Pair> { pair_0_weth }, route.Pairs);
        Assert.Equal(ETHER, route.Input);
        Assert.Equal(token0, route.Output);
    }

    [Fact]
    public void SupportsEtherOutput()
    {
        var route = new Route<Token, Ether>(new List<Pair> { pair_0_weth }, token0, ETHER);
        Assert.Equal(new List<Pair> { pair_0_weth }, route.Pairs);
        Assert.Equal(token0, route.Input);
        Assert.Equal(ETHER, route.Output);
    }
}
