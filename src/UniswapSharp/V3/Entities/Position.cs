using System.Numerics;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.V3.Utils;

// ReSharper disable InconsistentNaming

namespace UniswapSharp.V3.Entities;

public class Position
{
    private readonly (BigInteger amount0, BigInteger amount1)? _mintAmounts = null;

    private CurrencyAmount<Token>? _token0Amount;
    private CurrencyAmount<Token>? _token1Amount;

    public Position(Pool pool, int tickLower, int tickUpper, BigInteger liquidity)
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
        TickLower = tickLower;
        TickUpper = tickUpper;
        Liquidity = liquidity;
    }

    public Pool Pool { get; }
    public int TickLower { get; }
    public int TickUpper { get; }
    public BigInteger Liquidity { get; }

    public Price<Token, Token> Token0PriceLower => PriceTick.TickToPrice(Pool.Token0, Pool.Token1, TickLower);
    public Price<Token, Token> Token0PriceUpper => PriceTick.TickToPrice(Pool.Token0, Pool.Token1, TickUpper);

    public CurrencyAmount<Token> Amount0
    {
        get
        {
            if (_token0Amount == null)
            {
                if (Pool.TickCurrent < TickLower)
                {
                    _token0Amount = CurrencyAmount<Token>.FromRawAmount(
                        Pool.Token0,
                        SqrtPriceMath.GetAmount0Delta(
                            TickMath.GetSqrtRatioAtTick(TickLower),
                            TickMath.GetSqrtRatioAtTick(TickUpper),
                            Liquidity,
                            false
                        )
                    );
                }
                else if (Pool.TickCurrent < TickUpper)
                {
                    _token0Amount = CurrencyAmount<Token>.FromRawAmount(
                        Pool.Token0,
                        SqrtPriceMath.GetAmount0Delta(
                            Pool.SqrtRatioX96,
                            TickMath.GetSqrtRatioAtTick(TickUpper),
                            Liquidity,
                            false
                        )
                    );
                }
                else
                {
                    _token0Amount = CurrencyAmount<Token>.FromRawAmount(Pool.Token0, Constants.ZERO);
                }
            }

            return _token0Amount;
        }
    }

    public CurrencyAmount<Token> Amount1
    {
        get
        {
            if (_token1Amount == null)
            {
                if (Pool.TickCurrent < TickLower)
                {
                    _token1Amount = CurrencyAmount<Token>.FromRawAmount(Pool.Token1, Constants.ZERO);
                }
                else if (Pool.TickCurrent < TickUpper)
                {
                    _token1Amount = CurrencyAmount<Token>.FromRawAmount(
                        Pool.Token1,
                        SqrtPriceMath.GetAmount1Delta(
                            TickMath.GetSqrtRatioAtTick(TickLower),
                            Pool.SqrtRatioX96,
                            Liquidity,
                            false
                        )
                    );
                }
                else
                {
                    _token1Amount = CurrencyAmount<Token>.FromRawAmount(
                        Pool.Token1,
                        SqrtPriceMath.GetAmount1Delta(
                            TickMath.GetSqrtRatioAtTick(TickLower),
                            TickMath.GetSqrtRatioAtTick(TickUpper),
                            Liquidity,
                            false
                        )
                    );
                }
            }

            return _token1Amount;
        }
    }

    private (BigInteger sqrtRatioX96Lower, BigInteger sqrtRatioX96Upper) RatiosAfterSlippage(Percent slippageTolerance)
    {
        var priceLower = Pool.Token0Price.AsFraction().Multiply(new Percent(1).Subtract(slippageTolerance));
        var priceUpper = Pool.Token0Price.AsFraction().Multiply(slippageTolerance.Add(1));
        var sqrtRatioX96Lower = EncodeSqrtRatioX96.Encode(priceLower.Numerator, priceLower.Denominator);
        if (sqrtRatioX96Lower <= TickMath.MIN_SQRT_RATIO)
        {
            sqrtRatioX96Lower = TickMath.MIN_SQRT_RATIO + 1;
        }

        var sqrtRatioX96Upper = EncodeSqrtRatioX96.Encode(priceUpper.Numerator, priceUpper.Denominator);
        if (sqrtRatioX96Upper >= TickMath.MAX_SQRT_RATIO)
        {
            sqrtRatioX96Upper = TickMath.MAX_SQRT_RATIO - 1;
        }

        return (sqrtRatioX96Lower, sqrtRatioX96Upper);
    }


    public (BigInteger amount0, BigInteger amount1) BurnAmountsWithSlippage(Percent slippageTolerance)
    {
        var (sqrtRatioX96Upper, sqrtRatioX96Lower) = RatiosAfterSlippage(slippageTolerance);

        var poolLower = new Pool(
            Pool.Token0,
            Pool.Token1,
            Pool.Fee,
            sqrtRatioX96Lower,
            0,
            TickMath.GetTickAtSqrtRatio(sqrtRatioX96Lower)
        );
        var poolUpper = new Pool(
            Pool.Token0,
            Pool.Token1,
            Pool.Fee,
            sqrtRatioX96Upper,
            0,
            TickMath.GetTickAtSqrtRatio(sqrtRatioX96Upper)
        );

        var amount0 = new Position(
            poolUpper,
            TickLower,
            TickUpper,
            Liquidity
        ).Amount0;

        var amount1 = new Position(
            poolLower,
            TickLower,
            TickUpper,
            Liquidity
        ).Amount1;

        return (amount0.Quotient, amount1.Quotient);
    }



    public (BigInteger amount0, BigInteger amount1) MintAmounts
    {
        get
        {
            if (_mintAmounts == null)
            {
                if (Pool.TickCurrent < TickLower)
                {
                    return (SqrtPriceMath.GetAmount0Delta(
                        TickMath.GetSqrtRatioAtTick(TickLower),
                        TickMath.GetSqrtRatioAtTick(TickUpper),
                        Liquidity,
                        true
                    ), Constants.ZERO);
                }
                else if (Pool.TickCurrent < TickUpper)
                {
                    return (
                        SqrtPriceMath.GetAmount0Delta(
                            Pool.SqrtRatioX96,
                            TickMath.GetSqrtRatioAtTick(TickUpper),
                            Liquidity,
                            true
                        ),
                        SqrtPriceMath.GetAmount1Delta(
                            TickMath.GetSqrtRatioAtTick(TickLower),
                            Pool.SqrtRatioX96,
                            Liquidity,
                            true
                        )
                    );
                }
                else
                {
                    return (Constants.ZERO, SqrtPriceMath.GetAmount1Delta(
                        TickMath.GetSqrtRatioAtTick(TickLower),
                        TickMath.GetSqrtRatioAtTick(TickUpper),
                        Liquidity,
                        true
                    ));
                }
            }
            return _mintAmounts.Value;
        }
    }





    public static Position FromAmounts(Pool Pool, int TickLower, int TickUpper, BigInteger Amount0, BigInteger Amount1, bool UseFullPrecision)
    {
        var sqrtRatioAX96 = TickMath.GetSqrtRatioAtTick(TickLower);
        var sqrtRatioBX96 = TickMath.GetSqrtRatioAtTick(TickUpper);
        return new Position(Pool,
            TickLower,
            TickUpper,
            MaxLiquidity.MaxLiquidityForAmounts(
                Pool.SqrtRatioX96,
                sqrtRatioAX96,
                sqrtRatioBX96,
                Amount0,
                Amount1,
                UseFullPrecision
            )
        );
    }

    public static Position FromAmount0(Pool Pool, int TickLower, int TickUpper, BigInteger Amount0, bool UseFullPrecision)
    {
        return FromAmounts(
            Pool,
            TickLower,
            TickUpper,
            Amount0,
            TickMath.MaxUint256,
            UseFullPrecision
        );
    }

    public static Position FromAmount1(Pool Pool, int TickLower, int TickUpper, BigInteger Amount1)
    {
        return FromAmounts(
            Pool,
            TickLower,
            TickUpper,
            TickMath.MaxUint256,
            Amount1,
            true
        );
    }
}
