using System.Numerics;
using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.Router.Entities;
using UniswapSharp.Router.Entities.MixedRoute;
using UniswapSharp.Router.Utils;
using UniswapSharp.UniversalRouter.Utils;
using UniswapSharp.V4.Utils;
using Constants = UniswapSharp.UniversalRouter.Utils.Constants;
using EncodeMixedRouteToPath = UniswapSharp.Router.Utils.EncodeMixedRouteToPath;
using EncodeV3RouteToPath = UniswapSharp.V3.Utils.EncodeRouteToPath;
using EncodeV4RouteToPath = UniswapSharp.V4.Utils.EncodeRouteToPath;
using MixedRouteT = UniswapSharp.Router.Entities.MixedRoute<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;
using MixedSDK = UniswapSharp.Router.Entities.MixedRoute.MixedRouteSDK<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;
using MixedTradeT = UniswapSharp.Router.Entities.MixedRoute.MixedRouteTrade<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;
using PathCurrency = UniswapSharp.UniversalRouter.Utils.PathCurrency;
using RouterSwapT = UniswapSharp.Router.Entities.RouterSwap<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;
using RouterTradeT = UniswapSharp.Router.Entities.Trade<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;
using V2Pair = UniswapSharp.V2.Entities.Pair;
using V2TradeT = UniswapSharp.V2.Entities.Trade<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;
using V3Pool = UniswapSharp.V3.Entities.Pool;
using V3TradeT = UniswapSharp.V3.Entities.Trade<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;
using V4Act = UniswapSharp.V4.Utils.Actions;
using V4Pool = UniswapSharp.V4.Entities.Pool;
using V4RouteT = UniswapSharp.V4.Entities.Route<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;
using V4TradeT = UniswapSharp.V4.Entities.Trade<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;

namespace UniswapSharp.UniversalRouter.Entities.Actions;

/// <summary>
/// Port of universal-router-sdk <c>entities/actions/uniswap.ts</c> (<c>UniswapTrade</c>). Wraps a router-sdk
/// aggregated trade and encodes it as Universal Router commands.
/// </summary>
public sealed class UniswapTrade : ICommand
{
    private static readonly Percent REFUND_ETH_PRICE_IMPACT_THRESHOLD = new(50, 100);

    public RouterActionType TradeType => RouterActionType.UniswapTrade;
    public RouterTradeT Trade { get; }
    public SwapOptions Options { get; }
    public bool PayerIsUser { get; }

    public UniswapTrade(RouterTradeT trade, SwapOptions options)
    {
        Trade = trade;
        Options = options;

        if (options.Fee is not null && options.FlatFee is not null)
        {
            throw new InvalidOperationException("Only one fee option permitted");
        }

        if (options.NativeErc20Input == true)
        {
            if (Trade.InputAmount.Currency.IsNative)
            {
                throw new InvalidOperationException("nativeErc20Input requires an ERC20 input token");
            }
            if (options.TokenTransferMode == Actions.TokenTransferMode.ApproveProxy)
            {
                throw new InvalidOperationException("nativeErc20Input is not supported with ApproveProxy");
            }
            if (options.InputTokenPermit is not null)
            {
                throw new InvalidOperationException("nativeErc20Input does not use Permit2; remove inputTokenPermit");
            }
            if (InputRequiresUnwrap)
            {
                throw new InvalidOperationException(
                    "nativeErc20Input requires routes quoted against the ERC20 input (native pathInput unsupported)");
            }
        }

        if (options.TokenTransferMode == Actions.TokenTransferMode.ApproveProxy)
        {
            if (string.IsNullOrEmpty(options.Recipient) || options.Recipient == Constants.SENDER_AS_RECIPIENT)
            {
                throw new InvalidOperationException(
                    "Explicit recipient address required when using SwapProxy (SENDER_AS_RECIPIENT resolves to proxy)");
            }
            PayerIsUser = false;
        }
        else if (InputRequiresWrap || InputRequiresUnwrap || options.UseRouterBalance == true || options.NativeErc20Input == true)
        {
            PayerIsUser = false;
        }
        else
        {
            PayerIsUser = true;
        }
    }

