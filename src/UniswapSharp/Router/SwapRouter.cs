using System.Collections;
using System.Numerics;
using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.Core.Utils;
using UniswapSharp.Router.Entities;
using UniswapSharp.Router.Entities.MixedRoute;
using UniswapSharp.Router.Utils;
using UniswapSharp.V3.Utils;
using UniswapSharp.V4.Utils;
using static UniswapSharp.V3.Utils.AbiFunctionEncoder;
using MixedTradeT = UniswapSharp.Router.Entities.MixedRoute.MixedRouteTrade<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;
using RouterTradeT = UniswapSharp.Router.Entities.Trade<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;
using V2TradeT = UniswapSharp.V2.Entities.Trade<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;
using V3PoolT = UniswapSharp.V3.Entities.Pool;
using V3RouteInput = UniswapSharp.V3.Entities.RouteInput<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;
using V3TradeT = UniswapSharp.V3.Entities.Trade<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;
using V4PoolT = UniswapSharp.V4.Entities.Pool;

namespace UniswapSharp.Router;

/// <summary>Options for producing the arguments to send calls to the SwapRouter02.</summary>
public class SwapOptions
{
    public required Percent SlippageTolerance { get; init; }
    public string? Recipient { get; init; }

    /// <summary>Either a deadline (epoch seconds, as BigInteger/int/string) or a previous block hash (0x… string).</summary>
    public object? DeadlineOrPreviousBlockhash { get; init; }

    /// <summary>Accepts SelfPermit.StandardPermitArguments or SelfPermit.AllowedPermitArguments.</summary>
    public V3.SelfPermit.IPermitOptions? InputTokenPermit { get; init; }
    public V3.Payments.IFeeOptions? Fee { get; init; }
}

public class SwapAndAddOptions : SwapOptions
{
    /// <summary>Accepts SelfPermit.StandardPermitArguments or SelfPermit.AllowedPermitArguments.</summary>
    public V3.SelfPermit.IPermitOptions? OutputTokenPermit { get; init; }
}

/// <summary>
/// Port of router-sdk <c>swapRouter.ts</c> (SwapRouter02). Static helpers to encode trade calldata.
/// </summary>
public abstract class SwapRouter
{
    private static readonly Percent REFUND_ETH_PRICE_IMPACT_THRESHOLD = new(50, 100);

    private SwapRouter() { }

    public class MethodParameters
    {
        public required string Calldata { get; init; }
        public required string Value { get; init; }
    }

    private static string Encode(string sig, string[] types, object?[] values) =>
        "0x" + Selector(sig) + AbiParamEncoder.Encode(types, values)[2..];

    private static string Recipient(bool routerMustCustody, SwapOptions options) =>
        routerMustCustody
            ? Constants.ADDRESS_THIS
            : options.Recipient is null ? Constants.MSG_SENDER : AddressValidator.ValidateAndParseAddress(options.Recipient);

    private static string EncodeV2Swap(V2TradeT trade, SwapOptions options, bool routerMustCustody, bool performAggregatedSlippageCheck)
    {
        var amountIn = trade.MaximumAmountIn(options.SlippageTolerance).Quotient;
        var amountOut = trade.MinimumAmountOut(options.SlippageTolerance).Quotient;
        var path = trade.Route.Path.Select(t => (object?)t.Address).ToList();
        var recipient = Recipient(routerMustCustody, options);

        if (trade.TradeType == TradeType.EXACT_INPUT)
        {
            return Encode("swapExactTokensForTokens(uint256,uint256,address[],address)",
                new[] { "uint256", "uint256", "address[]", "address" },
                new object?[] { amountIn, performAggregatedSlippageCheck ? BigInteger.Zero : amountOut, path, recipient });
        }
        return Encode("swapTokensForExactTokens(uint256,uint256,address[],address)",
            new[] { "uint256", "uint256", "address[]", "address" },
            new object?[] { amountOut, amountIn, path, recipient });
    }

