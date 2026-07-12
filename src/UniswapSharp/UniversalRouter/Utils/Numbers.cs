using System.Numerics;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.V3.Utils;

namespace UniswapSharp.UniversalRouter.Utils;

/// <summary>Port of universal-router-sdk <c>utils/numbers.ts</c>.</summary>
public static class Numbers
{
    /// <summary>Encodes a fee percentage as basis points (bps): <c>toHex(fee * 10000)</c>.</summary>
    public static string EncodeFeeBips(Percent fee) => Utilities.ToHex(fee.Multiply((BigInteger)10_000).Quotient);

    private static readonly BigInteger FULL_PORTION_PRECISION = BigInteger.Pow(10, 18);

    /// <summary>Encodes a fee percentage with 1e18 precision: <c>toHex(fee * 1e18)</c>.</summary>
    public static string EncodeFee1e18(Percent fee) => Utilities.ToHex(fee.Multiply(FULL_PORTION_PRECISION).Quotient);
}
