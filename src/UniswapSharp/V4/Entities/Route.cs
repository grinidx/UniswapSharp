using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.V4.Utils;

namespace UniswapSharp.V4.Entities;

/// <summary>
/// Represents a list of V4 pools through which a swap can occur. Ported from v4-sdk/src/entities/route.ts.
/// </summary>
public class Route<TInput, TOutput> where TInput : BaseCurrency where TOutput : BaseCurrency
{
    public List<Pool> Pools { get; }
    public List<BaseCurrency> CurrencyPath { get; }
    public TInput Input { get; }
    public TOutput Output { get; }

    /// <summary>Equivalent, or wrapped/unwrapped, input that matches the first pool.</summary>
    public BaseCurrency PathInput { get; }

    /// <summary>Equivalent, or wrapped/unwrapped, output that matches the last pool.</summary>
    public BaseCurrency PathOutput { get; }

    private Price<TInput, TOutput>? _midPrice;

    public Route(List<Pool> pools, TInput input, TOutput output)
    {
        if (pools.Count == 0)
        {
            throw new ArgumentException("POOLS");
        }

        int chainId = pools[0].ChainId;
        if (!pools.All(pool => pool.ChainId == chainId))
        {
            throw new ArgumentException("CHAIN_IDS");
        }

        // Throws if the pools do not involve the input/output currency (or the native/wrapped equivalent).
        PathInput = PathCurrency.GetPathCurrency(input, pools[0]);
        PathOutput = PathCurrency.GetPathCurrency(output, pools[^1]);

        // If the input is native and the first pool is an eth-weth pool, the input has already been wrapped
        // to weth, so the path input must be set to the wrapped input.
        if (pools[0].Currency0.Wrapped().Equals(pools[0].Currency1))
        {
            if (PathInput.IsNative && pools.Count > 1 && pools[1].Currency0.IsNative)
            {
                PathInput = pools[0].Currency1;
            }
            else if (PathInput.Equals(pools[0].Currency1) && pools.Count > 1 && !pools[1].Currency0.IsNative)
            {
                PathInput = pools[0].Currency0;
            }
        }

        var currencyPath = new List<BaseCurrency> { PathInput };
        for (int i = 0; i < pools.Count; i++)
        {
            BaseCurrency currentInputCurrency = currencyPath[i];
            if (!currentInputCurrency.Equals(pools[i].Currency0) && !currentInputCurrency.Equals(pools[i].Currency1))
            {
                throw new ArgumentException("PATH");
            }
            BaseCurrency nextCurrency = currentInputCurrency.Equals(pools[i].Currency0) ? pools[i].Currency1 : pools[i].Currency0;
            currencyPath.Add(nextCurrency);
        }

        Pools = pools;
        CurrencyPath = currencyPath;
        Input = input;
        Output = output;
    }

    public int ChainId => Pools[0].ChainId;

    public Price<TInput, TOutput> MidPrice
    {
        get
        {
            if (_midPrice != null)
            {
                return _midPrice;
            }

            var start = Pools[0].Currency0.Equals(PathInput)
                ? (NextInput: Pools[0].Currency1, Price: Pools[0].Currency0Price)
                : (NextInput: Pools[0].Currency0, Price: Pools[0].Currency1Price);

            var result = Pools.Skip(1).Aggregate(start, (acc, pool) =>
                acc.NextInput.Equals(pool.Currency0)
                    ? (NextInput: pool.Currency1, Price: acc.Price.Multiply(pool.Currency0Price))
                    : (NextInput: pool.Currency0, Price: acc.Price.Multiply(pool.Currency1Price)));

            _midPrice = new Price<TInput, TOutput>(Input, Output, result.Price.Denominator, result.Price.Numerator);
            return _midPrice;
        }
    }
}