    private static List<string> EncodeV3Swap(V3TradeT trade, SwapOptions options, bool routerMustCustody, bool performAggregatedSlippageCheck)
    {
        var calldatas = new List<string>();
        foreach (var swap in trade.Swaps)
        {
            var amountIn = trade.MaximumAmountIn(options.SlippageTolerance, swap.InputAmount).Quotient;
            var amountOut = trade.MinimumAmountOut(options.SlippageTolerance, swap.OutputAmount).Quotient;
            bool singleHop = swap.Route.Pools.Count == 1;
            var recipient = Recipient(routerMustCustody, options);

            if (singleHop)
            {
                string tokenIn = swap.Route.TokenPath[0].Address;
                string tokenOut = swap.Route.TokenPath[1].Address;
                int fee = (int)swap.Route.Pools[0].Fee;
                if (trade.TradeType == TradeType.EXACT_INPUT)
                {
                    calldatas.Add(Encode("exactInputSingle((address,address,uint24,address,uint256,uint256,uint160))",
                        new[] { "(address,address,uint24,address,uint256,uint256,uint160)" },
                        new object?[] { new object?[] { tokenIn, tokenOut, fee, recipient, amountIn, performAggregatedSlippageCheck ? BigInteger.Zero : amountOut, BigInteger.Zero } }));
                }
                else
                {
                    calldatas.Add(Encode("exactOutputSingle((address,address,uint24,address,uint256,uint256,uint160))",
                        new[] { "(address,address,uint24,address,uint256,uint256,uint160)" },
                        new object?[] { new object?[] { tokenIn, tokenOut, fee, recipient, amountOut, amountIn, BigInteger.Zero } }));
                }
            }
            else
            {
                string path = V3.Utils.EncodeRouteToPath.Encode(swap.Route, trade.TradeType == TradeType.EXACT_OUTPUT);
                if (trade.TradeType == TradeType.EXACT_INPUT)
                {
                    calldatas.Add(Encode("exactInput((bytes,address,uint256,uint256))",
                        new[] { "(bytes,address,uint256,uint256)" },
                        new object?[] { new object?[] { path, recipient, amountIn, performAggregatedSlippageCheck ? BigInteger.Zero : amountOut } }));
                }
                else
                {
                    calldatas.Add(Encode("exactOutput((bytes,address,uint256,uint256))",
                        new[] { "(bytes,address,uint256,uint256)" },
                        new object?[] { new object?[] { path, recipient, amountOut, amountIn } }));
                }
            }
        }
        return calldatas;
    }