    public bool IsAllV4 => Trade.Swaps.All(s => s.Route.Protocol == Protocol.V4);

    public bool InputRequiresWrap
    {
        get
        {
            var route = Trade.Swaps[0].Route;
            var firstPool = route.Pools[0];
            if (firstPool is V4Pool)
            {
                return Trade.InputAmount.Currency.IsNative && !route.PathInput.IsNative;
            }
            return Trade.InputAmount.Currency.IsNative;
        }
    }

    public bool InputRequiresUnwrap
    {
        get
        {
            var route = Trade.Swaps[0].Route;
            var firstPool = route.Pools[0];
            if (firstPool is V4Pool)
            {
                return !Trade.InputAmount.Currency.IsNative && route.PathInput.IsNative;
            }
            return false;
        }
    }

    public bool OutputRequiresWrap
    {
        get
        {
            var lastRoute = Trade.Swaps[0].Route;
            var lastPool = lastRoute.Pools[^1];
            if (lastPool is V4Pool v4Last)
            {
                if (!Trade.OutputAmount.Currency.IsNative)
                {
                    if (lastRoute.PathOutput.IsNative)
                    {
                        return true;
                    }
                    if (v4Last.Currency1.Equals(v4Last.Currency0.Wrapped()) && lastRoute.Pools.Count > 1)
                    {
                        var poolBefore = lastRoute.Pools[^2];
                        if (poolBefore is V4Pool pv4 &&
                            (pv4.Currency0.Equals(v4Last.Currency1) || pv4.Currency1.Equals(v4Last.Currency1)))
                        {
                            return true;
                        }
                        if (TPool.Token0(poolBefore).Equals(v4Last.Currency1) || TPool.Token1(poolBefore).Equals(v4Last.Currency1))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
    }

    public bool OutputRequiresUnwrap
    {
        get
        {
            var lastRoute = Trade.Swaps[0].Route;
            var lastPool = lastRoute.Pools[^1];
            if (lastPool is V4Pool v4Last)
            {
                if (Trade.OutputAmount.Currency.IsNative)
                {
                    if (!lastRoute.PathOutput.IsNative)
                    {
                        return true;
                    }
                    if (lastRoute.Pools.Count > 1 &&
                        lastRoute.Pools[^2] is V4Pool pv4 &&
                        pv4.Currency0.IsNative &&
                        v4Last.Currency1.Equals(v4Last.Currency0.Wrapped()))
                    {
                        return true;
                    }
                    return false;
                }
            }
            return Trade.OutputAmount.Currency.IsNative;
        }
    }

    public bool OutputRequiresTransition => OutputRequiresWrap || OutputRequiresUnwrap;

    public void Encode(RoutePlanner planner, TradeConfig config)
    {
        if (InputRequiresWrap)
        {
            planner.AddCommand(CommandType.WRAP_ETH, new object?[]
            {
                Constants.ROUTER_AS_RECIPIENT,
                Trade.MaximumAmountIn(Options.SlippageTolerance).Quotient.ToString(),
            });
        }
        else if (InputRequiresUnwrap)
        {
            if (Options.TokenTransferMode != Actions.TokenTransferMode.ApproveProxy)
            {
                planner.AddCommand(CommandType.PERMIT2_TRANSFER_FROM, new object?[]
                {
                    ((Token)Trade.InputAmount.Currency).Address,
                    Constants.ROUTER_AS_RECIPIENT,
                    Trade.MaximumAmountIn(Options.SlippageTolerance).Quotient.ToString(),
                });
            }
            planner.AddCommand(CommandType.UNWRAP_WETH, new object?[] { Constants.ROUTER_AS_RECIPIENT, 0 });
        }

        Options.Recipient ??= Constants.SENDER_AS_RECIPIENT;

        bool performAggregatedSlippageCheck =
            Trade.TradeType == UniswapSharp.Core.TradeType.EXACT_INPUT && Trade.Routes.Count > 2;
        bool routerMustCustody = performAggregatedSlippageCheck || OutputRequiresTransition || HasFeeOption(Options);

        foreach (var swap in Trade.Swaps)
        {
            switch (swap.Route.Protocol)
            {
                case Protocol.V2:
                    AddV2Swap(planner, swap, Trade.TradeType, Options, PayerIsUser, routerMustCustody);
                    break;
                case Protocol.V3:
                    AddV3Swap(planner, swap, Trade.TradeType, Options, PayerIsUser, routerMustCustody);
                    break;
                case Protocol.V4:
                    AddV4Swap(planner, swap, Trade.TradeType, Options, PayerIsUser, routerMustCustody);
                    break;
                case Protocol.MIXED:
                    AddMixedSwap(planner, swap, Trade.TradeType, Options, PayerIsUser, routerMustCustody);
                    break;
                default:
                    throw new InvalidOperationException("UNSUPPORTED_TRADE_PROTOCOL");
            }
        }

        BigInteger minimumAmountOut = Trade.MinimumAmountOut(Options.SlippageTolerance).Quotient;

        if (routerMustCustody)
        {
            var pools = Trade.Swaps[0].Route.Pools;
            string pathOutputCurrencyAddress = CurrencyAddress.GetCurrencyAddress(
                PathCurrency.GetPathCurrency(Trade.OutputAmount.Currency, pools[^1]));

            BigInteger feeDeduction = BigInteger.Zero;

            if (Options.Fee is not null)
            {
                bool useFullPrecision = Constants.IsAtLeastV2_1_1(Options.UrVersion);
                if (!useFullPrecision && !Options.Fee.Fee.Multiply((BigInteger)10_000).Remainder().Numerator.IsZero)
                {
                    throw new InvalidOperationException("Fractional fee bips require Universal Router version V2_1_1 or higher");
                }

                if (useFullPrecision)
                {
                    string fee1e18 = Numbers.EncodeFee1e18(Options.Fee.Fee);
                    planner.AddCommand(CommandType.PAY_PORTION_FULL_PRECISION,
                        new object?[] { pathOutputCurrencyAddress, Options.Fee.Recipient, fee1e18 },
                        false, Options.UrVersion);
                    feeDeduction = minimumAmountOut * AbiParamEncoder.ToBigInteger(fee1e18) / BigInteger.Pow(10, 18);
                }
                else
                {
                    string feeBips = Numbers.EncodeFeeBips(Options.Fee.Fee);
                    planner.AddCommand(CommandType.PAY_PORTION,
                        new object?[] { pathOutputCurrencyAddress, Options.Fee.Recipient, feeBips });
                    feeDeduction = minimumAmountOut * AbiParamEncoder.ToBigInteger(feeBips) / 10000;
                }
            }

            if (Options.FlatFee is not null)
            {
                BigInteger feeAmount = AbiParamEncoder.ToBigInteger(Options.FlatFee.Amount);
                if (minimumAmountOut < feeAmount)
                {
                    throw new InvalidOperationException("Flat fee amount greater than minimumAmountOut");
                }
                planner.AddCommand(CommandType.TRANSFER,
                    new object?[] { pathOutputCurrencyAddress, Options.FlatFee.Recipient, Options.FlatFee.Amount });
                feeDeduction = feeAmount;
            }

            if (Trade.TradeType == UniswapSharp.Core.TradeType.EXACT_OUTPUT)
            {
                minimumAmountOut -= feeDeduction;
            }

            if (OutputRequiresUnwrap)
            {
                planner.AddCommand(CommandType.UNWRAP_WETH, new object?[] { Options.Recipient, minimumAmountOut });
            }
            else if (OutputRequiresWrap)
            {
                planner.AddCommand(CommandType.WRAP_ETH, new object?[] { Options.Recipient, Constants.CONTRACT_BALANCE });
            }
            else
            {
                planner.AddCommand(CommandType.SWEEP, new object?[]
                {
                    CurrencyAddress.GetCurrencyAddress(Trade.OutputAmount.Currency),
                    Options.Recipient,
                    minimumAmountOut,
                });
            }
        }

        if (Trade.TradeType == UniswapSharp.Core.TradeType.EXACT_OUTPUT || RiskOfPartialFill(Trade))
        {
            if (InputRequiresWrap)
            {
                planner.AddCommand(CommandType.UNWRAP_WETH, new object?[] { Options.Recipient, 0 });
            }
            else if (InputRequiresUnwrap)
            {
                planner.AddCommand(CommandType.WRAP_ETH, new object?[] { Options.Recipient, Constants.CONTRACT_BALANCE });
            }
            else if (Options.TokenTransferMode == Actions.TokenTransferMode.ApproveProxy)
            {
                planner.AddCommand(CommandType.SWEEP, new object?[]
                {
                    CurrencyAddress.GetCurrencyAddress(Trade.InputAmount.Currency), Options.Recipient, 0,
                });
            }
            else if (Options.NativeErc20Input == true || Trade.InputAmount.Currency.IsNative)
            {
                planner.AddCommand(CommandType.SWEEP, new object?[] { Constants.ETH_ADDRESS, Options.Recipient, 0 });
            }
        }

        if (Options.SafeMode == true)
        {
            planner.AddCommand(CommandType.SWEEP, new object?[] { Constants.ETH_ADDRESS, Options.Recipient, 0 });
        }
    }

    private static object?[] MinHopArray(BigInteger[]? minHop) =>
        (minHop ?? Array.Empty<BigInteger>()).Select(x => (object?)x).ToArray();

    private static void AddV2Swap(RoutePlanner planner, RouterSwapT swap, TradeType tradeType, SwapOptions options,
        bool payerIsUser, bool routerMustCustody)
    {
        var route = swap.Route;
        if (swap.MinHopPriceX36 is { Length: > 0 } mh && mh.Length != route.Pools.Count)
        {
            throw new InvalidOperationException($"minHopPriceX36 length ({mh.Length}) must equal route.pools.length ({route.Pools.Count})");
        }

        var v2Route = ((RouteV2<BaseCurrency, BaseCurrency>)route).V2Route;
        var trade = new V2TradeT(v2Route, tradeType == UniswapSharp.Core.TradeType.EXACT_INPUT ? swap.InputAmount : swap.OutputAmount, tradeType);
        bool useV2_1_1 = Constants.IsAtLeastV2_1_1(options.UrVersion);
        var path = route.Path.Select(t => (object?)t.Wrapped().Address).ToArray();

        if (tradeType == UniswapSharp.Core.TradeType.EXACT_INPUT)
        {
            var parms = new List<object?>
            {
                routerMustCustody ? Constants.ROUTER_AS_RECIPIENT : options.Recipient,
                trade.MaximumAmountIn(options.SlippageTolerance).Quotient.ToString(),
                routerMustCustody ? (object?)0 : trade.MinimumAmountOut(options.SlippageTolerance).Quotient.ToString(),
                path,
                payerIsUser,
            };
            if (useV2_1_1) parms.Add(MinHopArray(swap.MinHopPriceX36));
            planner.AddCommand(CommandType.V2_SWAP_EXACT_IN, parms.ToArray(), false, options.UrVersion);
        }
        else
        {
            var parms = new List<object?>
            {
                routerMustCustody ? Constants.ROUTER_AS_RECIPIENT : options.Recipient,
                trade.MinimumAmountOut(options.SlippageTolerance).Quotient.ToString(),
                trade.MaximumAmountIn(options.SlippageTolerance).Quotient.ToString(),
                path,
                payerIsUser,
            };
            if (useV2_1_1) parms.Add(MinHopArray(swap.MinHopPriceX36));
            planner.AddCommand(CommandType.V2_SWAP_EXACT_OUT, parms.ToArray(), false, options.UrVersion);
        }
    }

    private static void AddV3Swap(RoutePlanner planner, RouterSwapT swap, TradeType tradeType, SwapOptions options,
        bool payerIsUser, bool routerMustCustody)
    {
        var route = swap.Route;
        if (swap.MinHopPriceX36 is { Length: > 0 } mh && mh.Length != route.Pools.Count)
        {
            throw new InvalidOperationException($"minHopPriceX36 length ({mh.Length}) must equal route.pools.length ({route.Pools.Count})");
        }

        var v3Route = ((RouteV3<BaseCurrency, BaseCurrency>)route).V3Route;
        var trade = V3TradeT.CreateUncheckedTrade(new V3.Entities.RouteInput<BaseCurrency, BaseCurrency>
        {
            Route = v3Route,
            InputAmount = swap.InputAmount,
            OutputAmount = swap.OutputAmount,
        }, tradeType);

        bool useV2_1_1 = Constants.IsAtLeastV2_1_1(options.UrVersion);
        string path = EncodeV3RouteToPath.Encode(v3Route, tradeType == UniswapSharp.Core.TradeType.EXACT_OUTPUT);

        if (tradeType == UniswapSharp.Core.TradeType.EXACT_INPUT)
        {
            var parms = new List<object?>
            {
                routerMustCustody ? Constants.ROUTER_AS_RECIPIENT : options.Recipient,
                trade.MaximumAmountIn(options.SlippageTolerance).Quotient.ToString(),
                routerMustCustody ? (object?)0 : trade.MinimumAmountOut(options.SlippageTolerance).Quotient.ToString(),
                path,
                payerIsUser,
            };
            if (useV2_1_1) parms.Add(MinHopArray(swap.MinHopPriceX36));
            planner.AddCommand(CommandType.V3_SWAP_EXACT_IN, parms.ToArray(), false, options.UrVersion);
        }
        else
        {
            var parms = new List<object?>
            {
                routerMustCustody ? Constants.ROUTER_AS_RECIPIENT : options.Recipient,
                trade.MinimumAmountOut(options.SlippageTolerance).Quotient.ToString(),
                trade.MaximumAmountIn(options.SlippageTolerance).Quotient.ToString(),
                path,
                payerIsUser,
            };
            if (useV2_1_1) parms.Add(MinHopArray(swap.MinHopPriceX36));
            planner.AddCommand(CommandType.V3_SWAP_EXACT_OUT, parms.ToArray(), false, options.UrVersion);
        }
    }

    private static void AddV4Swap(RoutePlanner planner, RouterSwapT swap, TradeType tradeType, SwapOptions options,
        bool payerIsUser, bool routerMustCustody)
    {
        var route = swap.Route;
        if (swap.MinHopPriceX36 is { Length: > 0 } mh && mh.Length != route.Pools.Count)
        {
            throw new InvalidOperationException($"minHopPriceX36 length ({mh.Length}) must equal route.pools.length ({route.Pools.Count})");
        }

        var pools = route.Pools.Cast<V4Pool>().ToList();
        var v4Route = new V4RouteT(pools, swap.InputAmount.Currency, swap.OutputAmount.Currency);
        var trade = V4TradeT.CreateUncheckedTrade(new V4.Entities.RouteInput<BaseCurrency, BaseCurrency>
        {
            Route = v4Route,
            InputAmount = swap.InputAmount,
            OutputAmount = swap.OutputAmount,
        }, tradeType);

        Percent? slippageToleranceOnSwap =
            routerMustCustody && tradeType == UniswapSharp.Core.TradeType.EXACT_INPUT ? null : options.SlippageTolerance;

        var perHopSlippage = (swap.MinHopPriceX36 ?? Array.Empty<BigInteger>()).ToList();

        var v4Planner = new V4Planner();
        v4Planner.AddTrade(trade, slippageToleranceOnSwap, perHopSlippage, V4URVersion.ToV4URVersion(options.UrVersion));
        v4Planner.AddSettle(trade.Route.PathInput, payerIsUser);

        BaseCurrency pathOutputForTake = trade.Route.PathOutput;
        var lastPool = v4Route.Pools[^1];
        bool ethWethPool = lastPool.Currency1.Equals(lastPool.Currency0.Wrapped());

        if (ethWethPool && v4Route.Pools.Count > 1)
        {
            var poolBefore = v4Route.Pools[^2];
            if (pathOutputForTake.IsNative && poolBefore.Currency0.IsNative)
            {
                pathOutputForTake = pathOutputForTake.Wrapped();
            }
            else if (!pathOutputForTake.IsNative &&
                     (poolBefore.Currency0.Equals(lastPool.Currency1) || poolBefore.Currency1.Equals(lastPool.Currency1)))
            {
                pathOutputForTake = lastPool.Currency0;
            }
        }

        v4Planner.AddTake(pathOutputForTake,
            routerMustCustody ? Constants.ROUTER_AS_RECIPIENT : options.Recipient ?? Constants.SENDER_AS_RECIPIENT);
        planner.AddCommand(CommandType.V4_SWAP, new object?[] { v4Planner.Finalize() });
    }

    private static void AddMixedSwap(RoutePlanner planner, RouterSwapT swap, TradeType tradeType, SwapOptions options,
        bool payerIsUser, bool routerMustCustody)
    {
        var route = (MixedRouteT)swap.Route;
        if (swap.MinHopPriceX36 is { Length: > 0 } mhx && mhx.Length != route.Pools.Count)
        {
            throw new InvalidOperationException($"minHopPriceX36 length ({mhx.Length}) must equal route.pools.length ({route.Pools.Count})");
        }
        var inputAmount = swap.InputAmount;
        var outputAmount = swap.OutputAmount;
        string tradeRecipient = routerMustCustody ? Constants.ROUTER_AS_RECIPIENT : options.Recipient ?? Constants.SENDER_AS_RECIPIENT;

        if (route.Pools.Count == 1)
        {
            if (route.Pools[0] is V4Pool)
            {
                AddV4Swap(planner, swap, tradeType, options, payerIsUser, routerMustCustody);
                return;
            }
            if (route.Pools[0] is V3Pool)
            {
                AddV3Swap(planner, swap, tradeType, options, payerIsUser, routerMustCustody);
                return;
            }
            if (route.Pools[0] is V2Pair)
            {
                AddV2Swap(planner, swap, tradeType, options, payerIsUser, routerMustCustody);
                return;
            }
            throw new InvalidOperationException("Invalid route type");
        }

        var trade = MixedTradeT.CreateUncheckedTrade(route, inputAmount, outputAmount, tradeType);
        BigInteger amountIn = trade.MaximumAmountIn(options.SlippageTolerance, inputAmount).Quotient;
        object amountOut = routerMustCustody ? (object)BigInteger.Zero : trade.MinimumAmountOut(options.SlippageTolerance, outputAmount).Quotient;

        var sections = MixedRouteUtils.PartitionMixedRouteByProtocol(route);
        bool IsLast(int i) => i == sections.Count - 1;
        bool useV2_1_1 = Constants.IsAtLeastV2_1_1(options.UrVersion);

        BaseCurrency inputToken = route.PathInput;
        int hopOffset = 0;

        for (int i = 0; i < sections.Count; i++)
        {
            var section = sections[i];
            var routePool = section[0];
            var outputToken = MixedRouteUtils.GetOutputOfPools(section, inputToken);
            var subRoute = new MixedRouteT(new MixedSDK(section.ToList(), inputToken, outputToken));

            var sectionHopSlippage = swap.MinHopPriceX36?.Skip(hopOffset).Take(section.Count).ToArray();

            BaseCurrency nextInputToken;
            string swapRecipient;
            if (IsLast(i))
            {
                nextInputToken = outputToken;
                swapRecipient = tradeRecipient;
            }
            else
            {
                var nextPool = sections[i + 1][0];
                nextInputToken = PathCurrency.GetPathCurrency(outputToken, nextPool);
                bool v2PoolIsSwapRecipient = nextPool is V2Pair && outputToken.Equals(nextInputToken);
                swapRecipient = v2PoolIsSwapRecipient ? ((V2Pair)nextPool).LiquidityToken.Address : Constants.ROUTER_AS_RECIPIENT;
            }

            if (routePool is V4Pool)
            {
                var v4Planner = new V4Planner();
                var v4SubRoute = new V4RouteT(section.Cast<V4Pool>().ToList(), subRoute.Input, subRoute.Output);
                var v4SectionSlippage = (sectionHopSlippage ?? Array.Empty<BigInteger>()).Select(s => (object?)s).ToArray();

                v4Planner.AddSettle(inputToken, payerIsUser && i == 0, i == 0 ? amountIn : Constants.CONTRACT_BALANCE);
                object?[] pathTuples = EncodeV4RouteToPath.Encode(v4SubRoute).Select(pk => (object?)new object?[]
                {
                    pk.IntermediateCurrency, (BigInteger)pk.Fee, pk.TickSpacing, pk.Hooks, pk.HookData,
                }).ToArray();

                object?[] swapStruct = useV2_1_1
                    ? new object?[]
                    {
                        inputToken.IsNative ? Constants.ETH_ADDRESS : ((Token)inputToken).Address,
                        pathTuples, v4SectionSlippage, BigInteger.Zero, !IsLast(i) ? (object)BigInteger.Zero : amountOut,
                    }
                    : new object?[]
                    {
                        inputToken.IsNative ? Constants.ETH_ADDRESS : ((Token)inputToken).Address,
                        pathTuples, BigInteger.Zero, !IsLast(i) ? (object)BigInteger.Zero : amountOut,
                    };
                v4Planner.AddAction(V4Act.SWAP_EXACT_IN, new object?[] { swapStruct }, V4URVersion.ToV4URVersion(options.UrVersion));

                BaseCurrency outputTokenForTake = outputToken;
                if (IsLast(i))
                {
                    var lastPool = route.Pools[^1];
                    bool v4Pool = lastPool is V4Pool;
                    bool ethWethPool = v4Pool && ((V4Pool)lastPool).Currency1.Equals(((V4Pool)lastPool).Currency0.Wrapped());
                    var poolBefore = route.Pools[^2];
                    if (ethWethPool)
                    {
                        if (outputToken.IsNative && TPool.Token0(poolBefore).IsNative)
                        {
                            outputTokenForTake = outputToken.Wrapped();
                        }
                        else if (!outputToken.IsNative &&
                                 (TPool.Token0(poolBefore).Equals(TPool.Token1(lastPool)) || TPool.Token1(poolBefore).Equals(TPool.Token1(lastPool))))
                        {
                            outputTokenForTake = TPool.Token0(lastPool);
                        }
                    }
                }

                v4Planner.AddTake(outputTokenForTake, swapRecipient);
                planner.AddCommand(CommandType.V4_SWAP, new object?[] { v4Planner.Finalize() });
            }
            else if (routePool is V3Pool)
            {
                var v3Params = new List<object?>
                {
                    swapRecipient,
                    i == 0 ? amountIn : Constants.CONTRACT_BALANCE,
                    !IsLast(i) ? (object)BigInteger.Zero : amountOut,
                    EncodeMixedRouteToPath.Encode(subRoute),
                    payerIsUser && i == 0,
                };
                if (useV2_1_1) v3Params.Add((sectionHopSlippage ?? Array.Empty<BigInteger>()).Select(x => (object?)x).ToArray());
                planner.AddCommand(CommandType.V3_SWAP_EXACT_IN, v3Params.ToArray(), false, options.UrVersion);
            }
            else if (routePool is V2Pair)
            {
                var v2Params = new List<object?>
                {
                    swapRecipient,
                    i == 0 ? amountIn : Constants.CONTRACT_BALANCE,
                    !IsLast(i) ? (object)BigInteger.Zero : amountOut,
                    subRoute.Path.Select(t => (object?)t.Wrapped().Address).ToArray(),
                    payerIsUser && i == 0,
                };
                if (useV2_1_1) v2Params.Add((sectionHopSlippage ?? Array.Empty<BigInteger>()).Select(x => (object?)x).ToArray());
                planner.AddCommand(CommandType.V2_SWAP_EXACT_IN, v2Params.ToArray(), false, options.UrVersion);
            }
            else
            {
                throw new InvalidOperationException("Unexpected Pool Type");
            }

            if (!IsLast(i))
            {
                if (outputToken.IsNative && !nextInputToken.IsNative)
                {
                    planner.AddCommand(CommandType.WRAP_ETH, new object?[] { Constants.ROUTER_AS_RECIPIENT, Constants.CONTRACT_BALANCE });
                }
                else if (!outputToken.IsNative && nextInputToken.IsNative)
                {
                    planner.AddCommand(CommandType.UNWRAP_WETH, new object?[] { Constants.ROUTER_AS_RECIPIENT, 0 });
                }
            }

            hopOffset += section.Count;
            inputToken = nextInputToken;
        }
    }

    private static bool RiskOfPartialFill(RouterTradeT trade) =>
        trade.PriceImpact.GreaterThan(REFUND_ETH_PRICE_IMPACT_THRESHOLD);

    private static bool HasFeeOption(SwapOptions options) => options.Fee is not null || options.FlatFee is not null;
}
