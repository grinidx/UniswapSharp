using System.Globalization;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using UniswapSharp.V4.Utils;

namespace UniswapSharp.LiquidityLauncher;

/// <summary>
/// Pool-id and graffiti derivations. Ported from sdks/liquidity-launcher-sdk/src/poolId.ts.
/// </summary>
public static class PoolId
{
    /// <summary>
    /// Compute the Uniswap v4 PoolId for a launch pool, matching the on-chain <c>PoolKey.toId()</c>:
    /// <c>keccak256(abi.encode(currency0, currency1, fee, tickSpacing, hooks))</c> with the two
    /// currencies sorted ascending. Pass the REGISTERED hook (address(0) for the standard hookless
    /// launch pool) so the key matches what the strategy stored.
    /// </summary>
    public static string ComputeLbpPoolId(string currency, string token, int fee, int tickSpacing, string hook)
    {
        var (currency0, currency1) = AddressToBigInt(currency) < AddressToBigInt(token)
            ? (currency, token)
            : (token, currency);
        string encoded = AbiParamEncoder.Encode(
            new[] { "address", "address", "uint24", "int24", "address" },
            new object?[] { currency0, currency1, fee, tickSpacing, hook });
        return Sha3Keccack.Current.CalculateHash(encoded.HexToByteArray()).ToHex(true);
    }

    /// <summary><c>graffiti = keccak256(abi.encode(originalCreator))</c> — <c>LiquidityLauncher.getGraffiti</c>.</summary>
    public static string ComputeGraffiti(string originalCreator)
    {
        string encoded = AbiParamEncoder.Encode(new[] { "address" }, new object?[] { originalCreator });
        return Sha3Keccack.Current.CalculateHash(encoded.HexToByteArray()).ToHex(true);
    }

    private static BigInteger AddressToBigInt(string address)
    {
        string hex = address.StartsWith("0x") || address.StartsWith("0X") ? address[2..] : address;
        return BigInteger.Parse("0" + hex, NumberStyles.HexNumber);
    }
}
