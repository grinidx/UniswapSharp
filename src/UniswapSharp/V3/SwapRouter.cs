using System.Numerics;
using Nethereum.ABI;
using Nethereum.Hex.HexConvertors.Extensions;
using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.Core.Utils;
using UniswapSharp.V3.Entities;
using UniswapSharp.V3.Utils;
using static UniswapSharp.V3.Utils.AbiFunctionEncoder;

namespace UniswapSharp.V3;

public abstract class SwapRouter
{
    private SwapRouter() { }

    public static NonfungiblePositionManager.MethodParameters SwapCallParameters<TInput, TOutput>(
        Trade<TInput, TOutput> trade,
        Staker.SwapOptions options)
        where TInput : BaseCurrency where TOutput : BaseCurrency =>
        SwapCallParameters(new[] { trade }, options);

    public static NonfungiblePositionManager.MethodParameters SwapCallParameters<TInput, TOutput>(
        IReadOnlyList<Trade<TInput, TOutput>> trades,
        Staker.SwapOptions options)
        where TInput : BaseCurrency where TOutput : BaseCurrency
    {
        if (trades.Count == 0)
        {
            throw new ArgumentException("At least one trade is required", nameof(trades));
        }

        var sampleTrade = trades[0];
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

        var zeroIn = CurrencyAmount<TInput>.FromRawAmount(sampleTrade.InputAmount.Currency, BigInteger.Zero);
        var zeroOut = CurrencyAmount<TOutput>.FromRawAmount(sampleTrade.OutputAmount.Currency, BigInteger.Zero);

        var totalAmountOut = trades.Aggregate(zeroOut, (sum, trade) => sum.Add(trade.MinimumAmountOut(options.SlippageTolerance)));

        // flags
        bool mustRefund = sampleTrade.InputAmount.Currency.IsNative && sampleTrade.TradeType == TradeType.EXACT_OUTPUT;
        bool inputIsNative = sampleTrade.InputAmount.Currency.IsNative;
        bool outputIsNative = sampleTrade.OutputAmount.Currency.IsNative;
        bool routerMustCustody = outputIsNative || options.Fee != null;

        var totalValue = inputIsNative
            ? trades.Aggregate(zeroIn, (sum, trade) => sum.Add(trade.MaximumAmountIn(options.SlippageTolerance)))
            : zeroIn;

        if (options.InputTokenPermit != null)
        {
            if (!sampleTrade.InputAmount.Currency.IsToken)
            {
                throw new InvalidOperationException("NON_TOKEN_PERMIT");
            }
            calldatas.Add(SelfPermit.EncodePermit((Token)(object)sampleTrade.InputAmount.Currency, options.InputTokenPermit));
        }

        string recipient = AddressValidator.ValidateAndParseAddress(options.Recipient);
        BigInteger deadline = options.Deadline;
        BigInteger sqrtPriceLimitX96 = options.SqrtPriceLimitX96 ?? BigInteger.Zero;

        foreach (var trade in trades)
        {
            foreach (var swap in trade.Swaps)
            {
                BigInteger amountIn = trade.MaximumAmountIn(options.SlippageTolerance, swap.InputAmount).Quotient;
                BigInteger amountOut = trade.MinimumAmountOut(options.SlippageTolerance, swap.OutputAmount).Quotient;

                bool singleHop = swap.Route.Pools.Count == 1;
                string swapRecipient = routerMustCustody ? Constants.ADDRESS_ZERO : recipient;

                if (singleHop)
                {
                    string tokenInAddress = swap.Route.TokenPath[0].Address;
                    string tokenOutAddress = swap.Route.TokenPath[1].Address;
                    int fee = (int)swap.Route.Pools[0].Fee;

                    if (trade.TradeType == TradeType.EXACT_INPUT)
                    {
                        calldatas.Add(EncodeFunctionData(
                            "exactInputSingle((address,address,uint24,address,uint256,uint256,uint256,uint160))",
                            new ABIValue("address", tokenInAddress),
                            new ABIValue("address", tokenOutAddress),
                            new ABIValue("uint24", fee),
                            new ABIValue("address", swapRecipient),
                            new ABIValue("uint256", deadline),
                            new ABIValue("uint256", amountIn),
                            new ABIValue("uint256", amountOut),
                            new ABIValue("uint160", sqrtPriceLimitX96)));
                    }
                    else
                    {
                        calldatas.Add(EncodeFunctionData(
                            "exactOutputSingle((address,address,uint24,address,uint256,uint256,uint256,uint160))",
                            new ABIValue("address", tokenInAddress),
                            new ABIValue("address", tokenOutAddress),
                            new ABIValue("uint24", fee),
                            new ABIValue("address", swapRecipient),
                            new ABIValue("uint256", deadline),
                            new ABIValue("uint256", amountOut),
                            new ABIValue("uint256", amountIn),
                            new ABIValue("uint160", sqrtPriceLimitX96)));
                    }
                }
                else
                {
                    if (options.SqrtPriceLimitX96.HasValue)
                    {
                        throw new InvalidOperationException("MULTIHOP_PRICE_LIMIT");
                    }

                    byte[] path = EncodeRouteToPath.Encode(swap.Route, trade.TradeType == TradeType.EXACT_OUTPUT).HexToByteArray();

                    if (trade.TradeType == TradeType.EXACT_INPUT)
                    {
                        calldatas.Add(EncodeFunctionDataDynamicTuple(
                            "exactInput((bytes,address,uint256,uint256,uint256))",
                            new ABIValue("bytes", path),
                            new ABIValue("address", swapRecipient),
                            new ABIValue("uint256", deadline),
                            new ABIValue("uint256", amountIn),
                            new ABIValue("uint256", amountOut)));
                    }
                    else
                    {
                        calldatas.Add(EncodeFunctionDataDynamicTuple(
                            "exactOutput((bytes,address,uint256,uint256,uint256))",
                            new ABIValue("bytes", path),
                            new ABIValue("address", swapRecipient),
                            new ABIValue("uint256", deadline),
                            new ABIValue("uint256", amountOut),
                            new ABIValue("uint256", amountIn)));
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
                    calldatas.Add(Payments.EncodeSweepToken(sampleTrade.OutputAmount.Currency.Wrapped(), totalAmountOut.Quotient, recipient, options.Fee));
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
            Value = Utilities.ToHex(totalValue.Quotient)
        };
    }
}
