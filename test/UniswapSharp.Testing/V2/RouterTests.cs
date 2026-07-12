using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.V2;
using V2Router = UniswapSharp.V2.Router;
using UniswapSharp.V2.Entities;

namespace UniswapSharp.Testing.V2;

public class RouterTests
{
    private static readonly Ether ETHER = Ether.OnChain(1);
    private static readonly Token token0 = new(1, "0x0000000000000000000000000000000000000001", 18, "t0");
    private static readonly Token token1 = new(1, "0x0000000000000000000000000000000000000002", 18, "t1");
    private static readonly Token weth = Weth9.Tokens[1];

    private const string Recipient = "0x0000000000000000000000000000000000000004";

    private static CurrencyAmount<Token> CA(Token t, long v) => CurrencyAmount<Token>.FromRawAmount(t, v);
    private static CurrencyAmount<Ether> CAE(long v) => CurrencyAmount<Ether>.FromRawAmount(ETHER, v);

    private static readonly Pair pair_0_1 = new(CA(token0, 1000), CA(token1, 1000));
    private static readonly Pair pair_weth_0 = new(CA(weth, 1000), CA(token0, 1000));

    private static void CheckDeadline(object deadline)
    {
        var s = Assert.IsType<string>(deadline);
        var value = Convert.ToInt64(s[2..], 16); // strip "0x"
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        // deadline = now + ttl(50); require now - deadline <= 5 (upstream), i.e. it is not in the past
        Assert.True(now - value <= 5);
    }

    private static TradeOptions Opts(int? ttl = null, int? deadline = null, bool feeOnTransfer = false) => new()
    {
        Ttl = ttl,
        Deadline = deadline,
        Recipient = Recipient,
        AllowedSlippage = new Percent(1, 100),
        FeeOnTransfer = feeOnTransfer
    };

    // ---- exact in ----

    [Fact]
    public void ExactIn_EtherToToken1()
    {
        var result = V2Router.SwapCallParameters(
            Trade<Ether, Token>.ExactIn(new Route<Ether, Token>(new List<Pair> { pair_weth_0, pair_0_1 }, ETHER, token1), CAE(100)),
            Opts(ttl: 50));
        Assert.Equal("swapExactETHForTokens", result.MethodName);
        Assert.Equal("0x51", (string)result.Args[0]);
        Assert.Equal(new[] { weth.Address, token0.Address, token1.Address }, (string[])result.Args[1]);
        Assert.Equal(Recipient, (string)result.Args[2]);
        Assert.Equal("0x64", result.Value);
        CheckDeadline(result.Args[^1]);
    }

    [Fact]
    public void ExactIn_DeadlineSpecified()
    {
        var result = V2Router.SwapCallParameters(
            Trade<Ether, Token>.ExactIn(new Route<Ether, Token>(new List<Pair> { pair_weth_0, pair_0_1 }, ETHER, token1), CAE(100)),
            Opts(deadline: 50));
        Assert.Equal("swapExactETHForTokens", result.MethodName);
        Assert.Equal("0x51", (string)result.Args[0]);
        Assert.Equal(new[] { weth.Address, token0.Address, token1.Address }, (string[])result.Args[1]);
        Assert.Equal(Recipient, (string)result.Args[2]);
        Assert.Equal("0x32", (string)result.Args[3]);
        Assert.Equal("0x64", result.Value);
    }

    [Fact]
    public void ExactIn_Token1ToEther()
    {
        var result = V2Router.SwapCallParameters(
            Trade<Token, Ether>.ExactIn(new Route<Token, Ether>(new List<Pair> { pair_0_1, pair_weth_0 }, token1, ETHER), CA(token1, 100)),
            Opts(ttl: 50));
        Assert.Equal("swapExactTokensForETH", result.MethodName);
        Assert.Equal("0x64", (string)result.Args[0]);
        Assert.Equal("0x51", (string)result.Args[1]);
        Assert.Equal(new[] { token1.Address, token0.Address, weth.Address }, (string[])result.Args[2]);
        Assert.Equal(Recipient, (string)result.Args[3]);
        Assert.Equal("0x0", result.Value);
        CheckDeadline(result.Args[^1]);
    }

    [Fact]
    public void ExactIn_Token0ToToken1()
    {
        var result = V2Router.SwapCallParameters(
            Trade<Token, Token>.ExactIn(new Route<Token, Token>(new List<Pair> { pair_0_1 }, token0, token1), CA(token0, 100)),
            Opts(ttl: 50));
        Assert.Equal("swapExactTokensForTokens", result.MethodName);
        Assert.Equal("0x64", (string)result.Args[0]);
        Assert.Equal("0x59", (string)result.Args[1]);
        Assert.Equal(new[] { token0.Address, token1.Address }, (string[])result.Args[2]);
        Assert.Equal(Recipient, (string)result.Args[3]);
        Assert.Equal("0x0", result.Value);
        CheckDeadline(result.Args[^1]);
    }

    // ---- exact out ----

    [Fact]
    public void ExactOut_EtherToToken1()
    {
        var result = V2Router.SwapCallParameters(
            Trade<Ether, Token>.ExactOut(new Route<Ether, Token>(new List<Pair> { pair_weth_0, pair_0_1 }, ETHER, token1), CA(token1, 100)),
            Opts(ttl: 50));
        Assert.Equal("swapETHForExactTokens", result.MethodName);
        Assert.Equal("0x64", (string)result.Args[0]);
        Assert.Equal(new[] { weth.Address, token0.Address, token1.Address }, (string[])result.Args[1]);
        Assert.Equal(Recipient, (string)result.Args[2]);
        Assert.Equal("0x80", result.Value);
        CheckDeadline(result.Args[^1]);
    }

