using System.Numerics;
using UniswapSharp.V3.Utils;

namespace UniswapSharp.V4;

/// <summary>
/// Constants used internally by the V4 SDK. Ported from v4-sdk/src/internalConstants.ts.
/// </summary>
public static class Constants
{
    public const string ADDRESS_ZERO = "0x0000000000000000000000000000000000000000";
    public static readonly BigInteger NEGATIVE_ONE = BigInteger.MinusOne;
    public static readonly BigInteger ZERO = BigInteger.Zero;
    public static readonly BigInteger ONE = BigInteger.One;
    public static readonly BigInteger ONE_ETHER = BigInteger.Pow(10, 18);
    public const string EMPTY_BYTES = "0x";

    // used in liquidity amount math
    public static readonly BigInteger Q96 = BigInteger.Pow(2, 96);
    public static readonly BigInteger Q192 = Q96 * Q96;

    // pool setup
    public const int FEE_AMOUNT_LOW = 100;
    public const int FEE_AMOUNT_MEDIUM = 3000;
    public const int FEE_AMOUNT_HIGHEST = 10_000;
    public const int TICK_SPACING_TEN = 10;
    public const int TICK_SPACING_SIXTY = 60;

    // used in position manager math
    public const int MIN_SLIPPAGE_DECREASE = 0;

    // used when unwrapping weth in the position manager
    public static readonly BigInteger OPEN_DELTA = BigInteger.Zero;

    // default prices
    public static readonly BigInteger SQRT_PRICE_1_1 = EncodeSqrtRatioX96.Encode(1, 1);

    // default hook address
    public const string EMPTY_HOOK = "0x0000000000000000000000000000000000000000";

    // error constants
    public const string NATIVE_NOT_SET = "NATIVE_NOT_SET";
    public const string ZERO_LIQUIDITY = "ZERO_LIQUIDITY";
    public const string NO_SQRT_PRICE = "NO_SQRT_PRICE";
    public const string CANNOT_BURN = "CANNOT_BURN";

    /// <summary>The default factory-enabled fee amounts, in hundredths of a bip.</summary>
    public enum FeeAmount
    {
        LOWEST = 100,
        LOW = 500,
        MEDIUM = 3000,
        HIGH = 10000,
    }

    /// <summary>The default factory tick spacings by fee amount.</summary>
    public static readonly IReadOnlyDictionary<FeeAmount, int> TICK_SPACINGS = new Dictionary<FeeAmount, int>
    {
        { FeeAmount.LOWEST, 1 },
        { FeeAmount.LOW, 10 },
        { FeeAmount.MEDIUM, 60 },
        { FeeAmount.HIGH, 200 },
    };
}

/// <summary>
/// Function fragments that exist on the V4 PositionManager contract. The two permit entries are
/// the raw 4-byte selectors upstream uses to disambiguate the overloads (see internalConstants.ts).
/// </summary>
public static class PositionFunctions
{
    public const string INITIALIZE_POOL = "initializePool";
    public const string MODIFY_LIQUIDITIES = "modifyLiquidities";
    // Inherited from PermitForwarder: permitBatch(address,((address,uint160,uint48,uint48)[],address,uint256),bytes)
    public const string PERMIT_BATCH = "0x002a3e3a";
    // Inherited from ERC721Permit: permit(address,uint256,uint256,uint256,bytes)
    public const string ERC721PERMIT_PERMIT = "0x0f5730f1";
}
