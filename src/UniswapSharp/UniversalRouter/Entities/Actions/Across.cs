using System.Numerics;

namespace UniswapSharp.UniversalRouter.Entities.Actions;

/// <summary>
/// Port of universal-router-sdk <c>entities/actions/across.ts</c> (<c>AcrossV4DepositV3Params</c>).
/// Parameters for the Across V4 Deposit V3 command (cross-chain bridging via Across V3 SpokePool).
/// Numeric fields are <see cref="object"/> to accept <see cref="BigInteger"/>, <see cref="int"/>, or a
/// decimal string (BigNumberish).
/// </summary>
public sealed class AcrossV4DepositV3Params
{
    /// <summary>Credited depositor on origin chain.</summary>
    public required string Depositor { get; init; }

    /// <summary>Destination recipient.</summary>
    public required string Recipient { get; init; }

    /// <summary>ERC20 on origin (WETH if bridging ETH).</summary>
    public required string InputToken { get; init; }

    /// <summary>ERC20 on destination.</summary>
    public required string OutputToken { get; init; }

    /// <summary>Amount to bridge (supports CONTRACT_BALANCE).</summary>
    public required object InputAmount { get; init; }

    /// <summary>Expected amount on destination.</summary>
    public required object OutputAmount { get; init; }

    /// <summary>Destination chain ID.</summary>
    public required object DestinationChainId { get; init; }

    /// <summary>0x0 if no exclusivity.</summary>
    public required string ExclusiveRelayer { get; init; }

    /// <summary>uint32.</summary>
    public required object QuoteTimestamp { get; init; }

    /// <summary>uint32.</summary>
    public required object FillDeadline { get; init; }

    /// <summary>uint32.</summary>
    public required object ExclusivityDeadline { get; init; }

    /// <summary>bytes - optional message data.</summary>
    public required string Message { get; init; }

    /// <summary>If true, bridge native ETH (inputToken must be WETH).</summary>
    public required bool UseNative { get; init; }
}