    private static List<string> EncodeMixedRouteSwap(MixedTradeT trade, SwapOptions options, bool routerMustCustody, bool performAggregatedSlippageCheck)
    {
        var calldatas = new List<string>();
        if (trade.TradeType != TradeType.EXACT_INPUT)
        {
            throw new ArgumentException("TRADE_TYPE");
        }

        foreach (var swap in trade.Swaps)
        {
            var route = swap.Route;
            if (route.Pools.Any(p => p is V4PoolT))
            {
                throw new InvalidOperationException("Encoding mixed routes with V4 not supported");
            }
            var amountIn = trade.MaximumAmountIn(options.SlippageTolerance, swap.InputAmount).Quotient;
            var amountOut = trade.MinimumAmountOut(options.SlippageTolerance, swap.OutputAmount).Quotient;
            bool singleHop = route.Pools.Count == 1;
            var recipient = Recipient(routerMustCustody, options);

            bool MixedRouteIsAllV3(MixedRouteSDK<BaseCurrency, BaseCurrency> r) => r.Pools.All(p => p is V3PoolT);

            if (singleHop)
            {
                if (MixedRouteIsAllV3(route))
                {
                    calldatas.Add(Encode("exactInputSingle((address,address,uint24,address,uint256,uint256,uint160))",
                        new[] { "(address,address,uint24,address,uint256,uint256,uint160)" },
                        new object?[]
                        {
                            new object?[]
                            {
                                route.Path[0].Wrapped().Address,
                                route.Path[1].Wrapped().Address,
                                (int)((V3PoolT)route.Pools[0]).Fee,
                                recipient,
                                amountIn,
                                performAggregatedSlippageCheck ? BigInteger.Zero : amountOut,
                                BigInteger.Zero,
                            },
                        }));
                }
                else
                {
                    var path = route.Path.Select(t => (object?)t.Wrapped().Address).ToList();
                    calldatas.Add(Encode("swapExactTokensForTokens(uint256,uint256,address[],address)",
                        new[] { "uint256", "uint256", "address[]", "address" },
                        new object?[] { amountIn, performAggregatedSlippageCheck ? BigInteger.Zero : amountOut, path, recipient }));
                }
            }
            else
            {
                var sections = MixedRouteUtils.PartitionMixedRouteByProtocol(route);
                BaseCurrency inputToken = route.Input.Wrapped();

                for (int i = 0; i < sections.Count; i++)
                {
                    var section = sections[i];
                    var outputToken = MixedRouteUtils.GetOutputOfPools(section, inputToken);
                    var newRouteOriginal = new MixedRouteSDK<BaseCurrency, BaseCurrency>(
                        section,
                        TPool.Token0(section[0]).Equals(inputToken) ? TPool.Token0(section[0]) : TPool.Token1(section[0]),
                        outputToken);
                    var newRoute = new MixedRoute<BaseCurrency, BaseCurrency>(newRouteOriginal);

                    inputToken = outputToken.Wrapped();
                    bool isLast = i == sections.Count - 1;

                    if (MixedRouteIsAllV3(newRoute))
                    {
                        string path = EncodeMixedRouteToPath.Encode(newRoute);
                        calldatas.Add(Encode("exactInput((bytes,address,uint256,uint256))",
                            new[] { "(bytes,address,uint256,uint256)" },
                            new object?[]
                            {
                                new object?[]
                                {
                                    path,
                                    isLast ? recipient : Constants.ADDRESS_THIS,
                                    i == 0 ? amountIn : BigInteger.Zero,
                                    isLast ? amountOut : BigInteger.Zero,
                                },
                            }));
                    }
                    else
                    {
                        var path = newRoute.Path.Select(t => (object?)t.Wrapped().Address).ToList();
                        calldatas.Add(Encode("swapExactTokensForTokens(uint256,uint256,address[],address)",
                            new[] { "uint256", "uint256", "address[]", "address" },
                            new object?[]
                            {
                                i == 0 ? amountIn : BigInteger.Zero,
                                isLast ? amountOut : BigInteger.Zero,
                                path,
                                isLast ? recipient : Constants.ADDRESS_THIS,
                            }));
                    }
                }
            }
        }
        return calldatas;
    }

    private sealed class EncodeSwapsResult
    {
        public required List<string> Calldatas { get; init; }
        public required object SampleTrade { get; init; }
        public required bool RouterMustCustody { get; init; }
        public required bool InputIsNative { get; init; }
        public required bool OutputIsNative { get; init; }
        public required CurrencyAmount<BaseCurrency> TotalAmountIn { get; init; }
        public required CurrencyAmount<BaseCurrency> MinimumAmountOut { get; init; }
        public required CurrencyAmount<BaseCurrency> QuoteAmountOut { get; init; }
    }

