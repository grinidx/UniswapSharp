using UniswapSharp.UniversalRouter.Entities.Actions;
using UniswapSharp.UniversalRouter.Types;

namespace UniswapSharp.UniversalRouter.Utils;

/// <summary>Port of universal-router-sdk <c>utils/normalizeEncodeSwapsSpec.ts</c>.</summary>
public static class NormalizeEncodeSwapsSpec
{
    /// <summary>Fills the four optional fields that validate/compute require.</summary>
    public static NormalizedSwapSpecification Normalize(SwapSpecification spec) => new()
    {
        TradeType = spec.TradeType,
        Routing = spec.Routing,
        SlippageTolerance = spec.SlippageTolerance,
        Recipient = spec.Recipient ?? Constants.SENDER_AS_RECIPIENT,
        TokenTransferMode = spec.TokenTransferMode ?? Entities.Actions.TokenTransferMode.Permit2,
        UrVersion = spec.UrVersion ?? UniversalRouterVersion.V2_0,
        SafeMode = spec.SafeMode ?? false,
        Fee = spec.Fee,
        Permit = spec.Permit,
        ChainId = spec.ChainId,
        Deadline = spec.Deadline,
        NativeErc20Input = spec.NativeErc20Input,
    };
}
