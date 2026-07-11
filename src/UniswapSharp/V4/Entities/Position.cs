using System.Numerics;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.V3.Utils;
using UniswapSharp.V4.Utils;

namespace UniswapSharp.V4.Entities;

/// <summary>
/// Represents a position on a Uniswap V4 pool. Ported from v4-sdk/src/entities/position.ts —
/// similar to V3 but using <see cref="BaseCurrency"/>, and with a v4-specific slippage derivation.
/// </summary>
public class Position
{
    public Pool Pool { get; }
    public int TickLower { get; }
    public int TickUpper { get; }
    public BigInteger Liquidity { get; }

    private CurrencyAmount<BaseCurrency>? _token0Amount;
    private CurrencyAmount<BaseCurrency>? _token1Amount;

    public Position(Pool pool, BigInteger liquidity, int tickLower, int tickUpper)
    {
        if (tickLower >= tickUpper)
        {
            throw new ArgumentException("TICK_ORDER");
        }
        if (tickLower < TickMath.MIN_TICK || tickLower % pool.TickSpacing != 0)
        {
            throw new ArgumentException("TICK_LOWER");
        }
        if (tickUpper > TickMath.MAX_TICK || tickUpper % pool.TickSpacing != 0)
        {
            throw new ArgumentException("TICK_UPPER");
        }

        Pool = pool;
        Liquidity = liquidity;
        TickLower = tickLower;
        TickUpper = tickUpper;
    }

    public Price<BaseCurrency, BaseCurrency> Token0PriceLower => UniswapSharp.V4.Utils.PriceTick.TickToPrice(Pool.Currency0, Pool.Currency1, TickLower);
    public Price<BaseCurrency, BaseCurrency> Token0PriceUpper => UniswapSharp.V4.Utils.PriceTick.TickToPrice(Pool.Currency0, Pool.Currency1, TickUpper);

    public CurrencyAmount<BaseCurrency> Amount0
    {
        get
        {
            if (_token0Amount == null)
            {
                if (Pool.TickCurrent < TickLower)
                {
                    _token0Amount = CurrencyAmount<BaseCurrency>.FromRawAmount(Pool.Currency0,
                        SqrtPriceMath.GetAmount0Delta(TickMath.GetSqrtRatioAtTick(TickLower), TickMath.GetSqrtRatioAtTick(TickUpper), Liquidity, false));
                }
                else if (Pool.TickCurrent < TickUpper)
                {
                    _token0Amount = CurrencyAmount<BaseCurrency>.FromRawAmount(Pool.Currency0,
                        SqrtPriceMath.GetAmount0Delta(Pool.SqrtRatioX96, TickMath.GetSqrtRatioAtTick(TickUpper), Liquidity, false));
                }
                else
                {
                    _token0Amount = CurrencyAmount<BaseCurrency>.FromRawAmount(Pool.Currency0, Constants.ZERO);
                }
            }
            return _token0Amount;
        }
    }

    public CurrencyAmount<BaseCurrency> Amount1
    {
        get
        {
            if (_token1Amount == null)
            {
                if (Pool.TickCurrent < TickLower)
                {
                    _token1Amount = CurrencyAmount<BaseCurrency>.FromRawAmount(Pool.Currency1, Constants.ZERO);
                }
                else if (Pool.TickCurrent < TickUpper)
                {
                    _token1Amount = CurrencyAmount<BaseCurrency>.FromRawAmount(Pool.Currency1,
                        SqrtPriceMath.GetAmount1Delta(TickMath.GetSqrtRatioAtTick(TickLower), Pool.SqrtRatioX96, Liquidity, false));
                }
                else
                {
                    _token1Amount = CurrencyAmount<BaseCurrency>.FromRawAmount(Pool.Currency1,
                        SqrtPriceMath.GetAmount1Delta(TickMath.GetSqrtRatioAtTick(TickLower), TickMath.GetSqrtRatioAtTick(TickUpper), Liquidity, false));
                }
            }
            return _token1Amount;
        }
    }

    // Returns (lower, upper) sqrt ratios after the price 'slips' up to the slippage tolerance.
    private (BigInteger Lower, BigInteger Upper) RatiosAfterSlippage(Percent slippageTolerance)
    {
        var priceLower = Pool.Token0Price.AsFraction().Multiply(new Percent(1).Subtract(slippageTolerance));
        var priceUpper = Pool.Token0Price.AsFraction().Multiply(slippageTolerance.Add(1));

        BigInteger sqrtRatioX96Lower = EncodeSqrtRatioX96.Encode(priceLower.Numerator, priceLower.Denominator);
        if (sqrtRatioX96Lower <= TickMath.MIN_SQRT_RATIO)
        {
            sqrtRatioX96Lower = TickMath.MIN_SQRT_RATIO + 1;
        }

        BigInteger sqrtRatioX96Upper = EncodeSqrtRatioX96.Encode(priceUpper.Numerator, priceUpper.Denominator);
        if (sqrtRatioX96Upper >= TickMath.MAX_SQRT_RATIO)
        {
            sqrtRatioX96Upper = TickMath.MAX_SQRT_RATIO - 1;
        }

        return (sqrtRatioX96Lower, sqrtRatioX96Upper);
    }

