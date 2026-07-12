using System.Numerics;
using Nethereum.ABI;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.Core.Utils;
using UniswapSharp.V3;
using static UniswapSharp.V3.Utils.AbiFunctionEncoder;

namespace UniswapSharp.Router;

/// <summary>
/// Port of router-sdk <c>paymentsExtended.ts</c>. Extends V3 <c>Payments</c> with recipient-optional
/// variants (funds default to <c>msg.sender</c>) plus <c>pull</c> and <c>wrapETH</c>.
/// </summary>
public abstract class PaymentsExtended
{
    private PaymentsExtended() { }

    private static BigInteger EncodeFeeBips(Percent fee) => fee.Multiply(10_000).Quotient;

    public static string EncodeUnwrapWETH9(BigInteger amountMinimum, string? recipient = null, Payments.IFeeOptions? feeOptions = null)
    {
        // if there's a recipient, just pass it along
        if (recipient is not null)
        {
            return Payments.EncodeUnwrapWETH9(amountMinimum, recipient, feeOptions);
        }

        if (feeOptions is not null)
        {
            var feeBips = EncodeFeeBips(feeOptions.Fee);
            var feeRecipient = AddressValidator.ValidateAndParseAddress(feeOptions.Recipient);

            return EncodeFunctionData("unwrapWETH9WithFee(uint256,uint256,address)",
                new ABIValue("uint256", amountMinimum),
                new ABIValue("uint256", feeBips),
                new ABIValue("address", feeRecipient));
        }

        return EncodeFunctionData("unwrapWETH9(uint256)", new ABIValue("uint256", amountMinimum));
    }

    public static string EncodeSweepToken(Token token, BigInteger amountMinimum, string? recipient = null, Payments.IFeeOptions? feeOptions = null)
    {
        // if there's a recipient, just pass it along
        if (recipient is not null)
        {
            return Payments.EncodeSweepToken(token, amountMinimum, recipient, feeOptions);
        }

        if (feeOptions is not null)
        {
            var feeBips = EncodeFeeBips(feeOptions.Fee);
            var feeRecipient = AddressValidator.ValidateAndParseAddress(feeOptions.Recipient);

            return EncodeFunctionData("sweepTokenWithFee(address,uint256,uint256,address)",
                new ABIValue("address", token.Address),
                new ABIValue("uint256", amountMinimum),
                new ABIValue("uint256", feeBips),
                new ABIValue("address", feeRecipient));
        }

        return EncodeFunctionData("sweepToken(address,uint256)",
            new ABIValue("address", token.Address),
            new ABIValue("uint256", amountMinimum));
    }

    public static string EncodePull(Token token, BigInteger amount)
    {
        return EncodeFunctionData("pull(address,uint256)",
            new ABIValue("address", token.Address),
            new ABIValue("uint256", amount));
    }

    public static string EncodeWrapETH(BigInteger amount)
    {
        return EncodeFunctionData("wrapETH(uint256)", new ABIValue("uint256", amount));
    }
}
