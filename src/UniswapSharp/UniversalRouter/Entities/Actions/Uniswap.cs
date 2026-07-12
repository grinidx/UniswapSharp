using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.UniversalRouter.Utils;
using UniswapSharp.V3;

namespace UniswapSharp.UniversalRouter.Entities.Actions;

/// <summary>Absolute flat-fee option. Port of <c>FlatFeeOptions</c>.</summary>
public sealed record FlatFeeOptions(object Amount, string Recipient);

/// <summary>
/// How input tokens are transferred to the UR. Port of <c>TokenTransferMode</c>.
/// <c>ApproveProxy</c> uses the SwapProxy contract.
/// </summary>
public enum TokenTransferMode
{
    Permit2,
    ApproveProxy,
}

/// <summary>
/// Options for a <see cref="UniswapTrade"/>. Port of universal-router-sdk <c>SwapOptions</c>
/// (extends router-sdk's swap options, minus <c>inputTokenPermit</c> which is replaced with the Permit2 permit).
/// </summary>
public sealed class SwapOptions
{
    public required Percent SlippageTolerance { get; set; }
    public string? Recipient { get; set; }
    public object? DeadlineOrPreviousBlockhash { get; init; }
    public Payments.IFeeOptions? Fee { get; init; }
    public bool? UseRouterBalance { get; init; }

    /// <summary>See upstream: native gas-token ERC20 input (e.g. Arc USDC), funded via msg.value.</summary>
    public bool? NativeErc20Input { get; init; }

    public Permit2Permit? InputTokenPermit { get; init; }
    public FlatFeeOptions? FlatFee { get; init; }
    public bool? SafeMode { get; init; }

    /// <summary>Universal Router version for encoding (defaults to V2_0 for backward compatibility).</summary>
    public UniversalRouterVersion? UrVersion { get; init; }

    /// <summary>How input tokens are transferred to the UR (defaults to Permit2).</summary>
    public TokenTransferMode? TokenTransferMode { get; init; }

    /// <summary>Required when tokenTransferMode is ApproveProxy; resolves UR address for the proxy.</summary>
    public int? ChainId { get; init; }
}
