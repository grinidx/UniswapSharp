using System.Numerics;
using Nethereum.ABI;
using Nethereum.Hex.HexConvertors.Extensions;
using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.V3.Entities;
using UniswapSharp.V3.Utils;
using static UniswapSharp.V3.Utils.AbiFunctionEncoder;

namespace UniswapSharp.V3;

public abstract class SwapQuoter
{
    public static NonfungiblePositionManager.MethodParameters QuoteCallParameters<TInput, TOutput>(
        Route<TInput, TOutput> route,
        CurrencyAmount<BaseCurrency> amount,
        TradeType tradeType,
        QuoteOptions? options = null) where TInput : BaseCurrency where TOutput : BaseCurrency
    {
        options ??= new QuoteOptions();
        bool singleHop = route.Pools.Count == 1;
        BigInteger quoteAmount = amount.Quotient;
        string calldata;

        if (singleHop)
        {
            string tokenIn = route.TokenPath[0].Address;
            string tokenOut = route.TokenPath[1].Address;
            int fee = (int)route.Pools[0].Fee;
            BigInteger sqrtPriceLimitX96 = options.SqrtPriceLimitX96 ?? BigInteger.Zero;

            if (options.UseQuoterV2)
            {
                // QuoterV2 takes a single struct argument. Its members are all static,
                // so the tuple encodes as its fields concatenated — identical bytes to a
                // flat encoding — while the tuple signature drives a distinct selector.
                string fn = tradeType == TradeType.EXACT_INPUT
                    ? "quoteExactInputSingle((address,address,uint256,uint24,uint160))"
                    : "quoteExactOutputSingle((address,address,uint256,uint24,uint160))";
                calldata = EncodeFunctionData(fn,
                    new ABIValue("address", tokenIn),
                    new ABIValue("address", tokenOut),
                    new ABIValue("uint256", quoteAmount),
                    new ABIValue("uint24", fee),
                    new ABIValue("uint160", sqrtPriceLimitX96));
            }
            else
            {
                string fn = tradeType == TradeType.EXACT_INPUT
                    ? "quoteExactInputSingle(address,address,uint24,uint256,uint160)"
                    : "quoteExactOutputSingle(address,address,uint24,uint256,uint160)";
                calldata = EncodeFunctionData(fn,
                    new ABIValue("address", tokenIn),
                    new ABIValue("address", tokenOut),
                    new ABIValue("uint24", fee),
                    new ABIValue("uint256", quoteAmount),
                    new ABIValue("uint160", sqrtPriceLimitX96));
            }
        }
        else
        {
            if (options.SqrtPriceLimitX96.HasValue)
            {
                throw new InvalidOperationException("MULTIHOP_PRICE_LIMIT");
            }

            string path = EncodeRouteToPath.Encode(route, tradeType == TradeType.EXACT_OUTPUT);
            string fn = tradeType == TradeType.EXACT_INPUT
                ? "quoteExactInput(bytes,uint256)"
                : "quoteExactOutput(bytes,uint256)";
            calldata = EncodeFunctionData(fn,
                new ABIValue("bytes", path.HexToByteArray()),
                new ABIValue("uint256", quoteAmount));
        }

        return new NonfungiblePositionManager.MethodParameters
        {
            Calldata = calldata,
            Value = Utilities.ToHex(BigInteger.Zero)
        };
    }

    public class QuoteOptions
    {
        public BigInteger? SqrtPriceLimitX96 { get; set; }
        public bool UseQuoterV2 { get; set; }
    }
}
