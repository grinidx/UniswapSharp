namespace UniswapSharp.Core.Entities;

/// <summary>
/// A currency is any fungible financial instrument, including Ether, all ERC20 tokens, and other chain-native currencies
/// </summary>
public abstract class BaseCurrency
{


    /// <summary>
    /// Returns whether the currency is native to the chain and must be wrapped (e.g. Ether)
    /// </summary>
    public abstract bool IsNative { get; }

    /// <summary>
    /// Returns whether the currency is a token that is usable in Uniswap without wrapping
    /// </summary>
    public abstract bool IsToken { get; }

    /// <summary>
    /// The chain ID on which this currency resides
    /// </summary>
    public int ChainId { get; }

    /// <summary>
    /// The decimals used in representing currency amounts
    /// </summary>
    public int Decimals { get; }

    /// <summary>
    /// The symbol of the currency, i.e. a short textual non-unique identifier
    /// </summary>
    public string? Symbol { get; }

    /// <summary>
    /// The name of the currency, i.e. a descriptive textual non-unique identifier
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// Constructs an instance of the base class `BaseCurrency`.
    /// </summary>
    /// <param name="chainId">the chain ID on which this currency resides</param>
    /// <param name="decimals">decimals of the currency</param>
    /// <param name="symbol">symbol of the currency</param>
    /// <param name="name">name of the currency</param>
    protected BaseCurrency(int chainId, int decimals, string? symbol = null, string? name = null)
    {

        if (decimals < 0 || decimals >= 255) throw new ArgumentException("DECIMALS");

        ChainId = chainId;
        Decimals = decimals;
        Symbol = symbol;
        Name = name;
    }

    /// <summary>
    /// Returns whether this currency is functionally equivalent to the other currency
    /// </summary>
    /// <param name="other">the other currency</param>
    public abstract bool Equals(BaseCurrency other);

    /// <summary>
    /// Return the wrapped version of this currency that can be used with the Uniswap contracts. Currencies must
    /// implement this to be used in Uniswap
    /// </summary>
    public abstract Token Wrapped();


}
