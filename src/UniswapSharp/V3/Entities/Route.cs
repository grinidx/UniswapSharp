using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;

namespace UniswapSharp.V3.Entities;

public class Route<TInput, TOutput> where TInput : BaseCurrency where TOutput : BaseCurrency
{
    public List<Pool> Pools { get; }
    public List<Token> TokenPath { get; }
    public TInput Input { get; }
    public TOutput Output { get; }

    private Price<TInput, TOutput> _midPrice = null;

    public Route(List<Pool> pools, TInput input, TOutput output)
    {
        if (pools.Count == 0)
            throw new ArgumentException("Pools list cannot be empty", nameof(pools));

        int chainId = pools[0].ChainId;
        bool allOnSameChain = pools.All(pool => pool.ChainId == chainId);
        if (!allOnSameChain)
            throw new ArgumentException("All pools must be on the same chain", nameof(pools));

        Token wrappedInput = input.Wrapped();
        if (!pools[0].InvolvesToken(wrappedInput))
            throw new ArgumentException("First pool must involve the input token", nameof(input));

        if (!pools[pools.Count - 1].InvolvesToken(output.Wrapped()))
            throw new ArgumentException("Last pool must involve the output token", nameof(output));

        List<Token> tokenPath = new List<Token> { wrappedInput };
        for (int i = 0; i < pools.Count; i++)
        {
            Token currentInputToken = tokenPath[i];
            if (!currentInputToken.Equals(pools[i].Token0) && !currentInputToken.Equals(pools[i].Token1))
                throw new ArgumentException("Invalid path", nameof(pools));

            Token nextToken = currentInputToken.Equals(pools[i].Token0) ? pools[i].Token1 : pools[i].Token0;
            tokenPath.Add(nextToken);
        }

        Pools = pools;
        TokenPath = tokenPath;
        Input = input;
        Output = output ?? (TOutput)(object)tokenPath[tokenPath.Count - 1];
    }

    public int ChainId => Pools[0].ChainId;

    public Price<TInput, TOutput> MidPrice
    {
        get
        {
            if (_midPrice != null)
                return _midPrice;

            var result = Pools.Skip(1).Aggregate(
                Pools[0].Token0.Equals(Input.Wrapped())
                    ? new { NextInput = Pools[0].Token1, Price = Pools[0].Token0Price }
                    : new { NextInput = Pools[0].Token0, Price = Pools[0].Token1Price },
                (acc, pool) =>
                    acc.NextInput.Equals(pool.Token0)
                        ? new { NextInput = pool.Token1, Price = acc.Price.Multiply(pool.Token0Price) }
                        : new { NextInput = pool.Token0, Price = acc.Price.Multiply(pool.Token1Price) }
            );

            _midPrice = new Price<TInput, TOutput>(Input, Output, result.Price.Denominator, result.Price.Numerator);
            return _midPrice;
        }
    }
}