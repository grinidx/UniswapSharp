using System.Numerics;
using UniswapSharp.Core.Entities;
using UniswapSharp.UniversalRouter.Entities;
using UniswapSharp.UniversalRouter.Entities.Actions;
using UniswapSharp.UniversalRouter.Utils;
using UniswapSharp.V4.Utils;
using Constants = UniswapSharp.UniversalRouter.Utils.Constants;
using RouterTradeT = UniswapSharp.Router.Entities.Trade<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;

namespace UniswapSharp.UniversalRouter;

public abstract partial class SwapRouter
{
    /// <summary>
    /// Produces the calldata + value for a router-sdk trade. Port of <c>SwapRouter.swapCallParameters</c>.
    /// </summary>
    public static MethodParameters SwapCallParameters(
        RouterTradeT trades, SwapOptions options, IReadOnlyList<AcrossV4DepositV3Params>? bridgeOptions = null)
    {
        var planner = new RoutePlanner();
        var trade = new UniswapTrade(trades, options);
        var inputCurrency = trade.Trade.InputAmount.Currency;

        if (options.TokenTransferMode == TokenTransferMode.ApproveProxy)
        {
            if (inputCurrency.IsNative)
            {
                throw new InvalidOperationException("PROXY_NATIVE_INPUT: SwapProxy only supports ERC20 input");
            }
            if (!options.ChainId.HasValue)
            {
                throw new InvalidOperationException("PROXY_MISSING_CHAIN_ID: chainId required when tokenTransferMode is ApproveProxy");
            }
            if (options.InputTokenPermit is not null)
            {
                throw new InvalidOperationException("PROXY_PERMIT_CONFLICT: Permit2 not used with SwapProxy");
            }
        }
        else
        {
            if (inputCurrency.IsNative && options.InputTokenPermit is not null)
            {
                throw new InvalidOperationException("NATIVE_INPUT_PERMIT");
            }
            if (options.InputTokenPermit is not null)
            {
                InputTokens.EncodePermit(planner, options.InputTokenPermit);
            }
        }

        BigInteger nativeCurrencyValue;
        if (inputCurrency.IsNative)
        {
            nativeCurrencyValue = trade.Trade.MaximumAmountIn(options.SlippageTolerance).Quotient;
        }
        else if (options.NativeErc20Input == true)
        {
            if (inputCurrency.Decimals > 18)
            {
                throw new InvalidOperationException("NATIVE_ERC20_INPUT_DECIMALS");
            }
            nativeCurrencyValue = trade.Trade.MaximumAmountIn(options.SlippageTolerance).Quotient
                * BigInteger.Pow(10, 18 - inputCurrency.Decimals);
        }
        else
        {
            nativeCurrencyValue = BigInteger.Zero;
        }

        trade.Encode(planner, new TradeConfig(false));

        if (bridgeOptions is not null)
        {
            foreach (var bridge in bridgeOptions)
            {
                planner.AddAcrossBridge(bridge);
            }
        }

        if (options.TokenTransferMode == TokenTransferMode.ApproveProxy)
        {
            return EncodeProxyPlan(planner, trade, options);
        }

        object? deadline = IsTruthyDeadline(options.DeadlineOrPreviousBlockhash)
            ? AbiParamEncoder.ToBigInteger(options.DeadlineOrPreviousBlockhash!)
            : null;

        return EncodePlan(planner, nativeCurrencyValue, deadline);
    }

    private static MethodParameters EncodeProxyPlan(RoutePlanner planner, UniswapTrade trade, SwapOptions options)
    {
        object? deadline = IsTruthyDeadline(options.DeadlineOrPreviousBlockhash)
            ? AbiParamEncoder.ToBigInteger(options.DeadlineOrPreviousBlockhash!)
            : null;

        return EncodeProxyCall(
            planner,
            ((Token)trade.Trade.InputAmount.Currency).Address,
            trade.Trade.MaximumAmountIn(options.SlippageTolerance).Quotient,
            options.ChainId!.Value,
            options.UrVersion ?? UniversalRouterVersion.V2_0,
            deadline);
    }
}