    private static EncodeSwapsResult EncodeSwaps(object trades, SwapOptions options, bool isSwapAndAdd = false)
    {
        List<object> individualTrades;

        if (trades is RouterTradeT metaTrade)
        {
            if (!metaTrade.Swaps.All(s => s.Route.Protocol is Protocol.V2 or Protocol.V3 or Protocol.MIXED))
            {
                throw new InvalidOperationException("UNSUPPORTED_PROTOCOL (encoding routes with v4 not supported)");
            }

            individualTrades = new List<object>();
            foreach (var swap in metaTrade.Swaps)
            {
                var route = swap.Route;
                switch (route.Protocol)
                {
                    case Protocol.V2:
                        individualTrades.Add(new V2TradeT(
                            ((RouteV2<BaseCurrency, BaseCurrency>)route).V2Route,
                            (metaTrade.TradeType == TradeType.EXACT_INPUT ? swap.InputAmount.AsBaseCurrency()! : swap.OutputAmount.AsBaseCurrency()!),
                            metaTrade.TradeType));
                        break;
                    case Protocol.V3:
                        individualTrades.Add(V3TradeT.CreateUncheckedTrade(new V3RouteInput
                        {
                            Route = ((RouteV3<BaseCurrency, BaseCurrency>)route).V3Route,
                            InputAmount = swap.InputAmount,
                            OutputAmount = swap.OutputAmount,
                        }, metaTrade.TradeType));
                        break;
                    case Protocol.MIXED:
                        individualTrades.Add(MixedTradeT.CreateUncheckedTrade(
                            (MixedRoute<BaseCurrency, BaseCurrency>)route, swap.InputAmount, swap.OutputAmount, metaTrade.TradeType));
                        break;
                    default:
                        throw new InvalidOperationException("UNSUPPORTED_TRADE_PROTOCOL");
                }
            }
        }
        else if (trades is IEnumerable enumerable and not string)
        {
            individualTrades = enumerable.Cast<object>().ToList();
        }
        else
        {
            individualTrades = new List<object> { trades };
        }

        int numberOfTrades = individualTrades.Sum(t => t is V3TradeT or MixedTradeT ? SwapsCount(t) : 1);
        var sampleTrade = individualTrades[0];
        var sampleInput = InCurrency(sampleTrade);
        var sampleOutput = OutCurrency(sampleTrade);
        var sampleType = TType(sampleTrade);

        if (!individualTrades.All(t => InCurrency(t).Equals(sampleInput)))
        {
            throw new InvalidOperationException("TOKEN_IN_DIFF");
        }
        if (!individualTrades.All(t => OutCurrency(t).Equals(sampleOutput)))
        {
            throw new InvalidOperationException("TOKEN_OUT_DIFF");
        }
        if (!individualTrades.All(t => TType(t) == sampleType))
        {
            throw new InvalidOperationException("TRADE_TYPE_DIFF");
        }

        var calldatas = new List<string>();
        bool inputIsNative = sampleInput.IsNative;
        bool outputIsNative = sampleOutput.IsNative;
        bool performAggregatedSlippageCheck = sampleType == TradeType.EXACT_INPUT && numberOfTrades > 2;
        bool routerMustCustody = outputIsNative || options.Fee != null || isSwapAndAdd || performAggregatedSlippageCheck;

        if (options.InputTokenPermit != null)
        {
            if (!sampleInput.IsToken)
            {
                throw new InvalidOperationException("NON_TOKEN_PERMIT");
            }
            calldatas.Add(V3.SelfPermit.EncodePermit((Token)sampleInput, options.InputTokenPermit));
        }

        foreach (var trade in individualTrades)
        {
            switch (trade)
            {
                case V2TradeT v2:
                    calldatas.Add(EncodeV2Swap(v2, options, routerMustCustody, performAggregatedSlippageCheck));
                    break;
                case V3TradeT v3:
                    calldatas.AddRange(EncodeV3Swap(v3, options, routerMustCustody, performAggregatedSlippageCheck));
                    break;
                case MixedTradeT mixed:
                    calldatas.AddRange(EncodeMixedRouteSwap(mixed, options, routerMustCustody, performAggregatedSlippageCheck));
                    break;
                default:
                    throw new InvalidOperationException("Unsupported trade object");
            }
        }

        var zeroIn = CurrencyAmount<BaseCurrency>.FromRawAmount(sampleInput, 0);
        var zeroOut = CurrencyAmount<BaseCurrency>.FromRawAmount(sampleOutput, 0);
        var minimumAmountOut = individualTrades.Aggregate(zeroOut, (sum, t) => sum.Add(MinOut(t, options.SlippageTolerance)));
        var quoteAmountOut = individualTrades.Aggregate(zeroOut, (sum, t) => sum.Add(OutAmount(t)));
        var totalAmountIn = individualTrades.Aggregate(zeroIn, (sum, t) => sum.Add(MaxIn(t, options.SlippageTolerance)));

        return new EncodeSwapsResult
        {
            Calldatas = calldatas,
            SampleTrade = sampleTrade,
            RouterMustCustody = routerMustCustody,
            InputIsNative = inputIsNative,
            OutputIsNative = outputIsNative,
            TotalAmountIn = totalAmountIn,
            MinimumAmountOut = minimumAmountOut,
            QuoteAmountOut = quoteAmountOut,
        };
    }

