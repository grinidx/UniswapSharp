using System.Numerics;
using UniswapSharp.Core;

// ReSharper disable InconsistentNaming

namespace UniswapSharp.V3;

public static class Constants
{
    public const string FACTORY_ADDRESS = "0x1F98431c8aD98523631AE4a59f267346ea31F984";
    public const string ADDRESS_ZERO = "0x0000000000000000000000000000000000000000";

    [Obsolete("Please use PoolInitCodeHash(ChainId chainId) instead")]
    public const string POOL_INIT_CODE_HASH = "0xe34f199b19b2b4f47f68442619d555527d244f78a3297ea89325f843f87b8b54";

    public static string PoolInitCodeHash(ChainId? chainId = null)
    {
        switch (chainId)
        {
            case ChainId.ZKSYNC:
                return "0x010013f177ea1fcbc4520f9a3ca7cd2d1d77959e05aa66484027cb38e712aeed";
            default:
                return POOL_INIT_CODE_HASH;
        }
    }

    /// <summary>
    /// The default factory enabled fee amounts, denominated in hundredths of bips.
    /// </summary>
    public enum FeeAmount
    {
        LOWEST = 100,
        LOW = 500,
        MEDIUM = 3000,
        HIGH = 10000
    }

    /// <summary>
    /// The default factory tick spacings by fee amount.
    /// </summary>
    public static readonly Dictionary<FeeAmount, int> TICK_SPACINGS = new Dictionary<FeeAmount, int>
    {
        { FeeAmount.LOWEST, 1 },
        { FeeAmount.LOW, 10 },
        { FeeAmount.MEDIUM, 60 },
        { FeeAmount.HIGH, 200 }
    };


    public static readonly BigInteger NEGATIVE_ONE = new BigInteger(-1);
    public static readonly BigInteger ZERO = BigInteger.Zero;
    public static readonly BigInteger ONE = BigInteger.One;

    // Used in liquidity amount math
    public static readonly BigInteger Q96 = BigInteger.Pow(2, 96);
    public static readonly BigInteger Q192 = BigInteger.Pow(Q96, 2);

    public static readonly BigInteger MaxUint256 = BigInteger.Parse("115792089237316195423570985008687907853269984665640564039457584007913129639935");
}



