using System.Numerics;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.V3.Entities;
using UniswapSharp.V3.Utils;
using UniswapSharp.V4;
using UniswapSharp.V4.Entities;
using UniswapSharp.V4.Utils;
using Pool = UniswapSharp.V4.Entities.Pool;

namespace UniswapSharp.Testing.V4.Entities;

// Ported 1:1 from sdks/v4-sdk/src/entities/pool.test.ts.
// (The upstream "fee must be integer" case is omitted: the C# fee parameter is an int.)
public class PoolTests
{
    private static readonly Token USDC = new(1, "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48", 6, "USDC", "USD Coin");
    private static readonly Token DAI = new(1, "0x6B175474E89094C44Da98b954EedeAC495271d0F", 18, "DAI", "DAI Stablecoin");
    private static readonly Token WETH1 = Weth9.Tokens[1];
    private static readonly Token WETH3 = Weth9.Tokens[3];

    private const string ADDRESS_ZERO = Constants.ADDRESS_ZERO;
    private const int LOW = Constants.FEE_AMOUNT_LOW;
    private const int MEDIUM = Constants.FEE_AMOUNT_MEDIUM;
    private const int HIGHEST = Constants.FEE_AMOUNT_HIGHEST;
    private const int SPACING = Constants.TICK_SPACING_TEN;

    private static BigInteger Encode(BigInteger a, BigInteger b) => EncodeSqrtRatioX96.Encode(a, b);

    private static string ConstructHookAddress(params HookOptions[] options)
    {
        int flags = 0;
        foreach (var o in options) flags |= 1 << (int)o;
        string f = flags.ToString("x");
        return "0x" + new string('0', 40 - f.Length) + f;
    }