    /// <summary>Produces the calldata + value for a given trade or set of trades.</summary>
    public static MethodParameters SwapCallParameters(object trades, SwapOptions options)
    {
        var r = EncodeSwaps(trades, options);
        var calldatas = r.Calldatas;

        if (r.RouterMustCustody)
        {
            if (r.OutputIsNative)
            {
                calldatas.Add(PaymentsExtended.EncodeUnwrapWETH9(r.MinimumAmountOut.Quotient, options.Recipient, options.Fee));
            }
            else
            {
                calldatas.Add(PaymentsExtended.EncodeSweepToken(
                    OutCurrency(r.SampleTrade).Wrapped(), r.MinimumAmountOut.Quotient, options.Recipient, options.Fee));
            }
        }

        // must refund when paying in ETH with an uncertain input amount OR when there's a chance of partial fill.
        if (r.InputIsNative && (TType(r.SampleTrade) == TradeType.EXACT_OUTPUT || RiskOfPartialFill(trades)))
        {
            calldatas.Add(V3.Payments.EncodeRefundETH());
        }

        return new MethodParameters
        {
            Calldata = MulticallExtended.EncodeMulticall(calldatas, options.DeadlineOrPreviousBlockhash),
            Value = Utilities.ToHex(r.InputIsNative ? r.TotalAmountIn.Quotient : BigInteger.Zero),
        };
    }

