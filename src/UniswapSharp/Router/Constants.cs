using System.Numerics;
using UniswapSharp.Core.Entities.Fractions;

// ReSharper disable InconsistentNaming

namespace UniswapSharp.Router;

/// <summary>
/// Port of router-sdk <c>constants.ts</c>.
/// </summary>
public static class Constants
{
    public const string ADDRESS_ZERO = "0x0000000000000000000000000000000000000000";
    public const string MSG_SENDER = "0x0000000000000000000000000000000000000001";
    public const string ADDRESS_THIS = "0x0000000000000000000000000000000000000002";

    public static readonly BigInteger ZERO = BigInteger.Zero;
    public static readonly BigInteger ONE = BigInteger.One;

    // = 1 << 23 or 0b0100000000000000000000000
    public const int MIXED_QUOTER_V1_V2_FEE_PATH_PLACEHOLDER = 1 << 23;

    // = 10 << 4 or 0b00100000 (2 << 4)
    public const int MIXED_QUOTER_V2_V2_FEE_PATH_PLACEHOLDER = 2 << 4;

    // = 11 << 20 or 0b001100000000000000000000 (3 << 20)
    public const int MIXED_QUOTER_V2_V3_FEE_PATH_PLACEHOLDER = 3 << 20;

    // = 100 << 20 or 0b010000000000000000000000 (4 << 20)
    public const int MIXED_QUOTER_V2_V4_FEE_PATH_PLACEHOLDER = 4 << 20;

    public static readonly Percent ZERO_PERCENT = new(ZERO);
    public static readonly Percent ONE_HUNDRED_PERCENT = new(100, 100);
}
