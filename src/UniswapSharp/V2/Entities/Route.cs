using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;

namespace UniswapSharp.V2.Entities;

/// <summary>
/// Port of v2-sdk <c>entities/route.ts</c>.
/// </summary>
public class Route<TInput, TOutput>
    where TInput : BaseCurrency
    where TOutput : BaseCurrency
{
    public List<Pair> Pairs { get; }
    public List<Token> Path { get; }
    public TInput Input { get; }
    public TOutput Output { get; }

    private Price<TInput, TOutput>? _midPrice;

    public Route(List<Pair> pairs, TInput input, TOutput output)
    {
        if (pairs.Count == 0)
        {
            throw new ArgumentException("PAIRS");
        }

        var chainId = pairs[0].ChainId;
        if (!pairs.All(pair => pair.ChainId == chainId))
        {
            throw new ArgumentException("CHAIN_IDS");
        }

        var wrappedInput = input.Wrapped();
        if (!pairs[0].InvolvesToken(wrappedInput))
        {
            throw new ArgumentException("INPUT");
        }
        if (!pairs[^1].InvolvesToken(output.Wrapped()))
        {
            throw new ArgumentException("OUTPUT");
        }

        var path = new List<Token> { wrappedInput };
        for (var i = 0; i < pairs.Count; i++)
        {
            var currentInput = path[i];
            if (!currentInput.Equals(pairs[i].Token0) && !currentInput.Equals(pairs[i].Token1))
            {
                throw new ArgumentException("PATH");
            }
            var output2 = currentInput.Equals(pairs[i].Token0) ? pairs[i].Token1 : pairs[i].Token0;
            path.Add(output2);
        }

        Pairs = pairs;
        Path = path;
        Input = input;
        Output = output;
    }

    public Price<TInput, TOutput> MidPrice
    {
        get
        {
            if (_midPrice != null)
            {
                return _midPrice;
            }

            var prices = new List<Price<Token, Token>>();
            for (var i = 0; i < Pairs.Count; i++)
            {
                var pair = Pairs[i];
                prices.Add(Path[i].Equals(pair.Token0)
                    ? new Price<Token, Token>(pair.Reserve0.Currency, pair.Reserve1.Currency, pair.Reserve0.Quotient, pair.Reserve1.Quotient)
                    : new Price<Token, Token>(pair.Reserve1.Currency, pair.Reserve0.Currency, pair.Reserve1.Quotient, pair.Reserve0.Quotient));
            }

            var reduced = prices[0];
            for (var i = 1; i < prices.Count; i++)
            {
                reduced = reduced.Multiply(prices[i]);
            }

            _midPrice = new Price<TInput, TOutput>(Input, Output, reduced.Denominator, reduced.Numerator);
            return _midPrice;
        }
    }

    public int ChainId => Pairs[0].ChainId;
}
