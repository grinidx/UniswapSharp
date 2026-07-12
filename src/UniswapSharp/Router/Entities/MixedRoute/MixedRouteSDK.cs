using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.Router.Utils;
using V4Pool = UniswapSharp.V4.Entities.Pool;

namespace UniswapSharp.Router.Entities.MixedRoute;

/// <summary>
/// Port of router-sdk <c>entities/mixedRoute/route.ts</c>. Represents a list of pools or pairs
/// (a <c>TPool</c> union) through which a swap can occur.
/// </summary>
public class MixedRouteSDK<TInput, TOutput>
    where TInput : BaseCurrency
    where TOutput : BaseCurrency
{
    public List<object> Pools { get; }
    public List<BaseCurrency> Path { get; }
    public TInput Input { get; }
    public TOutput Output { get; }

    /// <summary>Routes may need to wrap/unwrap a currency to begin trading the path.</summary>
    public BaseCurrency PathInput { get; }

    /// <summary>Routes may need to wrap/unwrap a currency at the end of the trading path.</summary>
    public BaseCurrency PathOutput { get; }

    private Price<TInput, TOutput>? _midPrice;

    /// <param name="pools">Ordered pools/pairs the swap will take.</param>
    /// <param name="input">The input currency.</param>
    /// <param name="output">The output currency.</param>
    /// <param name="retainFakePools">Set true to retain "fake" eth-weth (tickSpacing 0) v4 pools.</param>
    public MixedRouteSDK(IReadOnlyList<object> pools, TInput input, TOutput output, bool retainFakePools = false)
    {
        var filtered = retainFakePools
            ? pools.ToList()
            : pools.Where(pool => !(pool is V4Pool v4 && v4.TickSpacing == 0)).ToList();

        if (filtered.Count == 0)
        {
            throw new ArgumentException("POOLS");
        }

        int chainId = TPool.ChainId(filtered[0]);
        if (!filtered.All(pool => TPool.ChainId(pool) == chainId))
        {
            throw new ArgumentException("CHAIN_IDS");
        }

        PathInput = PathCurrency.GetPathCurrency(input, filtered[0]);
        PathOutput = PathCurrency.GetPathCurrency(output, filtered[^1]);

        if (filtered[0] is not V4Pool)
        {
            if (!TPool.InvolvesToken(filtered[0], PathInput))
            {
                throw new ArgumentException("INPUT");
            }
        }
        else if (!TPool.V4InvolvesToken(filtered[0], PathInput))
        {
            throw new ArgumentException("INPUT");
        }

        var lastPool = filtered[^1];
        if (lastPool is V4Pool)
        {
            if (!TPool.V4InvolvesToken(lastPool, output) && !TPool.V4InvolvesToken(lastPool, output.Wrapped()))
            {
                throw new ArgumentException("OUTPUT");
            }
        }
        else if (!TPool.InvolvesToken(lastPool, output.Wrapped()))
        {
            throw new ArgumentException("OUTPUT");
        }

        // Normalises token0-token1 order and selects the next token/fee step to add to the path.
        var tokenPath = new List<BaseCurrency> { PathInput };
        tokenPath.Add(TPool.Token0(filtered[0]).Equals(PathInput) ? TPool.Token1(filtered[0]) : TPool.Token0(filtered[0]));

        for (int i = 1; i < filtered.Count; i++)
        {
            var pool = filtered[i];
            BaseCurrency inputToken = tokenPath[i];

            BaseCurrency outputToken;
            if ((pool is V4Pool && !TPool.InvolvesToken(pool, inputToken)) ||
                (pool is not V4Pool && inputToken.IsNative))
            {
                // Handle inputToken != pool.token0/token1. Two specific cases.
                if (inputToken.Equals(TPool.Token0(pool).Wrapped()))
                {
                    // 1) inputToken is WETH and the current pool has ETH
                    outputToken = TPool.Token1(pool);
                }
                else if (inputToken.Wrapped().Equals(TPool.Token0(pool)) || inputToken.Wrapped().Equals(TPool.Token1(pool)))
                {
                    // 2) inputToken is ETH and the current pool has WETH
                    outputToken = inputToken.Wrapped().Equals(TPool.Token0(pool)) ? TPool.Token1(pool) : TPool.Token0(pool);
                }
                else
                {
                    throw new ArgumentException($"POOL_MISMATCH pool inputToken {inputToken.Symbol}");
                }
            }
            else
            {
                // the input token must equal either token0 or token1
                if (!inputToken.Equals(TPool.Token0(pool)) && !inputToken.Equals(TPool.Token1(pool)))
                {
                    throw new ArgumentException("PATH");
                }
                outputToken = inputToken.Equals(TPool.Token0(pool)) ? TPool.Token1(pool) : TPool.Token0(pool);
            }
            tokenPath.Add(outputToken);
        }

        Pools = filtered;
        Path = tokenPath;
        Input = input;
        Output = output;
    }

    public int ChainId => TPool.ChainId(Pools[0]);

    /// <summary>Returns the mid price of the route.</summary>
    public Price<TInput, TOutput> MidPrice
    {
        get
        {
            if (_midPrice != null)
            {
                return _midPrice;
            }

            var start = TPool.Token0(Pools[0]).Equals(PathInput)
                ? (NextInput: TPool.Token1(Pools[0]), Price: TPool.Token0Price(Pools[0]).AsFraction())
                : (NextInput: TPool.Token0(Pools[0]), Price: TPool.Token1Price(Pools[0]).AsFraction());

            var result = Pools.Skip(1).Aggregate(start, (acc, pool) =>
                acc.NextInput.Equals(TPool.Token0(pool))
                    ? (NextInput: TPool.Token1(pool), Price: acc.Price.Multiply(TPool.Token0Price(pool).AsFraction()))
                    : (NextInput: TPool.Token0(pool), Price: acc.Price.Multiply(TPool.Token1Price(pool).AsFraction())));

            _midPrice = new Price<TInput, TOutput>(Input, Output, result.Price.Denominator, result.Price.Numerator);
            return _midPrice;
        }
    }
}
