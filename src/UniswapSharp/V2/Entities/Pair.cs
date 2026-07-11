using System.Numerics;
using Nethereum.ABI;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.Core.Utils;

namespace UniswapSharp.V2.Entities;

/// <summary>
/// Port of v2-sdk <c>entities/pair.ts</c>.
/// </summary>
public class Pair
{
    public Token LiquidityToken { get; }
    private readonly (CurrencyAmount<Token> Token0, CurrencyAmount<Token> Token1) _tokenAmounts;

    /// <summary>
    /// Computes the CREATE2 address of the pair for the given factory and tokens.
    /// </summary>
    public static string ComputePairAddress(string factoryAddress, Token tokenA, Token tokenB)
    {
        var (token0, token1) = tokenA.SortsBefore(tokenB) ? (tokenA, tokenB) : (tokenB, tokenA); // does safety checks

        // solidityKeccak256(['bytes'], [pack(['address', 'address'], [token0, token1])]) — the tokens are
        // tightly (non-padded) packed, so the salt is keccak256 of the 40-byte concatenation.
        var abiEncoder = new ABIEncode();
        var packed = abiEncoder.GetABIEncodedPacked(
            new ABIValue("address", token0.Address),
            new ABIValue("address", token1.Address)
        );
        var salt = Sha3Keccack.Current.CalculateHash(packed);

        return AddressValidator.GetCreate2Address(factoryAddress, salt, Constants.INIT_CODE_HASH.HexToByteArray());
    }

    public static string GetAddress(Token tokenA, Token tokenB)
    {
        var factoryAddress = Constants.FACTORY_ADDRESS_MAP.TryGetValue((ChainId)tokenA.ChainId, out var mapped)
            ? mapped
            : Constants.FACTORY_ADDRESS;
        return ComputePairAddress(factoryAddress, tokenA, tokenB);
    }

    public Pair(CurrencyAmount<Token> currencyAmountA, CurrencyAmount<Token> tokenAmountB)
    {
        var tokenAmounts = currencyAmountA.Currency.SortsBefore(tokenAmountB.Currency) // does safety checks
            ? (currencyAmountA, tokenAmountB)
            : (tokenAmountB, currencyAmountA);
        LiquidityToken = new Token(
            tokenAmounts.Item1.Currency.ChainId,
            GetAddress(tokenAmounts.Item1.Currency, tokenAmounts.Item2.Currency),
            18,
            "UNI-V2",
            "Uniswap V2"
        );
        _tokenAmounts = tokenAmounts;
    }

    /// <summary>
    /// Returns true if the token is either token0 or token1.
    /// </summary>
    public bool InvolvesToken(Token token)
    {
        return token.Equals(Token0) || token.Equals(Token1);
    }

    /// <summary>
    /// Returns the current mid price of the pair in terms of token0, i.e. the ratio of reserve1 to reserve0.
    /// </summary>
    public Price<Token, Token> Token0Price
    {
        get
        {
            var result = _tokenAmounts.Token1.Divide(_tokenAmounts.Token0);
            return new Price<Token, Token>(Token0, Token1, result.Denominator, result.Numerator);
        }
    }

    /// <summary>
    /// Returns the current mid price of the pair in terms of token1, i.e. the ratio of reserve0 to reserve1.
    /// </summary>
    public Price<Token, Token> Token1Price
    {
        get
        {
            var result = _tokenAmounts.Token0.Divide(_tokenAmounts.Token1);
            return new Price<Token, Token>(Token1, Token0, result.Denominator, result.Numerator);
        }
    }

    /// <summary>
    /// Return the price of the given token in terms of the other token in the pair.
    /// </summary>
    public Price<Token, Token> PriceOf(Token token)
    {
        if (!InvolvesToken(token))
        {
            throw new ArgumentException("TOKEN");
        }
        return token.Equals(Token0) ? Token0Price : Token1Price;
    }

    /// <summary>
    /// Returns the chain ID of the tokens in the pair.
    /// </summary>
    public int ChainId => Token0.ChainId;

    public Token Token0 => _tokenAmounts.Token0.Currency;

    public Token Token1 => _tokenAmounts.Token1.Currency;

    public CurrencyAmount<Token> Reserve0 => _tokenAmounts.Token0;

    public CurrencyAmount<Token> Reserve1 => _tokenAmounts.Token1;

    public CurrencyAmount<Token> ReserveOf(Token token)
    {
        if (!InvolvesToken(token))
        {
            throw new ArgumentException("TOKEN");
        }
        return token.Equals(Token0) ? Reserve0 : Reserve1;
    }

