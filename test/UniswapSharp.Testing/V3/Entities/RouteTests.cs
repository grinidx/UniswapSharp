using UniswapSharp.Core.Entities;
using UniswapSharp.V3;
using UniswapSharp.V3.Entities;
using UniswapSharp.V3.Utils;

namespace UniswapSharp.Testing.V3.Entities;

public class RouteTests
{
    private readonly Ether ETHER = Ether.OnChain(1);
    private readonly Token token0 = new Token(1, "0x0000000000000000000000000000000000000001", 18, "t0");
    private readonly Token token1 = new Token(1, "0x0000000000000000000000000000000000000002", 18, "t1");
    private readonly Token token2 = new Token(1, "0x0000000000000000000000000000000000000003", 18, "t2");
    private readonly Token weth = Weth9.Tokens[1];

    private readonly Pool pool_0_1;
    private readonly Pool pool_0_weth;
    private readonly Pool pool_1_weth;

    public RouteTests()
    {
        pool_0_1 = new Pool(token0, token1, Constants.FeeAmount.MEDIUM, EncodeSqrtRatioX96.Encode(1, 1), 0, 0, new List<int>());
        pool_0_weth = new Pool(token0, weth, Constants.FeeAmount.MEDIUM, EncodeSqrtRatioX96.Encode(1, 1), 0, 0, new List<int>());
        pool_1_weth = new Pool(token1, weth, Constants.FeeAmount.MEDIUM, EncodeSqrtRatioX96.Encode(1, 1), 0, 0, new List<int>());
    }

    [Fact]
    public void ConstructsPathFromTokens()
    {
        var route = new Route<Token, Token>(new List<Pool> { pool_0_1 }, token0, token1);
        Assert.Equal(new List<Pool> { pool_0_1 }, route.Pools);
        Assert.Equal(new List<Token> { token0, token1 }, route.TokenPath);
        Assert.Equal(token0, route.Input);
        Assert.Equal(token1, route.Output);
        Assert.Equal(1, route.ChainId);
    }

    [Fact]
    public void FailsIfInputNotInFirstPool()
    {
        Assert.Throws<ArgumentException>(() => new Route<Token, Token>(new List<Pool> { pool_0_1 }, weth, token1));
    }

    [Fact]
    public void FailsIfOutputNotInLastPool()
    {
        Assert.Throws<ArgumentException>(() => new Route<Token, Token>(new List<Pool> { pool_0_1 }, token0, weth));
    }

    [Fact]
    public void CanHaveTokenAsInputAndOutput()
    {
        var route = new Route<Token, Token>(new List<Pool> { pool_0_weth, pool_0_1, pool_1_weth }, weth, weth);
        Assert.Equal(new List<Pool> { pool_0_weth, pool_0_1, pool_1_weth }, route.Pools);
        Assert.Equal(weth, route.Input);
        Assert.Equal(weth, route.Output);
    }

    [Fact]
    public void SupportsEtherInput()
    {
        var route = new Route<NativeCurrency, Token>(new List<Pool> { pool_0_weth }, ETHER, token0);
        Assert.Equal(new List<Pool> { pool_0_weth }, route.Pools);
        Assert.Equal(ETHER, route.Input);
        Assert.Equal(token0, route.Output);
    }

    [Fact]
    public void SupportsEtherOutput()
    {
        var route = new Route<Token, NativeCurrency>(new List<Pool> { pool_0_weth }, token0, ETHER);
        Assert.Equal(new List<Pool> { pool_0_weth }, route.Pools);
        Assert.Equal(token0, route.Input);
        Assert.Equal(ETHER, route.Output);
    }

    public class MidPriceTests
    {
        private readonly Pool pool_0_1;
        private readonly Pool pool_1_2;
        private readonly Pool pool_0_weth;
        private readonly Pool pool_1_weth;

        public MidPriceTests()
        {
            var token0 = new Token(1, "0x0000000000000000000000000000000000000001", 18, "t0");
            var token1 = new Token(1, "0x0000000000000000000000000000000000000002", 18, "t1");
            var token2 = new Token(1, "0x0000000000000000000000000000000000000003", 18, "t2");
            var weth = Weth9.Tokens[1];

            pool_0_1 = new Pool(token0, token1, Constants.FeeAmount.MEDIUM, EncodeSqrtRatioX96.Encode(1, 5), 0, TickMath.GetTickAtSqrtRatio(EncodeSqrtRatioX96.Encode(1, 5)), new List<int>());
            pool_1_2 = new Pool(token1, token2, Constants.FeeAmount.MEDIUM, EncodeSqrtRatioX96.Encode(15, 30), 0, TickMath.GetTickAtSqrtRatio(EncodeSqrtRatioX96.Encode(15, 30)), new List<int>());
            pool_0_weth = new Pool(token0, weth, Constants.FeeAmount.MEDIUM, EncodeSqrtRatioX96.Encode(3, 1), 0, TickMath.GetTickAtSqrtRatio(EncodeSqrtRatioX96.Encode(3, 1)), new List<int>());
            pool_1_weth = new Pool(token1, weth, Constants.FeeAmount.MEDIUM, EncodeSqrtRatioX96.Encode(1, 7), 0, TickMath.GetTickAtSqrtRatio(EncodeSqrtRatioX96.Encode(1, 7)), new List<int>());
        }

        [Fact]
        public void CorrectFor0To1()
        {
            var price = new Route<Token, Token>(new List<Pool> { pool_0_1 }, pool_0_1.Token0, pool_0_1.Token1).MidPrice;
            Assert.Equal("0.2000", price.ToFixed(4));
            Assert.True(price.BaseCurrency.Equals(pool_0_1.Token0));
            Assert.True(price.QuoteCurrency.Equals(pool_0_1.Token1));
        }

        [Fact]
        public void IsCached()
        {
            var route = new Route<Token, Token>(new List<Pool> { pool_0_1 }, pool_0_1.Token0, pool_0_1.Token1);
            Assert.Equal(route.MidPrice, route.MidPrice);
        }

        // Add more tests for other scenarios...
    }
}
