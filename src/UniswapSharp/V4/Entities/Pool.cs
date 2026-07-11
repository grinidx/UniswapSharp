using System.Numerics;
using Nethereum.ABI;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.Core.Utils;
using UniswapSharp.V3.Entities;
using UniswapSharp.V3.Utils;
using UniswapSharp.V4.Utils;

namespace UniswapSharp.V4.Entities;

/// <summary>The five fields that uniquely identify a V4 pool.</summary>
public record PoolKey(string Currency0, string Currency1, int Fee, int TickSpacing, string Hooks);

/// <summary>
/// Represents a V4 pool. Ported from v4-sdk/src/entities/pool.ts. Unlike V3 it is keyed on
/// currencies (native or token), an independent tickSpacing, and a hook address; the pool is
/// identified by a keccak <see cref="PoolId"/> of its <see cref="PoolKey"/> rather than a CREATE2 address.
/// </summary>
public class Pool
{
    public const int DYNAMIC_FEE_FLAG = 0x800000;

    private static readonly NoTickDataProvider NO_TICK_DATA_PROVIDER_DEFAULT = new();
    private static readonly ABIEncode Abi = new();

    private Price<BaseCurrency, BaseCurrency>? _currency0Price;
    private Price<BaseCurrency, BaseCurrency>? _currency1Price;

    public BaseCurrency Currency0 { get; }
    public BaseCurrency Currency1 { get; }
    public int Fee { get; }
    public int TickSpacing { get; }
    public BigInteger SqrtRatioX96 { get; }
    public string Hooks { get; }
    public BigInteger Liquidity { get; }
    public int TickCurrent { get; }
    public ITickDataProvider TickDataProvider { get; }
    public PoolKey PoolKey { get; }
    public string PoolId { get; }

    public static PoolKey GetPoolKey(BaseCurrency currencyA, BaseCurrency currencyB, int fee, int tickSpacing, string hooks)
    {
        if (!IsAddress(hooks))
        {
            throw new ArgumentException("Invalid hook address");
        }

        var (currency0, currency1) = CurrencyOrder.SortsBefore(currencyA, currencyB) ? (currencyA, currencyB) : (currencyB, currencyA);
        return new PoolKey(CurrencyMap.ToAddress(currency0), CurrencyMap.ToAddress(currency1), fee, tickSpacing, hooks);
    }

    public static string GetPoolId(BaseCurrency currencyA, BaseCurrency currencyB, int fee, int tickSpacing, string hooks)
    {
        var (currency0, currency1) = CurrencyOrder.SortsBefore(currencyA, currencyB) ? (currencyA, currencyB) : (currencyB, currencyA);
        byte[] encoded = Abi.GetABIEncoded(
            new ABIValue("address", CurrencyMap.ToAddress(currency0)),
            new ABIValue("address", CurrencyMap.ToAddress(currency1)),
            new ABIValue("uint24", fee),
            new ABIValue("int24", tickSpacing),
            new ABIValue("address", hooks));
        return Sha3Keccack.Current.CalculateHash(encoded).ToHex(true);
    }

    public Pool(
        BaseCurrency currencyA,
        BaseCurrency currencyB,
        int fee,
        int tickSpacing,
        string hooks,
        BigInteger sqrtRatioX96,
        BigInteger liquidity,
        int tickCurrent,
        object? ticks = null)
    {
        if (!IsAddress(hooks))
        {
            throw new ArgumentException("Invalid hook address");
        }
        if (fee != DYNAMIC_FEE_FLAG && fee >= 1_000_000)
        {
            throw new ArgumentException("FEE");
        }
        if (fee == DYNAMIC_FEE_FLAG && !IsNonZeroAddress(hooks))
        {
            throw new ArgumentException("Dynamic fee pool requires a hook");
        }

        var tickCurrentSqrtRatioX96 = TickMath.GetSqrtRatioAtTick(tickCurrent);
        var nextTickSqrtRatioX96 = TickMath.GetSqrtRatioAtTick(tickCurrent + 1);
        if (sqrtRatioX96 < tickCurrentSqrtRatioX96 || sqrtRatioX96 > nextTickSqrtRatioX96)
        {
            throw new ArgumentException("PRICE_BOUNDS");
        }

        (Currency0, Currency1) = CurrencyOrder.SortsBefore(currencyA, currencyB) ? (currencyA, currencyB) : (currencyB, currencyA);
        Fee = fee;
        SqrtRatioX96 = sqrtRatioX96;
        TickSpacing = tickSpacing;
        Hooks = hooks;
        Liquidity = liquidity;
        TickCurrent = tickCurrent;
        TickDataProvider = ticks is IEnumerable<Tick> tickList
            ? new TickListDataProvider(tickList, tickSpacing)
            : ticks as ITickDataProvider ?? NO_TICK_DATA_PROVIDER_DEFAULT;
        PoolKey = GetPoolKey(Currency0, Currency1, Fee, TickSpacing, Hooks);
        PoolId = GetPoolId(Currency0, Currency1, Fee, TickSpacing, Hooks);
    }

    /// <summary>Backwards compatibility with the v2/v3 SDKs.</summary>
    public BaseCurrency Token0 => Currency0;
    public BaseCurrency Token1 => Currency1;