    public (CurrencyAmount<Token> OutputAmount, Pair Pair) GetOutputAmount(
        CurrencyAmount<Token> inputAmount,
        bool calculateFotFees = true)
    {
        if (!InvolvesToken(inputAmount.Currency))
        {
            throw new ArgumentException("TOKEN");
        }
        if (Reserve0.Quotient.IsZero || Reserve1.Quotient.IsZero)
        {
            throw new InsufficientReservesError();
        }

        var inputReserve = ReserveOf(inputAmount.Currency);
        var outputReserve = ReserveOf(inputAmount.Currency.Equals(Token0) ? Token1 : Token0);

        var percentAfterSellFees = calculateFotFees ? DerivePercentAfterSellFees(inputAmount) : Constants.ZERO_PERCENT;
        var inputAmountAfterTax = percentAfterSellFees.GreaterThan(Constants.ZERO_PERCENT)
            ? CurrencyAmount<Token>.FromRawAmount(
                inputAmount.Currency,
                percentAfterSellFees.Multiply(inputAmount).Quotient // fraction.quotient will round down by itself, which is desired
            )
            : inputAmount;

        var inputAmountWithFeeAndAfterTax = inputAmountAfterTax.Quotient * Constants._997;
        var numerator = inputAmountWithFeeAndAfterTax * outputReserve.Quotient;
        var denominator = inputReserve.Quotient * Constants._1000 + inputAmountWithFeeAndAfterTax;
        var outputAmount = CurrencyAmount<Token>.FromRawAmount(
            inputAmount.Currency.Equals(Token0) ? Token1 : Token0,
            numerator / denominator // BigInteger division truncates toward zero (== floor for non-negatives), which is desired
        );

        if (outputAmount.Quotient.IsZero)
        {
            throw new InsufficientInputAmountError();
        }

        var percentAfterBuyFees = calculateFotFees ? DerivePercentAfterBuyFees(outputAmount) : Constants.ZERO_PERCENT;
        var outputAmountAfterTax = percentAfterBuyFees.GreaterThan(Constants.ZERO_PERCENT)
            ? CurrencyAmount<Token>.FromRawAmount(
                outputAmount.Currency,
                outputAmount.Multiply(percentAfterBuyFees).Quotient // fraction.quotient will round down by itself, which is desired
            )
            : outputAmount;
        if (outputAmountAfterTax.Quotient.IsZero)
        {
            throw new InsufficientInputAmountError();
        }

        return (
            outputAmountAfterTax,
            new Pair(inputReserve.Add(inputAmountAfterTax), outputReserve.Subtract(outputAmountAfterTax))
        );
    }

    public (CurrencyAmount<Token> InputAmount, Pair Pair) GetInputAmount(
        CurrencyAmount<Token> outputAmount,
        bool calculateFotFees = true)
    {
        if (!InvolvesToken(outputAmount.Currency))
        {
            throw new ArgumentException("TOKEN");
        }

        var percentAfterBuyFees = calculateFotFees ? DerivePercentAfterBuyFees(outputAmount) : Constants.ZERO_PERCENT;
        var outputAmountBeforeTax = percentAfterBuyFees.GreaterThan(Constants.ZERO_PERCENT)
            ? CurrencyAmount<Token>.FromRawAmount(
                outputAmount.Currency,
                outputAmount.Divide(percentAfterBuyFees).Quotient + Constants.ONE // add 1 for rounding up
            )
            : outputAmount;

        if (Reserve0.Quotient.IsZero ||
            Reserve1.Quotient.IsZero ||
            outputAmount.Quotient >= ReserveOf(outputAmount.Currency).Quotient ||
            outputAmountBeforeTax.Quotient >= ReserveOf(outputAmount.Currency).Quotient)
        {
            throw new InsufficientReservesError();
        }

        var outputReserve = ReserveOf(outputAmount.Currency);
        var inputReserve = ReserveOf(outputAmount.Currency.Equals(Token0) ? Token1 : Token0);

        var numerator = inputReserve.Quotient * outputAmountBeforeTax.Quotient * Constants._1000;
        var denominator = (outputReserve.Quotient - outputAmountBeforeTax.Quotient) * Constants._997;
        var inputAmount = CurrencyAmount<Token>.FromRawAmount(
            outputAmount.Currency.Equals(Token0) ? Token1 : Token0,
            numerator / denominator + Constants.ONE // add 1 here is part of the formula, no rounding needed here
        );

        var percentAfterSellFees = calculateFotFees ? DerivePercentAfterSellFees(inputAmount) : Constants.ZERO_PERCENT;
        var inputAmountBeforeTax = percentAfterSellFees.GreaterThan(Constants.ZERO_PERCENT)
            ? CurrencyAmount<Token>.FromRawAmount(
                inputAmount.Currency,
                inputAmount.Divide(percentAfterSellFees).Quotient + Constants.ONE // add 1 for rounding up
            )
            : inputAmount;
        return (
            inputAmountBeforeTax,
            new Pair(inputReserve.Add(inputAmount), outputReserve.Subtract(outputAmount))
        );
    }