    private Pool CounterfactualPool(BigInteger sqrtRatioX96) =>
        new(Pool.Currency0, Pool.Currency1, Pool.Fee, Pool.TickSpacing, Pool.Hooks, sqrtRatioX96, 0, TickMath.GetTickAtSqrtRatio(sqrtRatioX96));

    /// <summary>
    /// The maximum amounts that must be sent to safely mint the position's liquidity within the slippage tolerance.
    /// Unlike V3, v4 bounds minting/increasing by the MAXIMUM amounts, using this position's precise liquidity.
    /// </summary>
    public (BigInteger amount0, BigInteger amount1) MintAmountsWithSlippage(Percent slippageTolerance)
    {
        var (sqrtRatioX96Lower, sqrtRatioX96Upper) = RatiosAfterSlippage(slippageTolerance);

        // The largest amount1 happens at the upper price; the largest amount0 at the lower price.
        var amount1 = new Position(CounterfactualPool(sqrtRatioX96Upper), Liquidity, TickLower, TickUpper).MintAmounts.amount1;
        var amount0 = new Position(CounterfactualPool(sqrtRatioX96Lower), Liquidity, TickLower, TickUpper).MintAmounts.amount0;

        return (amount0, amount1);
    }

    /// <summary>The minimum amounts that should be requested to safely burn the position's liquidity within slippage.</summary>
    public (BigInteger amount0, BigInteger amount1) BurnAmountsWithSlippage(Percent slippageTolerance)
    {
        var (sqrtRatioX96Lower, sqrtRatioX96Upper) = RatiosAfterSlippage(slippageTolerance);

        // The smallest amount0 happens at the upper price; the smallest amount1 at the lower price.
        var amount0 = new Position(CounterfactualPool(sqrtRatioX96Upper), Liquidity, TickLower, TickUpper).Amount0;
        var amount1 = new Position(CounterfactualPool(sqrtRatioX96Lower), Liquidity, TickLower, TickUpper).Amount1;

        return (amount0.Quotient, amount1.Quotient);
    }

    /// <summary>The minimum amounts that must be sent to mint the position's liquidity at the current pool price.</summary>
    public (BigInteger amount0, BigInteger amount1) MintAmounts
    {
        get
        {
            if (Pool.TickCurrent < TickLower)
            {
                return (SqrtPriceMath.GetAmount0Delta(TickMath.GetSqrtRatioAtTick(TickLower), TickMath.GetSqrtRatioAtTick(TickUpper), Liquidity, true), Constants.ZERO);
            }
            else if (Pool.TickCurrent < TickUpper)
            {
                return (
                    SqrtPriceMath.GetAmount0Delta(Pool.SqrtRatioX96, TickMath.GetSqrtRatioAtTick(TickUpper), Liquidity, true),
                    SqrtPriceMath.GetAmount1Delta(TickMath.GetSqrtRatioAtTick(TickLower), Pool.SqrtRatioX96, Liquidity, true));
            }
            else
            {
                return (Constants.ZERO, SqrtPriceMath.GetAmount1Delta(TickMath.GetSqrtRatioAtTick(TickLower), TickMath.GetSqrtRatioAtTick(TickUpper), Liquidity, true));
            }
        }
    }

    /// <summary>The AllowanceTransferPermitBatch for adding liquidity to this position.</summary>
    public AllowanceTransferPermitBatch PermitBatchData(Percent slippageTolerance, string spender, BigInteger nonce, BigInteger deadline)
    {
        var (amount0, amount1) = MintAmountsWithSlippage(slippageTolerance);
        return new AllowanceTransferPermitBatch(
            new List<PermitDetails>
            {
                new(Pool.Currency0.Wrapped().Address, amount0, deadline, nonce),
                new(Pool.Currency1.Wrapped().Address, amount1, deadline, nonce),
            },
            spender,
            deadline);
    }

    public static Position FromAmounts(Pool pool, int tickLower, int tickUpper, BigInteger amount0, BigInteger amount1, bool useFullPrecision)
    {
        BigInteger sqrtRatioAX96 = TickMath.GetSqrtRatioAtTick(tickLower);
        BigInteger sqrtRatioBX96 = TickMath.GetSqrtRatioAtTick(tickUpper);
        BigInteger liquidity = MaxLiquidity.MaxLiquidityForAmounts(pool.SqrtRatioX96, sqrtRatioAX96, sqrtRatioBX96, amount0, amount1, useFullPrecision);
        return new Position(pool, liquidity, tickLower, tickUpper);
    }

    public static Position FromAmount0(Pool pool, int tickLower, int tickUpper, BigInteger amount0, bool useFullPrecision) =>
        FromAmounts(pool, tickLower, tickUpper, amount0, UniswapSharp.Core.Constants.MaxUint256, useFullPrecision);

    public static Position FromAmount1(Pool pool, int tickLower, int tickUpper, BigInteger amount1) =>
        FromAmounts(pool, tickLower, tickUpper, UniswapSharp.Core.Constants.MaxUint256, amount1, true);
}