    public bool InvolvesCurrency(BaseCurrency currency) => currency.Equals(Currency0) || currency.Equals(Currency1);

    /// <summary>Backwards compatibility with the v2/v3 SDKs.</summary>
    public bool InvolvesToken(BaseCurrency currency) => InvolvesCurrency(currency);

    /// <summary>v4-only convenience, used for the mixed-route ETH &lt;-&gt; WETH connection only.</summary>
    public bool V4InvolvesToken(BaseCurrency currency)
    {
        return InvolvesCurrency(currency) ||
               currency.Wrapped().Equals(Currency0) ||
               currency.Wrapped().Equals(Currency1) ||
               currency.Wrapped().Equals(Currency0.Wrapped()) ||
               currency.Wrapped().Equals(Currency1.Wrapped());
    }

    public Price<BaseCurrency, BaseCurrency> Currency0Price =>
        _currency0Price ??= new Price<BaseCurrency, BaseCurrency>(Currency0, Currency1, Constants.Q192, SqrtRatioX96 * SqrtRatioX96);

    /// <summary>Backwards compatibility with the v2/v3 SDKs.</summary>
    public Price<BaseCurrency, BaseCurrency> Token0Price => Currency0Price;

    public Price<BaseCurrency, BaseCurrency> Currency1Price =>
        _currency1Price ??= new Price<BaseCurrency, BaseCurrency>(Currency1, Currency0, SqrtRatioX96 * SqrtRatioX96, Constants.Q192);

    /// <summary>Backwards compatibility with the v2/v3 SDKs.</summary>
    public Price<BaseCurrency, BaseCurrency> Token1Price => Currency1Price;

    public Price<BaseCurrency, BaseCurrency> PriceOf(BaseCurrency currency)
    {
        if (!InvolvesCurrency(currency))
        {
            throw new ArgumentException("CURRENCY");
        }
        return currency.Equals(Currency0) ? Currency0Price : Currency1Price;
    }

    public int ChainId => Currency0.ChainId;

    public async Task<(CurrencyAmount<BaseCurrency> outputAmount, Pool pool)> GetOutputAmount(
        CurrencyAmount<BaseCurrency> inputAmount, BigInteger? sqrtPriceLimitX96 = null)
    {
        if (!InvolvesCurrency(inputAmount.Currency))
        {
            throw new ArgumentException("CURRENCY");
        }

        bool zeroForOne = inputAmount.Currency.Equals(Currency0);
        var (outputAmount, sqrtRatioX96, liquidity, tickCurrent) = await Swap(zeroForOne, inputAmount.Quotient, sqrtPriceLimitX96);
        var outputCurrency = zeroForOne ? Currency1 : Currency0;

        return (
            CurrencyAmount<BaseCurrency>.FromRawAmount(outputCurrency, outputAmount * Constants.NEGATIVE_ONE),
            new Pool(Currency0, Currency1, Fee, TickSpacing, Hooks, sqrtRatioX96, liquidity, tickCurrent, TickDataProvider));
    }

    public async Task<(CurrencyAmount<BaseCurrency> inputAmount, Pool pool)> GetInputAmount(
        CurrencyAmount<BaseCurrency> outputAmount, BigInteger? sqrtPriceLimitX96 = null)
    {
        if (!InvolvesCurrency(outputAmount.Currency))
        {
            throw new ArgumentException("CURRENCY");
        }

        bool zeroForOne = outputAmount.Currency.Equals(Currency1);
        var (inputAmount, sqrtRatioX96, liquidity, tickCurrent) = await Swap(zeroForOne, outputAmount.Quotient * Constants.NEGATIVE_ONE, sqrtPriceLimitX96);
        var inputCurrency = zeroForOne ? Currency0 : Currency1;

        return (
            CurrencyAmount<BaseCurrency>.FromRawAmount(inputCurrency, inputAmount),
            new Pool(Currency0, Currency1, Fee, TickSpacing, Hooks, sqrtRatioX96, liquidity, tickCurrent, TickDataProvider));
    }

    private async Task<(BigInteger amountCalculated, BigInteger sqrtRatioX96, BigInteger liquidity, int tickCurrent)> Swap(
        bool zeroForOne, BigInteger amountSpecified, BigInteger? sqrtPriceLimitX96 = null)
    {
        if (HookImpactsSwap())
        {
            throw new InvalidOperationException("Unsupported hook");
        }

        return await V3Swap.ExecuteAsync(
            Fee, SqrtRatioX96, TickCurrent, Liquidity, TickSpacing, TickDataProvider, zeroForOne, amountSpecified, sqrtPriceLimitX96);
    }

    private bool HookImpactsSwap() => Hook.HasSwapPermissions(Hooks);

    private static bool IsAddress(string address)
    {
        string candidate = address.StartsWith("0x") || address.StartsWith("0X") ? address : "0x" + address;
        try
        {
            AddressValidator.ValidateAndParseAddress(candidate);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // Upstream: `Number(hooks) > 0` — the hook address is non-zero.
    private static bool IsNonZeroAddress(string address)
    {
        string hex = address.StartsWith("0x") || address.StartsWith("0X") ? address[2..] : address;
        return hex.Any(c => c != '0');
    }
}