    [Fact]
    public void ExactOut_Token1ToEther()
    {
        var result = V2Router.SwapCallParameters(
            Trade<Token, Ether>.ExactOut(new Route<Token, Ether>(new List<Pair> { pair_0_1, pair_weth_0 }, token1, ETHER), CAE(100)),
            Opts(ttl: 50));
        Assert.Equal("swapTokensForExactETH", result.MethodName);
        Assert.Equal("0x64", (string)result.Args[0]);
        Assert.Equal("0x80", (string)result.Args[1]);
        Assert.Equal(new[] { token1.Address, token0.Address, weth.Address }, (string[])result.Args[2]);
        Assert.Equal(Recipient, (string)result.Args[3]);
        Assert.Equal("0x0", result.Value);
        CheckDeadline(result.Args[^1]);
    }

    [Fact]
    public void ExactOut_Token0ToToken1()
    {
        var result = V2Router.SwapCallParameters(
            Trade<Token, Token>.ExactOut(new Route<Token, Token>(new List<Pair> { pair_0_1 }, token0, token1), CA(token1, 100)),
            Opts(ttl: 50));
        Assert.Equal("swapTokensForExactTokens", result.MethodName);
        Assert.Equal("0x64", (string)result.Args[0]);
        Assert.Equal("0x71", (string)result.Args[1]);
        Assert.Equal(new[] { token0.Address, token1.Address }, (string[])result.Args[2]);
        Assert.Equal(Recipient, (string)result.Args[3]);
        Assert.Equal("0x0", result.Value);
        CheckDeadline(result.Args[^1]);
    }

    // ---- supporting fee on transfer, exact in ----

    [Fact]
    public void Fot_ExactIn_EtherToToken1()
    {
        var result = V2Router.SwapCallParameters(
            Trade<Ether, Token>.ExactIn(new Route<Ether, Token>(new List<Pair> { pair_weth_0, pair_0_1 }, ETHER, token1), CAE(100)),
            Opts(ttl: 50, feeOnTransfer: true));
        Assert.Equal("swapExactETHForTokensSupportingFeeOnTransferTokens", result.MethodName);
        Assert.Equal("0x51", (string)result.Args[0]);
        Assert.Equal(new[] { weth.Address, token0.Address, token1.Address }, (string[])result.Args[1]);
        Assert.Equal(Recipient, (string)result.Args[2]);
        Assert.Equal("0x64", result.Value);
        CheckDeadline(result.Args[^1]);
    }

    [Fact]
    public void Fot_ExactIn_Token1ToEther()
    {
        var result = V2Router.SwapCallParameters(
            Trade<Token, Ether>.ExactIn(new Route<Token, Ether>(new List<Pair> { pair_0_1, pair_weth_0 }, token1, ETHER), CA(token1, 100)),
            Opts(ttl: 50, feeOnTransfer: true));
        Assert.Equal("swapExactTokensForETHSupportingFeeOnTransferTokens", result.MethodName);
        Assert.Equal("0x64", (string)result.Args[0]);
        Assert.Equal("0x51", (string)result.Args[1]);
        Assert.Equal(new[] { token1.Address, token0.Address, weth.Address }, (string[])result.Args[2]);
        Assert.Equal(Recipient, (string)result.Args[3]);
        Assert.Equal("0x0", result.Value);
        CheckDeadline(result.Args[^1]);
    }

    [Fact]
    public void Fot_ExactIn_Token0ToToken1()
    {
        var result = V2Router.SwapCallParameters(
            Trade<Token, Token>.ExactIn(new Route<Token, Token>(new List<Pair> { pair_0_1 }, token0, token1), CA(token0, 100)),
            Opts(ttl: 50, feeOnTransfer: true));
        Assert.Equal("swapExactTokensForTokensSupportingFeeOnTransferTokens", result.MethodName);
        Assert.Equal("0x64", (string)result.Args[0]);
        Assert.Equal("0x59", (string)result.Args[1]);
        Assert.Equal(new[] { token0.Address, token1.Address }, (string[])result.Args[2]);
        Assert.Equal(Recipient, (string)result.Args[3]);
        Assert.Equal("0x0", result.Value);
        CheckDeadline(result.Args[^1]);
    }

    // ---- supporting fee on transfer, exact out (all throw) ----

    [Fact]
    public void Fot_ExactOut_EtherToToken1_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => V2Router.SwapCallParameters(
            Trade<Ether, Token>.ExactOut(new Route<Ether, Token>(new List<Pair> { pair_weth_0, pair_0_1 }, ETHER, token1), CA(token1, 100)),
            Opts(ttl: 50, feeOnTransfer: true)));
        Assert.Equal("EXACT_OUT_FOT", ex.Message);
    }

    [Fact]
    public void Fot_ExactOut_Token1ToEther_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => V2Router.SwapCallParameters(
            Trade<Token, Ether>.ExactOut(new Route<Token, Ether>(new List<Pair> { pair_0_1, pair_weth_0 }, token1, ETHER), CAE(100)),
            Opts(ttl: 50, feeOnTransfer: true)));
        Assert.Equal("EXACT_OUT_FOT", ex.Message);
    }

    [Fact]
    public void Fot_ExactOut_Token0ToToken1_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => V2Router.SwapCallParameters(
            Trade<Token, Token>.ExactOut(new Route<Token, Token>(new List<Pair> { pair_0_1 }, token0, token1), CA(token1, 100)),
            Opts(ttl: 50, feeOnTransfer: true)));
        Assert.Equal("EXACT_OUT_FOT", ex.Message);
    }
}
