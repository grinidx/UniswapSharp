using System.Numerics;
using UniswapSharp.V3.Entities;

namespace UniswapSharp.V3.Utils;

public static class V3Swap
{
    private static readonly BigInteger NEGATIVE_ONE = BigInteger.MinusOne;
    private static readonly BigInteger ONE = BigInteger.One;
    private static readonly BigInteger ZERO = BigInteger.Zero;

    public class StepComputations
    {
        public BigInteger SqrtPriceStartX96 { get; set; }
        public int TickNext { get; set; }
        public bool Initialized { get; set; }
        public BigInteger SqrtPriceNextX96 { get; set; }
        public BigInteger AmountIn { get; set; }
        public BigInteger AmountOut { get; set; }
        public BigInteger FeeAmount { get; set; }
    }

    public static async Task<(BigInteger amountCalculated, BigInteger sqrtRatioX96, BigInteger liquidity, int tickCurrent)> ExecuteAsync(
        BigInteger fee,
        BigInteger sqrtRatioX96,
        int tickCurrent,
        BigInteger liquidity,
        int tickSpacing,
        ITickDataProvider tickDataProvider,
        bool zeroForOne,
        BigInteger amountSpecified,
        BigInteger? sqrtPriceLimitX96 = null)
    {
        if (!sqrtPriceLimitX96.HasValue)
            sqrtPriceLimitX96 = zeroForOne
                ? TickMath.MIN_SQRT_RATIO + ONE
                : TickMath.MAX_SQRT_RATIO - ONE;

        if (zeroForOne)
        {
            if (sqrtPriceLimitX96.Value <= TickMath.MIN_SQRT_RATIO)
                throw new ArgumentException("RATIO_MIN");
            if (sqrtPriceLimitX96.Value >= sqrtRatioX96)
                throw new ArgumentException("RATIO_CURRENT");
        }
        else
        {
            if (sqrtPriceLimitX96.Value >= TickMath.MAX_SQRT_RATIO)
                throw new ArgumentException("RATIO_MAX");
            if (sqrtPriceLimitX96.Value <= sqrtRatioX96)
                throw new ArgumentException("RATIO_CURRENT");
        }

        bool exactInput = amountSpecified >= ZERO;

        var state = new State
        {
            AmountSpecifiedRemaining = amountSpecified,
            AmountCalculated = ZERO,
            SqrtPriceX96 = sqrtRatioX96,
            Tick = tickCurrent,
            Liquidity = liquidity
        };

        var cnt = 0;
        while (state.AmountSpecifiedRemaining != ZERO && state.SqrtPriceX96 != sqrtPriceLimitX96)
        {

            var step = new StepComputations
            {
                SqrtPriceStartX96 = state.SqrtPriceX96
            };


            (step.TickNext, step.Initialized) = await tickDataProvider.NextInitializedTickWithinOneWord(
                state.Tick,
                zeroForOne,
                tickSpacing
            );


            if (step.TickNext < TickMath.MIN_TICK)
            {
                step.TickNext = TickMath.MIN_TICK;
            }
            else if (step.TickNext > TickMath.MAX_TICK)
            {
                step.TickNext = TickMath.MAX_TICK;
            }

            step.SqrtPriceNextX96 = TickMath.GetSqrtRatioAtTick(step.TickNext);



            (state.SqrtPriceX96, step.AmountIn, step.AmountOut, step.FeeAmount) = SwapMath.ComputeSwapStep(
                state.SqrtPriceX96,
                (zeroForOne
                    ? step.SqrtPriceNextX96 < sqrtPriceLimitX96
                    : step.SqrtPriceNextX96 > sqrtPriceLimitX96)
                    ? sqrtPriceLimitX96.Value
                    : step.SqrtPriceNextX96,

                state.Liquidity,
                state.AmountSpecifiedRemaining,
                fee
            );

            if (exactInput)
            {
                state.AmountSpecifiedRemaining -= (step.AmountIn + step.FeeAmount);
                state.AmountCalculated -= step.AmountOut;
            }
            else
            {
                state.AmountSpecifiedRemaining += step.AmountOut;
                state.AmountCalculated += (step.AmountIn + step.FeeAmount);

            }

            if (state.SqrtPriceX96 == step.SqrtPriceNextX96)
            {
                if (step.Initialized)
                {
                    var liquidityNet = (await tickDataProvider.GetTick(step.TickNext)).LiquidityNet;
                    if (zeroForOne) liquidityNet *= NEGATIVE_ONE;

                    state.Liquidity = LiquidityMath.AddDelta(state.Liquidity, liquidityNet);
                }

                state.Tick = zeroForOne ? step.TickNext - 1 : step.TickNext;
            }
            else if (state.SqrtPriceX96 != step.SqrtPriceStartX96)
            {
                state.Tick = TickMath.GetTickAtSqrtRatio(state.SqrtPriceX96);


            }

            cnt++;
            if (cnt > 1000)
            {
                throw new Exception("Infinite loop");
            }



        }

        //Console.WriteLine("AmountCalculated: {0},{1},{2},{3}", state.AmountCalculated, state.SqrtPriceX96, state.Liquidity, state.Tick);

        return (state.AmountCalculated, state.SqrtPriceX96, state.Liquidity, state.Tick);
    }


    public class State
    {
        public BigInteger AmountSpecifiedRemaining { get; set; }
        public BigInteger AmountCalculated { get; set; }
        public BigInteger SqrtPriceX96 { get; set; }
        public int Tick { get; set; }
        public BigInteger Liquidity { get; set; }
    }
}

