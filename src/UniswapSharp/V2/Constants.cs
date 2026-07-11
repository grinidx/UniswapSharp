using System.Numerics;
using UniswapSharp.Core;
using UniswapSharp.Core.Entities.Fractions;

// ReSharper disable InconsistentNaming

namespace UniswapSharp.V2;

/// <summary>
/// Port of v2-sdk <c>constants.ts</c>.
/// </summary>
public static class Constants
{
    /// <summary>
    /// Deprecated: use <see cref="FACTORY_ADDRESS_MAP"/> instead.
    /// </summary>
    public const string FACTORY_ADDRESS = "0x5C69bEe701ef814a2B6a3EDD4B1652CB9cc5aA6f";

    public static readonly Dictionary<ChainId, string> FACTORY_ADDRESS_MAP = Addresses.V2_FACTORY_ADDRESSES;

    public const string INIT_CODE_HASH = "0x96e8ac4277198ff8b6f785478aa9a39f403cb768dd02cbee326c3e7da348845f";

    public static readonly BigInteger MINIMUM_LIQUIDITY = new(1000);

    // exports for internal consumption
    public static readonly BigInteger ZERO = BigInteger.Zero;
    public static readonly BigInteger ONE = BigInteger.One;
    public static readonly BigInteger FIVE = new(5);
    public static readonly BigInteger _997 = new(997);
    public static readonly BigInteger _1000 = new(1000);
    public static readonly BigInteger BASIS_POINTS = new(10000);

    public static readonly Percent ZERO_PERCENT = new(ZERO);
    public static readonly Percent ONE_HUNDRED_PERCENT = new(ONE);
}
