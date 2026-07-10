using System.Numerics;
using Nethereum.ABI;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.V3.Entities;
using UniswapSharp.V3.Utils;

namespace UniswapSharp.V3;

public abstract class SwapRouter
{
    public static ABIEncode INTERFACE = new ABIEncode();

    private SwapRouter() { }

    public static NonfungiblePositionManager.MethodParameters SwapCallParameters(
        IEnumerable<Trade<BaseCurrency, BaseCurrency>> trades,
        Staker.SwapOptions options)
    {
        if (!trades.Any())
        {
            throw new ArgumentException("At least one trade is required", nameof(trades));
        }

        var sampleTrade = trades.First();
        var tokenIn = sampleTrade.InputAmount.Currency.Wrapped();
        var tokenOut = sampleTrade.OutputAmount.Currency.Wrapped();

        // All trades should have the same starting and ending token.
        if (!trades.All(trade => trade.InputAmount.Currency.Wrapped().Equals(tokenIn)))
        {
            throw new InvalidOperationException("TOKEN_IN_DIFF");
        }
        if (!trades.All(trade => trade.OutputAmount.Currency.Wrapped().Equals(tokenOut)))
        {
            throw new InvalidOperationException("TOKEN_OUT_DIFF");
        }

        var calldatas = new List<string>();

        var zeroIn = CurrencyAmount<BaseCurrency>.FromRawAmount(trades.First().InputAmount.Currency, BigInteger.Zero);
        var zeroOut = CurrencyAmount<BaseCurrency>.FromRawAmount(trades.First().OutputAmount.Currency, BigInteger.Zero);

        var totalAmountOut = trades.Aggregate(zeroOut, (sum, trade) => sum.Add(trade.MinimumAmountOut(options.SlippageTolerance)));

        var mustRefund = sampleTrade.InputAmount.Currency.IsNative && sampleTrade.TradeType == TradeType.EXACT_OUTPUT;
        var inputIsNative = sampleTrade.InputAmount.Currency.IsNative;
        var outputIsNative = sampleTrade.OutputAmount.Currency.IsNative;
        var routerMustCustody = outputIsNative || options.Fee != null;

        var totalValue = inputIsNative
            ? trades.Aggregate(zeroIn, (sum, trade) => sum.Add(trade.MaximumAmountIn(options.SlippageTolerance)))
            : zeroIn;

        if (options.InputTokenPermit != null)
        {
            if (!sampleTrade.InputAmount.Currency.IsToken)
            {
                throw new InvalidOperationException("NON_TOKEN_PERMIT");
            }
            calldatas.Add(SelfPermit.EncodePermit((Token)sampleTrade.InputAmount.Currency, options.InputTokenPermit));
        }

        var recipient = AddressUtil.Current.ConvertToChecksumAddress(options.Recipient);
        var deadline = options.Deadline.ToHex(false);

        foreach (var trade in trades)
        {
            foreach (var swap in trade.Swaps)
            {
                var amountIn = trade.MaximumAmountIn(options.SlippageTolerance, swap.InputAmount).Quotient.ToHex(false);
                var amountOut = trade.MinimumAmountOut(options.SlippageTolerance, swap.OutputAmount).Quotient.ToHex(false);

                var singleHop = swap.Route.Pools.Count == 1;

                if (singleHop)
                {
                    if (trade.TradeType == TradeType.EXACT_INPUT)
                    {
                        var exactInputSingleParams = new
                        {
                            tokenIn = swap.Route.TokenPath[0].Address,
                            tokenOut = swap.Route.TokenPath[1].Address,
                            fee = swap.Route.Pools[0].Fee,
                            recipient = routerMustCustody ? Constants.ADDRESS_ZERO : recipient,
                            deadline,
                            amountIn,
                            amountOutMinimum = amountOut,
                            sqrtPriceLimitX96 = (options.SqrtPriceLimitX96 ?? BigInteger.Zero).ToHex(false)
                        };

                        //calldatas.Add(INTERFACE.GetFunctionEncoder("exactInputSingle").EncodeParameters(new[] { exactInputSingleParams }));
                    }
                    else
                    {
                        var exactOutputSingleParams = new
                        {
                            tokenIn = swap.Route.TokenPath[0].Address,
                            tokenOut = swap.Route.TokenPath[1].Address,
                            fee = swap.Route.Pools[0].Fee,
                            recipient = routerMustCustody ? Constants.ADDRESS_ZERO : recipient,
                            deadline,
                            amountOut,
                            amountInMaximum = amountIn,
                            sqrtPriceLimitX96 = (options.SqrtPriceLimitX96 ?? BigInteger.Zero).ToHex(false)
                        };

                       // calldatas.Add(INTERFACE.GetFunctionEncoder("exactOutputSingle").EncodeParameters(new[] { exactOutputSingleParams }));
                    }
                }
                else
                {
                    if (options.SqrtPriceLimitX96.HasValue)
                    {
                        throw new InvalidOperationException("MULTIHOP_PRICE_LIMIT");
                    }

                    var path = EncodeRouteToPath.Encode(swap.Route, trade.TradeType == TradeType.EXACT_OUTPUT);

                    if (trade.TradeType == TradeType.EXACT_INPUT)
                    {
                        var exactInputParams = new
                        {
                            path,
                            recipient = routerMustCustody ? Constants.ADDRESS_ZERO : recipient,
                            deadline,
                            amountIn,
                            amountOutMinimum = amountOut
                        };

                      //  calldatas.Add(INTERFACE.GetFunctionEncoder("exactInput").EncodeParameters(new[] { exactInputParams }));
                    }
                    else
                    {
                        var exactOutputParams = new
                        {
                            path,
                            recipient = routerMustCustody ? Constants.ADDRESS_ZERO : recipient,
                            deadline,
                            amountOut,
                            amountInMaximum = amountIn
                        };

                       // calldatas.Add(INTERFACE.GetFunctionEncoder("exactOutput").EncodeParameters(new[] { exactOutputParams }));
                    }
                }
            }
        }

        if (routerMustCustody)
        {
            if (options.Fee != null)
            {
                if (outputIsNative)
                {
                    calldatas.Add(Payments.EncodeUnwrapWETH9(totalAmountOut.Quotient, recipient, options.Fee));
                }
                else
                {
                   // calldatas.Add(Payments.EncodeSweepToken(sampleTrade.OutputAmount.Currency.Wrapped, totalAmountOut.Quotient, recipient, options.Fee));
                }
            }
            else
            {
                calldatas.Add(Payments.EncodeUnwrapWETH9(totalAmountOut.Quotient, recipient));
            }
        }

        if (mustRefund)
        {
            calldatas.Add(Payments.EncodeRefundETH());
        }

        return new NonfungiblePositionManager.MethodParameters
        {
            Calldata = Multicall.EncodeMulticall(calldatas),
            Value = totalValue.Quotient.ToHex(false)
        };
    }
}