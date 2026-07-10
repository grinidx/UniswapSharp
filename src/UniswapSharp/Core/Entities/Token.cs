using System.Diagnostics;
using System.Numerics;
using UniswapSharp.Core.Utils;

namespace UniswapSharp.Core.Entities;

/// <summary>
/// Represents an ERC20 token with a unique address and some metadata.
/// </summary>
public class Token : BaseCurrency, IEquatable<Token>
{
    public override bool IsNative { get; } = false;
    public override bool IsToken { get; } = true;

    /// <summary>
    /// The contract address on the chain on which this token lives
    /// </summary>
    public string Address { get; }

    /// <summary>
    /// Relevant for fee-on-transfer (FOT) token taxes,
    /// Not every ERC20 token is FOT token, so this field is optional
    /// </summary>
    public BigInteger? BuyFeeBps { get; }
    public BigInteger? SellFeeBps { get; }

    /// <summary>
    /// Initializes a new instance of the Token class.
    /// </summary>
    /// <param name="chainId">The chain ID on which this token lives</param>
    /// <param name="address">The contract address on the chain on which this token lives</param>
    /// <param name="decimals">The number of decimals for the token</param>
    /// <param name="symbol">The symbol of the token</param>
    /// <param name="name">The name of the token</param>
    /// <param name="bypassChecksum">If true it only checks for length === 42, startsWith 0x and contains only hex characters</param>
    /// <param name="buyFeeBps">Buy fee tax for FOT tokens, in basis points</param>
    /// <param name="sellFeeBps">Sell fee tax for FOT tokens, in basis points</param>
    public Token(
        int chainId,
        string address,
        int decimals,
        string? symbol = null,
        string? name = null,
        bool bypassChecksum = false,
        BigInteger? buyFeeBps = null,
        BigInteger? sellFeeBps = null
    ) : base(chainId, decimals, symbol, name)
    {
        if (bypassChecksum)
        {
            Address = AddressValidator.CheckValidAddress(address);
        }
        else
        {
            Address = AddressValidator.ValidateAndParseAddress(address);
        }

        if (buyFeeBps.HasValue)
        {
            Debug.Assert(buyFeeBps.Value >= BigInteger.Zero, "NON-NEGATIVE FOT FEES");
        }

        if (sellFeeBps.HasValue)
        {
            Debug.Assert(sellFeeBps.Value >= BigInteger.Zero, "NON-NEGATIVE FOT FEES");
        }

        BuyFeeBps = buyFeeBps;
        SellFeeBps = sellFeeBps;
    }

    /// <summary>
    /// Returns true if the two tokens are equivalent, i.e. have the same chainId and address.
    /// </summary>
    /// <param name="other">Other token to compare</param>
    /// <returns>True if the tokens are equivalent, false otherwise</returns>
    public bool Equals(Token? other)
    {
        return other is { IsToken: true } && ChainId == other.ChainId && Address.ToLower() == ((Token)other).Address.ToLower();
    }

    public override bool Equals(BaseCurrency other)
    {
        return other.IsToken && ChainId == other.ChainId && Address.ToLower() == ((Token)other).Address.ToLower();
    }

    /// <summary>
    /// Returns true if the address of this token sorts before the address of the other token
    /// </summary>
    /// <param name="other">Other token to compare</param>
    /// <returns>True if this token sorts before the other token, false otherwise</returns>
    /// <exception cref="InvalidOperationException">Thrown if the tokens have the same address or are on different chains</exception>
    public bool SortsBefore(Token other)
    {
        if (ChainId != other.ChainId)
        {
            throw new InvalidOperationException("CHAIN_IDS");
        }

        if (Address.ToLower() == other.Address.ToLower())
        {
            throw new InvalidOperationException("ADDRESSES");
        }

        return string.Compare(Address.ToLower(), other.Address.ToLower(), StringComparison.Ordinal) < 0;
    }



    /// <summary>
    /// Return this token, which does not need to be wrapped
    /// </summary>
    public override Token Wrapped() => this;


}