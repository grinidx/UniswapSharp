using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using UniswapSharp.V4.Utils;

namespace UniswapSharp.LiquidityLauncher;

/// <summary>
/// Salt derivation matching the launcher's on-chain CREATE2 scheme. Ported from
/// sdks/liquidity-launcher-sdk/src/salts.ts.
/// </summary>
public static class Salts
{
    /// <summary>Salt the launcher passes to the strategy: <c>keccak256(abi.encode(msg.sender, userSalt))</c>.</summary>
    public static string ComputeLauncherSalt(string wallet, string userSalt)
    {
        string encoded = AbiParamEncoder.Encode(
            new[] { "address", "bytes32" }, new object?[] { wallet, userSalt });
        return Sha3Keccack.Current.CalculateHash(encoded.HexToByteArray()).ToHex(true);
    }

    /// <summary>Salt the strategy derives for the initializer: <c>keccak256(abi.encode(launcherSalt, migratorParams))</c>.</summary>
    public static string ComputeInitializerSalt(string wallet, string userSalt, MigratorParameters migrator)
    {
        string launcherSalt = ComputeLauncherSalt(wallet, userSalt);
        string encoded = AbiParamEncoder.Encode(
            new[] { "bytes32", Encode.MIGRATOR_PARAMETERS_TYPE },
            new object?[] { launcherSalt, Encode.MigratorParametersValue(migrator) });
        return Sha3Keccack.Current.CalculateHash(encoded.HexToByteArray()).ToHex(true);
    }
}