    public CurrencyAmount<Token> GetLiquidityMinted(
        CurrencyAmount<Token> totalSupply,
        CurrencyAmount<Token> tokenAmountA,
        CurrencyAmount<Token> tokenAmountB)
    {
        if (!totalSupply.Currency.Equals(LiquidityToken))
        {
            throw new ArgumentException("LIQUIDITY");
        }
        var tokenAmounts = tokenAmountA.Currency.SortsBefore(tokenAmountB.Currency) // does safety checks
            ? (tokenAmountA, tokenAmountB)
            : (tokenAmountB, tokenAmountA);
        if (!tokenAmounts.Item1.Currency.Equals(Token0) || !tokenAmounts.Item2.Currency.Equals(Token1))
        {
            throw new ArgumentException("TOKEN");
        }

        BigInteger liquidity;
        if (totalSupply.Quotient.IsZero)
        {
            liquidity = MathUtils.Sqrt(tokenAmounts.Item1.Quotient * tokenAmounts.Item2.Quotient) - Constants.MINIMUM_LIQUIDITY;
        }
        else
        {
            var amount0 = tokenAmounts.Item1.Quotient * totalSupply.Quotient / Reserve0.Quotient;
            var amount1 = tokenAmounts.Item2.Quotient * totalSupply.Quotient / Reserve1.Quotient;
            liquidity = amount0 <= amount1 ? amount0 : amount1;
        }
        if (!(liquidity > Constants.ZERO))
        {
            throw new InsufficientInputAmountError();
        }
        return CurrencyAmount<Token>.FromRawAmount(LiquidityToken, liquidity);
    }

    public CurrencyAmount<Token> GetLiquidityValue(
        Token token,
        CurrencyAmount<Token> totalSupply,
        CurrencyAmount<Token> liquidity,
        bool feeOn = false,
        BigInteger? kLast = null)
    {
        if (!InvolvesToken(token))
        {
            throw new ArgumentException("TOKEN");
        }
        if (!totalSupply.Currency.Equals(LiquidityToken))
        {
            throw new ArgumentException("TOTAL_SUPPLY");
        }
        if (!liquidity.Currency.Equals(LiquidityToken))
        {
            throw new ArgumentException("LIQUIDITY");
        }
        if (liquidity.Quotient > totalSupply.Quotient)
        {
            throw new ArgumentException("LIQUIDITY");
        }

        CurrencyAmount<Token> totalSupplyAdjusted;
        if (!feeOn)
        {
            totalSupplyAdjusted = totalSupply;
        }
        else
        {
            if (kLast is null)
            {
                throw new ArgumentException("K_LAST");
            }
            var kLastParsed = kLast.Value;
            if (!kLastParsed.IsZero)
            {
                var rootK = MathUtils.Sqrt(Reserve0.Quotient * Reserve1.Quotient);
                var rootKLast = MathUtils.Sqrt(kLastParsed);
                if (rootK > rootKLast)
                {
                    var numerator = totalSupply.Quotient * (rootK - rootKLast);
                    var denominator = rootK * Constants.FIVE + rootKLast;
                    var feeLiquidity = numerator / denominator;
                    totalSupplyAdjusted = totalSupply.Add(CurrencyAmount<Token>.FromRawAmount(LiquidityToken, feeLiquidity));
                }
                else
                {
                    totalSupplyAdjusted = totalSupply;
                }
            }
            else
            {
                totalSupplyAdjusted = totalSupply;
            }
        }

        return CurrencyAmount<Token>.FromRawAmount(
            token,
            liquidity.Quotient * ReserveOf(token).Quotient / totalSupplyAdjusted.Quotient
        );
    }

    private Percent DerivePercentAfterSellFees(CurrencyAmount<Token> inputAmount)
    {
        var sellFeeBps = Token0.Equals(inputAmount.Currency) ? Token0.SellFeeBps : Token1.SellFeeBps;
        if (sellFeeBps.HasValue && sellFeeBps.Value > BigInteger.Zero)
        {
            return Constants.ONE_HUNDRED_PERCENT.Subtract(new Percent(sellFeeBps.Value).Divide(Constants.BASIS_POINTS));
        }
        return Constants.ZERO_PERCENT;
    }

    private Percent DerivePercentAfterBuyFees(CurrencyAmount<Token> outputAmount)
    {
        var buyFeeBps = Token0.Equals(outputAmount.Currency) ? Token0.BuyFeeBps : Token1.BuyFeeBps;
        if (buyFeeBps.HasValue && buyFeeBps.Value > BigInteger.Zero)
        {
            return Constants.ONE_HUNDRED_PERCENT.Subtract(new Percent(buyFeeBps.Value).Divide(Constants.BASIS_POINTS));
        }
        return Constants.ZERO_PERCENT;
    }
}