    /// <summary>Produces the calldata + value for a swap-and-add (swap then add liquidity to a V3 position).</summary>
    public static MethodParameters SwapAndAddCallParameters(
        object trades,
        SwapAndAddOptions options,
        V3.Entities.Position position,
        CondensedAddLiquidityOptions addLiquidityOptions,
        ApprovalTypes tokenInApprovalType,
        ApprovalTypes tokenOutApprovalType)
    {
        var r = EncodeSwaps(trades, options, isSwapAndAdd: true);
        var calldatas = r.Calldatas;
        var totalAmountSwapped = r.TotalAmountIn;

        if (options.OutputTokenPermit != null)
        {
            if (!r.QuoteAmountOut.Currency.IsToken)
            {
                throw new InvalidOperationException("NON_TOKEN_PERMIT_OUTPUT");
            }
            calldatas.Add(V3.SelfPermit.EncodePermit((Token)r.QuoteAmountOut.Currency, options.OutputTokenPermit));
        }

        int chainId = TradeChainId(r.SampleTrade);
        bool zeroForOne = position.Pool.Token0.Wrapped().Address == totalAmountSwapped.Currency.Wrapped().Address;
        var (positionAmountIn, positionAmountOut) = GetPositionAmounts(position, zeroForOne);

        // if tokens are native they will be converted to WETH9
        var tokenIn = r.InputIsNative ? Weth9.Tokens[chainId] : positionAmountIn.Currency.Wrapped();
        var tokenOut = r.OutputIsNative ? Weth9.Tokens[chainId] : positionAmountOut.Currency.Wrapped();

        // if swap output doesn't make up the whole desired balance, pull in the remaining tokens for adding liquidity
        var amountOutRemaining = positionAmountOut.Subtract(r.QuoteAmountOut.Wrapped()!);
        if (amountOutRemaining.GreaterThan(CurrencyAmount<Token>.FromRawAmount(positionAmountOut.Currency, 0)))
        {
            if (r.OutputIsNative)
            {
                calldatas.Add(PaymentsExtended.EncodeWrapETH(amountOutRemaining.Quotient));
            }
            else
            {
                calldatas.Add(PaymentsExtended.EncodePull(tokenOut, amountOutRemaining.Quotient));
            }
        }

        // if input is native, convert to WETH9, else pull ERC20 token
        if (r.InputIsNative)
        {
            calldatas.Add(PaymentsExtended.EncodeWrapETH(positionAmountIn.Quotient));
        }
        else
        {
            calldatas.Add(PaymentsExtended.EncodePull(tokenIn, positionAmountIn.Quotient));
        }

        // approve token balances to the NFTManager
        if (tokenInApprovalType != ApprovalTypes.NOT_REQUIRED)
        {
            calldatas.Add(ApproveAndCall.EncodeApprove(tokenIn, tokenInApprovalType));
        }
        if (tokenOutApprovalType != ApprovalTypes.NOT_REQUIRED)
        {
            calldatas.Add(ApproveAndCall.EncodeApprove(tokenOut, tokenOutApprovalType));
        }

        // position with token amounts resulting from a swap with maximum slippage (hence minimal amount out possible)
        var minimalPosition = V3.Entities.Position.FromAmounts(
            position.Pool,
            position.TickLower,
            position.TickUpper,
            zeroForOne ? position.Amount0.Quotient : r.MinimumAmountOut.Quotient,
            zeroForOne ? r.MinimumAmountOut.Quotient : position.Amount1.Quotient,
            UseFullPrecision: false);

        calldatas.Add(ApproveAndCall.EncodeAddLiquidity(position, minimalPosition, addLiquidityOptions, options.SlippageTolerance));

        // sweep remaining tokens
        calldatas.Add(r.InputIsNative
            ? PaymentsExtended.EncodeUnwrapWETH9(BigInteger.Zero)
            : PaymentsExtended.EncodeSweepToken(tokenIn, BigInteger.Zero));
        calldatas.Add(r.OutputIsNative
            ? PaymentsExtended.EncodeUnwrapWETH9(BigInteger.Zero)
            : PaymentsExtended.EncodeSweepToken(tokenOut, BigInteger.Zero));

        BigInteger value;
        if (r.InputIsNative)
        {
            value = totalAmountSwapped.Wrapped()!.Add(positionAmountIn.Wrapped()!).Quotient;
        }
        else if (r.OutputIsNative)
        {
            value = amountOutRemaining.Quotient;
        }
        else
        {
            value = BigInteger.Zero;
        }

        return new MethodParameters
        {
            Calldata = MulticallExtended.EncodeMulticall(calldatas, options.DeadlineOrPreviousBlockhash),
            Value = value.ToString(),
        };
    }

    private static (CurrencyAmount<Token> positionAmountIn, CurrencyAmount<Token> positionAmountOut) GetPositionAmounts(
        V3.Entities.Position position, bool zeroForOne)
    {
        var (amount0, amount1) = position.MintAmounts;
        var currencyAmount0 = CurrencyAmount<Token>.FromRawAmount(position.Pool.Token0, amount0);
        var currencyAmount1 = CurrencyAmount<Token>.FromRawAmount(position.Pool.Token1, amount1);
        return zeroForOne ? (currencyAmount0, currencyAmount1) : (currencyAmount1, currencyAmount0);
    }

