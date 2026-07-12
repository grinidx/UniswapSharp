using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using V2Pair = UniswapSharp.V2.Entities.Pair;
using V3Pool = UniswapSharp.V3.Entities.Pool;
using V4Pool = UniswapSharp.V4.Entities.Pool;

namespace UniswapSharp.Router.Utils;

/// <summary>
/// Port of router-sdk <c>utils/TPool.ts</c> — the union type <c>Pair | V3Pool | V4Pool</c>.
/// <para>
/// C# has no structural union type, so a mixed route holds its pools as <see cref="object"/> and this
/// helper provides the uniform accessors that upstream reaches through <c>instanceof</c> narrowing,
/// normalising the differing V2/V3 (token-keyed) and V4 (currency-keyed) surfaces onto
/// <see cref="BaseCurrency"/>.
/// </para>
/// </summary>
public static class TPool
{
    public static bool IsV2(object pool) => pool is V2Pair;
    public static bool IsV3(object pool) => pool is V3Pool;
    public static bool IsV4(object pool) => pool is V4Pool;

    public static int ChainId(object pool) => pool switch
    {
        V2Pair pair => pair.ChainId,
        V3Pool v3 => v3.ChainId,
        V4Pool v4 => v4.ChainId,
        _ => throw new ArgumentException(UnsupportedMessage(pool)),
    };

    public static BaseCurrency Token0(object pool) => pool switch
    {
        V2Pair pair => pair.Token0,
        V3Pool v3 => v3.Token0,
        V4Pool v4 => v4.Currency0,
        _ => throw new ArgumentException(UnsupportedMessage(pool)),
    };

    public static BaseCurrency Token1(object pool) => pool switch
    {
        V2Pair pair => pair.Token1,
        V3Pool v3 => v3.Token1,
        V4Pool v4 => v4.Currency1,
        _ => throw new ArgumentException(UnsupportedMessage(pool)),
    };

    public static bool InvolvesToken(object pool, BaseCurrency currency) => pool switch
    {
        V2Pair pair => currency is Token t && pair.InvolvesToken(t),
        V3Pool v3 => currency is Token t && v3.InvolvesToken(t),
        V4Pool v4 => v4.InvolvesCurrency(currency),
        _ => throw new ArgumentException(UnsupportedMessage(pool)),
    };

    /// <summary>V4-only involvement check (matches native against wrapped and vice-versa).</summary>
    public static bool V4InvolvesToken(object pool, BaseCurrency currency) => pool switch
    {
        V4Pool v4 => v4.V4InvolvesToken(currency),
        _ => throw new ArgumentException("v4InvolvesToken is only defined for V4 pools"),
    };

    public static Price<BaseCurrency, BaseCurrency> Token0Price(object pool) => pool switch
    {
        V2Pair pair => ToBasePrice(pair.Token0Price),
        V3Pool v3 => ToBasePrice(v3.Token0Price),
        V4Pool v4 => v4.Currency0Price,
        _ => throw new ArgumentException(UnsupportedMessage(pool)),
    };

    public static Price<BaseCurrency, BaseCurrency> Token1Price(object pool) => pool switch
    {
        V2Pair pair => ToBasePrice(pair.Token1Price),
        V3Pool v3 => ToBasePrice(v3.Token1Price),
        V4Pool v4 => v4.Currency1Price,
        _ => throw new ArgumentException(UnsupportedMessage(pool)),
    };

    /// <summary>Uniform <c>getOutputAmount</c> over the union; returns only the output amount.</summary>
    public static async Task<CurrencyAmount<BaseCurrency>> GetOutputAmount(object pool, CurrencyAmount<BaseCurrency> inputAmount)
    {
        switch (pool)
        {
            case V2Pair pair:
                {
                    var (output, _) = pair.GetOutputAmount(ToTokenAmount(inputAmount));
                    return output.AsBaseCurrency()!;
                }
            case V3Pool v3:
                {
                    var (output, _) = await v3.GetOutputAmount(ToTokenAmount(inputAmount));
                    return output.AsBaseCurrency()!;
                }
            case V4Pool v4:
                {
                    var (output, _) = await v4.GetOutputAmount(inputAmount);
                    return output;
                }
            default:
                throw new ArgumentException(UnsupportedMessage(pool));
        }
    }

    /// <summary>A protocol-unique identifier for the pool, used for cross-route de-duplication.</summary>
    public static string PoolIdentifier(object pool) => pool switch
    {
        V4Pool v4 => v4.PoolId,
        V3Pool v3 => V3Pool.GetAddress(v3.Token0, v3.Token1, v3.Fee),
        V2Pair pair => V2Pair.GetAddress(pair.Token0, pair.Token1),
        _ => throw new ArgumentException("Unexpected pool type in route when constructing trade object"),
    };

    private static CurrencyAmount<Token> ToTokenAmount(CurrencyAmount<BaseCurrency> amount)
    {
        if (amount.Currency is not Token token)
        {
            throw new ArgumentException("Expected a token amount for a V2/V3 pool");
        }
        return CurrencyAmount<Token>.FromFractionalAmount(token, amount.Numerator, amount.Denominator);
    }

    private static Price<BaseCurrency, BaseCurrency> ToBasePrice<TBase, TQuote>(Price<TBase, TQuote> price)
        where TBase : BaseCurrency where TQuote : BaseCurrency =>
        new(price.BaseCurrency, price.QuoteCurrency, price.Denominator, price.Numerator);

    private static string UnsupportedMessage(object pool) => $"Unsupported pool type {pool.GetType().Name}";
}
