using System.Numerics;
// ReSharper disable InconsistentNaming

namespace UniswapSharp.V3.Entities;

public class Tick
{
    public static readonly int MIN_TICK = -887272;
    public static readonly int MAX_TICK = 887272;

    public int Index { get; }
    public BigInteger LiquidityGross { get;  }
    public BigInteger LiquidityNet { get; }




    public Tick(int index,  BigInteger liquidityNet, BigInteger liquidityGross)
    {
        if (index < MIN_TICK || index > MAX_TICK)
        {
            throw new ArgumentException("TICK");
        }

        Index = index;
        LiquidityGross = liquidityGross;
        LiquidityNet = liquidityNet;
    }
}