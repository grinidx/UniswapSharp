using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using UniswapSharp.Core.Utils;
using UniswapSharp.V4.Utils;

namespace UniswapSharp.LiquidityLauncher;

/// <summary>
/// Discriminated lock-recipient request: each mode carries only its own fields. Common fields apply
/// to every mode. Ported from sdks/liquidity-launcher-sdk/src/lock.ts.
/// </summary>
public abstract record LockRecipientInput(
    string PositionManager,
    // Granted transfer rights over the position once the timelock expires (the pool owner).
    string Operator,
    BigInteger TimelockBlockNumber,
    // CREATE2 salt; derive once per launch (e.g. Salts.ComputeLauncherSalt).
    string LockSalt);

/// <summary>Plain hold-until-block, then the operator may move the position.</summary>
public sealed record TimelockLockRecipientInput(
    string PositionManager, string Operator, BigInteger TimelockBlockNumber, string LockSalt)
    : LockRecipientInput(PositionManager, Operator, TimelockBlockNumber, LockSalt);

/// <summary>Same as timelock, but LP fees are claimable to a fee recipient meanwhile.</summary>
public sealed record FeesForwarderLockRecipientInput(
    string PositionManager, string Operator, BigInteger TimelockBlockNumber, string LockSalt, string FeeRecipient)
    : LockRecipientInput(PositionManager, Operator, TimelockBlockNumber, LockSalt);

/// <summary>Same as timelock, but accrued currency buys back and burns the token.</summary>
public sealed record BuybackBurnLockRecipientInput(
    string PositionManager, string Operator, BigInteger TimelockBlockNumber, string LockSalt,
    string Token, string Currency, BigInteger MinTokenBurnAmount)
    : LockRecipientInput(PositionManager, Operator, TimelockBlockNumber, LockSalt);

public record LockRecipient(
    string PredictedAddress,
    // Calldata for CANONICAL_CREATE2_DEPLOYER: salt(32 bytes) ++ initCode.
    string DeployData);

/// <summary>
/// Liquidity-lock recipients deployed deterministically via CREATE2 through the canonical deployer.
/// Ported from sdks/liquidity-launcher-sdk/src/lock.ts.
/// </summary>
public static class Lock
{
    /// <summary>
    /// Computes the deterministic CREATE2 address of a per-launch lock-recipient contract and the
    /// calldata to deploy it via the canonical deployer.
    /// </summary>
    public static LockRecipient BuildLockRecipient(LockRecipientInput input)
    {
        string initCode = BuildInitCode(input);
        byte[] bytecodeHash = Sha3Keccack.Current.CalculateHash(initCode.HexToByteArray());
        string predictedAddress = AddressValidator.GetCreate2Address(
            Constants.CANONICAL_CREATE2_DEPLOYER, input.LockSalt.HexToByteArray(), bytecodeHash);
        string deployData = "0x" + Strip0x(input.LockSalt) + Strip0x(initCode);
        return new LockRecipient(predictedAddress, deployData);
    }

    private static string BuildInitCode(LockRecipientInput input)
    {
        switch (input)
        {
            case TimelockLockRecipientInput:
                // constructor(IPositionManager, address operator, uint256 timelockBlockNumber)
                return LockRecipientBytecode.TIMELOCK + Strip0x(AbiParamEncoder.Encode(
                    new[] { "address", "address", "uint256" },
                    new object?[] { input.PositionManager, input.Operator, input.TimelockBlockNumber }));

            case FeesForwarderLockRecipientInput ff:
                // constructor(IPositionManager, address operator, uint256 timelockBlockNumber, address feeRecipient)
                return LockRecipientBytecode.FEES_FORWARDER + Strip0x(AbiParamEncoder.Encode(
                    new[] { "address", "address", "uint256", "address" },
                    new object?[] { input.PositionManager, input.Operator, input.TimelockBlockNumber, ff.FeeRecipient }));

            case BuybackBurnLockRecipientInput bb:
                // constructor(address token, address currency, address operator, IPositionManager,
                //   uint256 timelockBlockNumber, uint256 minTokenBurnAmount)
                return LockRecipientBytecode.BUYBACK_BURN + Strip0x(AbiParamEncoder.Encode(
                    new[] { "address", "address", "address", "address", "uint256", "uint256" },
                    new object?[]
                    {
                        bb.Token, bb.Currency, input.Operator, input.PositionManager,
                        input.TimelockBlockNumber, bb.MinTokenBurnAmount,
                    }));

            default:
                throw new LauncherSdkError(LauncherErrorCode.INVALID_INPUT, "Unsupported liquidity lock mode");
        }
    }

    private static string Strip0x(string hex) => hex.StartsWith("0x") || hex.StartsWith("0X") ? hex[2..] : hex;
}