    // ---- constructor ----
    [Fact]
    public void Constructor_DifferentChainsThrows()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new Pool(USDC, WETH3, MEDIUM, SPACING, ADDRESS_ZERO, Encode(1, 1), 0, 0, new List<Tick>()));
        Assert.Equal("CHAIN_IDS", ex.Message);
    }

    [Fact]
    public void Constructor_FeeCannotBeMoreThan1e6()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new Pool(USDC, WETH1, 1_000_000, SPACING, ADDRESS_ZERO, Encode(1, 1), 0, 0, new List<Tick>()));
        Assert.Equal("FEE", ex.Message);
    }

    [Fact]
    public void Constructor_FeeCanBeDynamic()
    {
        var pool = new Pool(USDC, WETH1, Pool.DYNAMIC_FEE_FLAG, SPACING, "0xfff0000000000000000000000000000000000000", Encode(1, 1), 0, 0, new List<Tick>());
        Assert.Equal(Pool.DYNAMIC_FEE_FLAG, pool.Fee);
    }

    [Fact]
    public void Constructor_DynamicFeeRequiresHook()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new Pool(USDC, WETH1, Pool.DYNAMIC_FEE_FLAG, SPACING, ADDRESS_ZERO, Encode(1, 1), 0, 0, new List<Tick>()));
        Assert.Equal("Dynamic fee pool requires a hook", ex.Message);
    }

    [Fact]
    public void Constructor_InvalidHookAddress()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new Pool(USDC, WETH1, MEDIUM, SPACING, "0x123", Encode(1, 1), 0, 0, new List<Tick>()));
        Assert.Equal("Invalid hook address", ex.Message);
    }

    [Fact]
    public void Constructor_SameCurrencyThrows()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new Pool(USDC, USDC, MEDIUM, SPACING, ADDRESS_ZERO, Encode(1, 1), 0, 0, new List<Tick>()));
        Assert.Equal("ADDRESSES", ex.Message);
    }

    [Fact]
    public void Constructor_PriceBounds()
    {
        Assert.Equal("PRICE_BOUNDS", Assert.Throws<ArgumentException>(() =>
            new Pool(USDC, WETH1, MEDIUM, SPACING, ADDRESS_ZERO, Encode(1, 1), 0, 1, new List<Tick>())).Message);
        Assert.Equal("PRICE_BOUNDS", Assert.Throws<ArgumentException>(() =>
            new Pool(USDC, WETH1, MEDIUM, SPACING, ADDRESS_ZERO, Encode(1, 1) + 1, 0, -1, new List<Tick>())).Message);
    }

    [Fact]
    public void Constructor_WorksForEmptyPools()
    {
        _ = new Pool(USDC, WETH1, MEDIUM, SPACING, ADDRESS_ZERO, Encode(1, 1), 0, 0, new List<Tick>());
        _ = new Pool(USDC, WETH1, 1, SPACING, ADDRESS_ZERO, Encode(1, 1), 0, 0, new List<Tick>());
        _ = new Pool(USDC, WETH1, HIGHEST, SPACING, ADDRESS_ZERO, Encode(1, 1), 0, 0, new List<Tick>());
    }

    // ---- getPoolId / getPoolKey ----
    [Fact]
    public void GetPoolId_Matches()
    {
        var result1 = Pool.GetPoolId(USDC, DAI, LOW, SPACING, ADDRESS_ZERO);
        Assert.Equal("0x503fb8d73fd2351c645ae9fea85381bac6b16ea0c2038e14dc1e96d447c8ffbb", result1);
        Assert.Equal(result1, Pool.GetPoolId(DAI, USDC, LOW, SPACING, ADDRESS_ZERO));
    }

    [Fact]
    public void GetPoolKey_Matches()
    {
        var expected = new PoolKey(DAI.Address, USDC.Address, LOW, SPACING, ADDRESS_ZERO);
        Assert.Equal(expected, Pool.GetPoolKey(USDC, DAI, LOW, SPACING, ADDRESS_ZERO));
        Assert.Equal(expected, Pool.GetPoolKey(DAI, USDC, LOW, SPACING, ADDRESS_ZERO));
    }

    [Fact]
    public void Currency0_SortsBefore()
    {
        Assert.True(new Pool(USDC, DAI, LOW, SPACING, ADDRESS_ZERO, Encode(1, 1), 0, 0, new List<Tick>()).Currency0.Equals(DAI));
        Assert.True(new Pool(DAI, USDC, LOW, SPACING, ADDRESS_ZERO, Encode(1, 1), 0, 0, new List<Tick>()).Currency0.Equals(DAI));
    }

    [Fact]
    public void Currency1_SortsAfter()
    {
        Assert.True(new Pool(USDC, DAI, LOW, SPACING, ADDRESS_ZERO, Encode(1, 1), 0, 0, new List<Tick>()).Currency1.Equals(USDC));
        Assert.True(new Pool(DAI, USDC, LOW, SPACING, ADDRESS_ZERO, Encode(1, 1), 0, 0, new List<Tick>()).Currency1.Equals(USDC));
    }

    [Fact]
    public void PoolId_And_PoolKey_Properties()
    {
        var pool = new Pool(USDC, DAI, LOW, SPACING, ADDRESS_ZERO, Encode(1, 1), 0, 0, new List<Tick>());
        Assert.Equal("0x503fb8d73fd2351c645ae9fea85381bac6b16ea0c2038e14dc1e96d447c8ffbb", pool.PoolId);
        Assert.Equal(new PoolKey(DAI.Address, USDC.Address, LOW, SPACING, ADDRESS_ZERO), pool.PoolKey);
    }

    // ---- prices ----
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Currency0Price(bool usdcFirst)
    {
        var sqrt = Encode(new BigInteger(101_000_000), BigInteger.Parse("100000000000000000000"));
        var pool = usdcFirst
            ? new Pool(USDC, DAI, LOW, SPACING, ADDRESS_ZERO, sqrt, 0, TickMath.GetTickAtSqrtRatio(sqrt), new List<Tick>())
            : new Pool(DAI, USDC, LOW, SPACING, ADDRESS_ZERO, sqrt, 0, TickMath.GetTickAtSqrtRatio(sqrt), new List<Tick>());
        Assert.Equal("1.01", pool.Currency0Price.ToSignificant(5));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Currency1Price(bool usdcFirst)
    {
        var sqrt = Encode(new BigInteger(101_000_000), BigInteger.Parse("100000000000000000000"));
        var pool = usdcFirst
            ? new Pool(USDC, DAI, LOW, SPACING, ADDRESS_ZERO, sqrt, 0, TickMath.GetTickAtSqrtRatio(sqrt), new List<Tick>())
            : new Pool(DAI, USDC, LOW, SPACING, ADDRESS_ZERO, sqrt, 0, TickMath.GetTickAtSqrtRatio(sqrt), new List<Tick>());
        Assert.Equal("0.9901", pool.Currency1Price.ToSignificant(5));
    }

    [Fact]
    public void PriceOf()
    {
        var pool = new Pool(USDC, DAI, LOW, SPACING, ADDRESS_ZERO, Encode(1, 1), 0, 0, new List<Tick>());
        Assert.Same(pool.Currency0Price, pool.PriceOf(DAI));
        Assert.Same(pool.Currency1Price, pool.PriceOf(USDC));
        Assert.Equal("CURRENCY", Assert.Throws<ArgumentException>(() => pool.PriceOf(WETH1)).Message);
    }

    [Fact]
    public void ChainId()
    {
        Assert.Equal(1, new Pool(USDC, DAI, LOW, SPACING, ADDRESS_ZERO, Encode(1, 1), 0, 0, new List<Tick>()).ChainId);
        Assert.Equal(1, new Pool(DAI, USDC, LOW, SPACING, ADDRESS_ZERO, Encode(1, 1), 0, 0, new List<Tick>()).ChainId);
    }

    [Fact]
    public void InvolvesCurrency()
    {
        var pool = new Pool(USDC, DAI, LOW, SPACING, ADDRESS_ZERO, Encode(1, 1), 0, 0, new List<Tick>());
        Assert.True(pool.InvolvesCurrency(USDC));
        Assert.True(pool.InvolvesCurrency(DAI));
        Assert.False(pool.InvolvesCurrency(WETH1));
    }

    [Fact]
    public void V4InvolvesToken()
    {
        var pool = new Pool(Ether.OnChain(1), DAI, LOW, SPACING, ADDRESS_ZERO, Encode(1, 1), 0, 0, new List<Tick>());
        Assert.True(pool.V4InvolvesToken(Ether.OnChain(1)));
        Assert.True(pool.V4InvolvesToken(DAI));
        Assert.True(pool.V4InvolvesToken(WETH1));

        var pool2 = new Pool(Ether.OnChain(1).Wrapped(), DAI, LOW, SPACING, ADDRESS_ZERO, Encode(1, 1), 0, 0, new List<Tick>());
        Assert.True(pool2.V4InvolvesToken(Ether.OnChain(1)));
        Assert.True(pool2.V4InvolvesToken(DAI));
        Assert.True(pool2.V4InvolvesToken(WETH1));
    }

    // ---- swaps ----
    private static Pool MakeSwapPool() => new(
        USDC, DAI, LOW, SPACING, ADDRESS_ZERO, Encode(1, 1), Constants.ONE_ETHER, 0,
        new List<Tick>
        {
            new(NearestUsableTick.Find(TickMath.MIN_TICK, SPACING), Constants.ONE_ETHER, Constants.ONE_ETHER),
            new(NearestUsableTick.Find(TickMath.MAX_TICK, SPACING), Constants.ONE_ETHER * Constants.NEGATIVE_ONE, Constants.ONE_ETHER),
        });

    private static Pool MakeSwapHookPool() => new(
        USDC, DAI, LOW, SPACING, ConstructHookAddress(HookOptions.BeforeSwap), Encode(1, 1), Constants.ONE_ETHER, 0, new List<Tick>());

    [Fact]
    public async Task GetOutputAmount_ThrowsForBeforeSwapHook()
    {
        var input = CurrencyAmount<BaseCurrency>.FromRawAmount((BaseCurrency)USDC, 100);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => MakeSwapHookPool().GetOutputAmount(input));
        Assert.Equal("Unsupported hook", ex.Message);
    }

    [Fact]
    public async Task GetOutputAmount_UsdcToDai()
    {
        var input = CurrencyAmount<BaseCurrency>.FromRawAmount((BaseCurrency)USDC, 100);
        var (output, _) = await MakeSwapPool().GetOutputAmount(input);
        Assert.True(output.Currency.Equals(DAI));
        Assert.Equal(new BigInteger(98), output.Quotient);
    }

    [Fact]
    public async Task GetOutputAmount_DaiToUsdc()
    {
        var input = CurrencyAmount<BaseCurrency>.FromRawAmount((BaseCurrency)DAI, 100);
        var (output, _) = await MakeSwapPool().GetOutputAmount(input);
        Assert.True(output.Currency.Equals(USDC));
        Assert.Equal(new BigInteger(98), output.Quotient);
    }

    [Fact]
    public async Task GetInputAmount_ThrowsForBeforeSwapHook()
    {
        var output = CurrencyAmount<BaseCurrency>.FromRawAmount((BaseCurrency)DAI, 98);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => MakeSwapHookPool().GetInputAmount(output));
        Assert.Equal("Unsupported hook", ex.Message);
    }

    [Fact]
    public async Task GetInputAmount_UsdcToDai()
    {
        var output = CurrencyAmount<BaseCurrency>.FromRawAmount((BaseCurrency)DAI, 98);
        var (input, _) = await MakeSwapPool().GetInputAmount(output);
        Assert.True(input.Currency.Equals(USDC));
        Assert.Equal(new BigInteger(100), input.Quotient);
    }

    [Fact]
    public async Task GetInputAmount_DaiToUsdc()
    {
        var output = CurrencyAmount<BaseCurrency>.FromRawAmount((BaseCurrency)USDC, 98);
        var (input, _) = await MakeSwapPool().GetInputAmount(output);
        Assert.True(input.Currency.Equals(DAI));
        Assert.Equal(new BigInteger(100), input.Quotient);
    }

    [Fact]
    public async Task BigNums()
    {
        var bigNum = new BigInteger(9007199254740991L) + 1; // MAX_SAFE_INTEGER + 1
        var pool = new Pool(USDC, DAI, LOW, SPACING, ADDRESS_ZERO, Encode(bigNum, bigNum), Constants.ONE_ETHER, 0,
            new List<Tick>
            {
                new(NearestUsableTick.Find(TickMath.MIN_TICK, SPACING), Constants.ONE_ETHER, Constants.ONE_ETHER),
                new(NearestUsableTick.Find(TickMath.MAX_TICK, SPACING), Constants.ONE_ETHER * Constants.NEGATIVE_ONE, Constants.ONE_ETHER),
            });
        var input = CurrencyAmount<BaseCurrency>.FromRawAmount((BaseCurrency)USDC, 100);
        var (output, _) = await pool.GetOutputAmount(input);
        Assert.True(output.Currency.Equals(DAI));
    }

    // ---- backwards compatibility ----
    [Fact]
    public void BackwardsCompatibility()
    {
        var pool = new Pool(USDC, DAI, LOW, SPACING, ADDRESS_ZERO, Encode(1, 1), 0, 0, new List<Tick>());
        Assert.Same(pool.Currency0, pool.Token0);
        Assert.Same(pool.Currency1, pool.Token1);
        Assert.Same(pool.Currency0Price, pool.Token0Price);
        Assert.Same(pool.Currency1Price, pool.Token1Price);
        Assert.Equal(pool.InvolvesCurrency(USDC), pool.InvolvesToken(USDC));
    }
}