    private static int TradeChainId(object trade) => trade switch
    {
        V2TradeT v2 => v2.Route.ChainId,
        V3TradeT v3 => v3.Swaps[0].Route.ChainId,
        MixedTradeT m => m.Swaps[0].Route.ChainId,
        _ => throw new ArgumentException("Unsupported trade"),
    };

    private static bool RiskOfPartialFill(object trades)
    {
        if (trades is IEnumerable enumerable and not string and not RouterTradeT)
        {
            return enumerable.Cast<object>().Any(V3TradeWithHighPriceImpact);
        }
        return V3TradeWithHighPriceImpact(trades);
    }

    private static bool V3TradeWithHighPriceImpact(object trade) =>
        trade is not V2TradeT && PriceImpactOf(trade).GreaterThan(REFUND_ETH_PRICE_IMPACT_THRESHOLD);

    // ---- polymorphic accessors over the heterogeneous trade union ----
    private static BaseCurrency InCurrency(object t) => t switch
    {
        V2TradeT v2 => v2.InputAmount.Currency,
        V3TradeT v3 => v3.InputAmount.Currency,
        MixedTradeT m => m.InputAmount.Currency,
        RouterTradeT r => r.InputAmount.Currency,
        _ => throw new ArgumentException("Unsupported trade"),
    };

    private static BaseCurrency OutCurrency(object t) => t switch
    {
        V2TradeT v2 => v2.OutputAmount.Currency,
        V3TradeT v3 => v3.OutputAmount.Currency,
        MixedTradeT m => m.OutputAmount.Currency,
        RouterTradeT r => r.OutputAmount.Currency,
        _ => throw new ArgumentException("Unsupported trade"),
    };

    private static TradeType TType(object t) => t switch
    {
        V2TradeT v2 => v2.TradeType,
        V3TradeT v3 => v3.TradeType,
        MixedTradeT m => m.TradeType,
        RouterTradeT r => r.TradeType,
        _ => throw new ArgumentException("Unsupported trade"),
    };

    private static CurrencyAmount<BaseCurrency> OutAmount(object t) => t switch
    {
        V2TradeT v2 => v2.OutputAmount,
        V3TradeT v3 => v3.OutputAmount,
        MixedTradeT m => m.OutputAmount,
        RouterTradeT r => r.OutputAmount,
        _ => throw new ArgumentException("Unsupported trade"),
    };

    private static CurrencyAmount<BaseCurrency> MinOut(object t, Percent slippage) => t switch
    {
        V2TradeT v2 => v2.MinimumAmountOut(slippage),
        V3TradeT v3 => v3.MinimumAmountOut(slippage),
        MixedTradeT m => m.MinimumAmountOut(slippage),
        RouterTradeT r => r.MinimumAmountOut(slippage),
        _ => throw new ArgumentException("Unsupported trade"),
    };

    private static CurrencyAmount<BaseCurrency> MaxIn(object t, Percent slippage) => t switch
    {
        V2TradeT v2 => v2.MaximumAmountIn(slippage),
        V3TradeT v3 => v3.MaximumAmountIn(slippage),
        MixedTradeT m => m.MaximumAmountIn(slippage),
        RouterTradeT r => r.MaximumAmountIn(slippage),
        _ => throw new ArgumentException("Unsupported trade"),
    };

    private static Percent PriceImpactOf(object t) => t switch
    {
        V3TradeT v3 => v3.PriceImpact,
        MixedTradeT m => m.PriceImpact,
        RouterTradeT r => r.PriceImpact,
        _ => throw new ArgumentException("Unsupported trade"),
    };

    private static int SwapsCount(object t) => t switch
    {
        V3TradeT v3 => v3.Swaps.Count,
        MixedTradeT m => m.Swaps.Count,
        _ => 1,
    };
}
