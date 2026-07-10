using System.Numerics;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.V3.Utils;
// ReSharper disable InconsistentNaming

namespace UniswapSharp.V3.Entities;

public class Pool
{
    private static readonly NoTickDataProvider NO_TICK_DATA_PROVIDER_DEFAULT = new();

    private Price<Token, Token>? token0Price;
    private Price<Token, Token>? token1Price;

    public Pool(Token tokenA, Token tokenB, Constants.FeeAmount fee, BigInteger sqrtRatioX96, BigInteger liquidity, int tickCurrent, object? ticks = null)
    {
        //if (!(fee is int) || fee >= 1_000_000)
        //    throw new ArgumentException("FEE");


        var tickCurrentSqrtRatioX96 = TickMath.GetSqrtRatioAtTick(tickCurrent);
        var nextTickSqrtRatioX96 = TickMath.GetSqrtRatioAtTick(tickCurrent + 1);
        if (sqrtRatioX96 < tickCurrentSqrtRatioX96 || sqrtRatioX96 > nextTickSqrtRatioX96)
        {
            throw new ArgumentException("PRICE_BOUNDS");
        }

        (Token0, Token1) = tokenA.SortsBefore(tokenB) ? (tokenA, tokenB) : (tokenB, tokenA);
        Fee = fee;
        SqrtRatioX96 = sqrtRatioX96;
        Liquidity = liquidity;
        TickCurrent = tickCurrent;
        TickDataProvider = ticks is IEnumerable<Tick> tickArray
            ? new TickListDataProvider(tickArray, Constants.TICK_SPACINGS[fee])
            : ticks as ITickDataProvider ?? NO_TICK_DATA_PROVIDER_DEFAULT;
    }

    public Token Token0 { get; }
    public Token Token1 { get; }
    public Constants.FeeAmount Fee { get; }
    public BigInteger SqrtRatioX96 { get; }
    public BigInteger Liquidity { get; }
    public int TickCurrent { get; }
    public ITickDataProvider TickDataProvider { get; }

    public Price<Token, Token> Token0Price
    {
        get { return token0Price ??= new Price<Token, Token>(Token0, Token1, Constants.Q192, SqrtRatioX96 * SqrtRatioX96); }
    }

    public Price<Token, Token> Token1Price
    {
        get { return token1Price ??= new Price<Token, Token>(Token1, Token0, SqrtRatioX96 * SqrtRatioX96, Constants.Q192); }
    }

    public int ChainId => Token0.ChainId;

    public int TickSpacing => Constants.TICK_SPACINGS[Fee];

    public static string GetAddress(Token tokenA, Token tokenB, Constants.FeeAmount fee, string? initCodeHashManualOverride = null,
        string? factoryAddressOverride = null)
    {
        return ComputePoolAddress.Compute(
            factoryAddressOverride ?? Constants.FACTORY_ADDRESS,
            tokenA,
            tokenB,
            fee,
            initCodeHashManualOverride);
    }

    public bool InvolvesToken(Token token)
    {
        return token.Equals(Token0) || token.Equals(Token1);
    }

    public Price<Token, Token> PriceOf(Token token)
    {
        if (!InvolvesToken(token))
        {
            throw new ArgumentException("TOKEN");
        }

        return token.Equals(Token0) ? Token0Price : Token1Price;
    }

    public async Task<(CurrencyAmount<Token> outputAmount, Pool pool)> GetOutputAmount(CurrencyAmount<Token> inputAmount, BigInteger? sqrtPriceLimitX96 = null)
    {
        if (!InvolvesToken(inputAmount.Currency))
        {
            throw new ArgumentException("TOKEN");
        }

        var zeroForOne = inputAmount.Currency.Equals(Token0);

        var (outputAmount, sqrtRatioX96, liquidity, tickCurrent) = await Swap(zeroForOne, inputAmount.Quotient, sqrtPriceLimitX96);

        var outputToken = zeroForOne ? Token1 : Token0;
        return (CurrencyAmount<Token>.FromRawAmount(outputToken, outputAmount * Constants.NEGATIVE_ONE),
            new Pool(Token0, Token1, Fee, sqrtRatioX96, liquidity, tickCurrent, TickDataProvider));
    }

    public async Task<(CurrencyAmount<Token> inputAmount, Pool pool)> GetInputAmount(CurrencyAmount<Token> outputAmount, BigInteger? sqrtPriceLimitX96 = null)
    {
        if (!outputAmount.Currency.IsToken || !InvolvesToken(outputAmount.Currency))
        {
            throw new ArgumentException("TOKEN");
        }

        var zeroForOne = outputAmount.Currency.Equals(Token1);

        var (inputAmount, sqrtRatioX96, liquidity, tickCurrent) = await Swap(zeroForOne, outputAmount.Quotient * Constants.NEGATIVE_ONE, sqrtPriceLimitX96);
        var inputToken = zeroForOne ? Token0 : Token1;
        return (CurrencyAmount<Token>.FromRawAmount(inputToken, inputAmount),
            new Pool(Token0, Token1, Fee, sqrtRatioX96, liquidity, tickCurrent, TickDataProvider));
    }

    private async Task<(BigInteger amountCalculated, BigInteger sqrtRatioX96, BigInteger liquidity, int tickCurrent)> Swap(bool zeroForOne, BigInteger amountSpecified, BigInteger? sqrtPriceLimitX96 = null)
    {
        return await V3Swap.ExecuteAsync(
            (int)Fee,
            SqrtRatioX96,
            TickCurrent,
            Liquidity,
            TickSpacing,
            TickDataProvider,
            zeroForOne,
            amountSpecified,
            sqrtPriceLimitX96);
    }
}
