using System.Globalization;
using System.Numerics;
using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.Core.Utils;
using UniswapSharp.V2.Entities;

namespace UniswapSharp.V2;

/// <summary>
/// Options for producing the arguments to send call to the router. Set either <see cref="Ttl"/> (seconds from now)
/// or <see cref="Deadline"/> (absolute unix seconds); upstream models these as two distinct option shapes.
/// </summary>
public class TradeOptions
{
    /// <summary>How much the execution price is allowed to move unfavorably from the trade execution price.</summary>
    public required Percent AllowedSlippage { get; init; }

    /// <summary>
    /// How long the swap is valid until it expires, in seconds. Used to produce a <c>deadline</c> computed from when
    /// the swap call parameters are generated.
    /// </summary>
    public int? Ttl { get; init; }

    /// <summary>
    /// When the transaction expires (absolute unix seconds). An alternate to specifying the <see cref="Ttl"/>, for
    /// when you do not want to use local time.
    /// </summary>
    public int? Deadline { get; init; }

    /// <summary>The account that should receive the output of the swap.</summary>
    public required string Recipient { get; init; }

    /// <summary>
    /// Whether any of the tokens in the path are fee on transfer tokens, which should be handled with special methods.
    /// </summary>
    public bool FeeOnTransfer { get; init; }
}

/// <summary>
/// The parameters to use in the call to the Uniswap V2 Router to execute a trade.
/// </summary>
public class SwapParameters
{
    /// <summary>The method to call on the Uniswap V2 Router.</summary>
    public required string MethodName { get; init; }
    /// <summary>The arguments to pass to the method, all hex encoded. Each element is a <c>string</c> or <c>string[]</c>.</summary>
    public required IReadOnlyList<object> Args { get; init; }
    /// <summary>The amount of wei to send in hex.</summary>
    public required string Value { get; init; }
}

/// <summary>
/// Port of v2-sdk <c>router.ts</c>. Represents the Uniswap V2 Router, and has static methods for helping execute trades.
/// </summary>
public static class Router
{
    private const string ZeroHex = "0x0";

    private static string ToHex(BigInteger value)
    {
        if (value.IsZero)
        {
            return "0x0";
        }
        var hex = value.ToString("x", CultureInfo.InvariantCulture).TrimStart('0');
        return "0x" + (hex.Length == 0 ? "0" : hex);
    }

    /// <summary>
    /// Produces the on-chain method name to call and the hex encoded parameters to pass as arguments for a given trade.
    /// </summary>
    public static SwapParameters SwapCallParameters<TInput, TOutput>(
        Trade<TInput, TOutput> trade,
        TradeOptions options)
        where TInput : BaseCurrency
        where TOutput : BaseCurrency
    {
        var etherIn = trade.InputAmount.Currency.IsNative;
        var etherOut = trade.OutputAmount.Currency.IsNative;
        // the router does not support both ether in and out
        if (etherIn && etherOut) throw new ArgumentException("ETHER_IN_OUT");
        if (options.Ttl.HasValue && options.Ttl.Value <= 0) throw new ArgumentException("TTL");

        var to = AddressValidator.ValidateAndParseAddress(options.Recipient);
        var amountIn = ToHex(trade.MaximumAmountIn(options.AllowedSlippage).Quotient);
        var amountOut = ToHex(trade.MinimumAmountOut(options.AllowedSlippage).Quotient);
        var path = trade.Route.Path.Select(token => token.Address).ToArray();
        var deadline = options.Ttl.HasValue
            ? ToHex(new BigInteger(DateTimeOffset.UtcNow.ToUnixTimeSeconds() + options.Ttl.Value))
            : ToHex(new BigInteger(options.Deadline!.Value));

        var useFeeOnTransfer = options.FeeOnTransfer;

        string methodName;
        IReadOnlyList<object> args;
        string value;
        switch (trade.TradeType)
        {
            case TradeType.EXACT_INPUT:
                if (etherIn)
                {
                    methodName = useFeeOnTransfer ? "swapExactETHForTokensSupportingFeeOnTransferTokens" : "swapExactETHForTokens";
                    // (uint amountOutMin, address[] calldata path, address to, uint deadline)
                    args = new object[] { amountOut, path, to, deadline };
                    value = amountIn;
                }
                else if (etherOut)
                {
                    methodName = useFeeOnTransfer ? "swapExactTokensForETHSupportingFeeOnTransferTokens" : "swapExactTokensForETH";
                    // (uint amountIn, uint amountOutMin, address[] calldata path, address to, uint deadline)
                    args = new object[] { amountIn, amountOut, path, to, deadline };
                    value = ZeroHex;
                }
                else
                {
                    methodName = useFeeOnTransfer
                        ? "swapExactTokensForTokensSupportingFeeOnTransferTokens"
                        : "swapExactTokensForTokens";
                    // (uint amountIn, uint amountOutMin, address[] calldata path, address to, uint deadline)
                    args = new object[] { amountIn, amountOut, path, to, deadline };
                    value = ZeroHex;
                }
                break;
            case TradeType.EXACT_OUTPUT:
                if (useFeeOnTransfer) throw new ArgumentException("EXACT_OUT_FOT");
                if (etherIn)
                {
                    methodName = "swapETHForExactTokens";
                    // (uint amountOut, address[] calldata path, address to, uint deadline)
                    args = new object[] { amountOut, path, to, deadline };
                    value = amountIn;
                }
                else if (etherOut)
                {
                    methodName = "swapTokensForExactETH";
                    // (uint amountOut, uint amountInMax, address[] calldata path, address to, uint deadline)
                    args = new object[] { amountOut, amountIn, path, to, deadline };
                    value = ZeroHex;
                }
                else
                {
                    methodName = "swapTokensForExactTokens";
                    // (uint amountOut, uint amountInMax, address[] calldata path, address to, uint deadline)
                    args = new object[] { amountOut, amountIn, path, to, deadline };
                    value = ZeroHex;
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(trade), trade.TradeType, "Unknown trade type");
        }

        return new SwapParameters
        {
            MethodName = methodName,
            Args = args,
            Value = value
        };
    }
}
