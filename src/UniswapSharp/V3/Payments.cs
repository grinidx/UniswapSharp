using System.Numerics;
using Nethereum.ABI;
using Nethereum.Util;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using static UniswapSharp.V3.Utils.AbiFunctionEncoder;

namespace UniswapSharp.V3;

public abstract class Payments
{
    public static ABIEncode INTERFACE = new ABIEncode();

    private Payments() { }

    private static BigInteger EncodeFeeBips(Percent fee)
    {
        return fee.Multiply(10_000).Quotient;
    }

    public static string EncodeUnwrapWETH9(BigInteger amountMinimum, string recipient, IFeeOptions? feeOptions = null)
    {
        recipient = AddressUtil.Current.ConvertToChecksumAddress(recipient);

        if (feeOptions != null)
        {
            var feeBips = EncodeFeeBips(feeOptions.Fee);
            var feeRecipient = AddressUtil.Current.ConvertToChecksumAddress(feeOptions.Recipient);

            return EncodeFunctionData("unwrapWETH9WithFee(uint256,address,uint256,address)",
                new ABIValue("uint256", amountMinimum),
                new ABIValue("address", recipient),
                new ABIValue("uint256", feeBips),
                new ABIValue("address", feeRecipient));
        }

        return EncodeFunctionData("unwrapWETH9(uint256,address)",
            new ABIValue("uint256", amountMinimum),
            new ABIValue("address", recipient));
    }

    public static string EncodeSweepToken(Token token, BigInteger amountMinimum, string recipient, IFeeOptions? feeOptions = null)
    {
        recipient = AddressUtil.Current.ConvertToChecksumAddress(recipient);

        if (feeOptions != null)
        {
            var feeBips = EncodeFeeBips(feeOptions.Fee);
            var feeRecipient = AddressUtil.Current.ConvertToChecksumAddress(feeOptions.Recipient);

            return EncodeFunctionData("sweepTokenWithFee(address,uint256,address,uint256,address)",
                new ABIValue("address", token.Address),
                new ABIValue("uint256", amountMinimum),
                new ABIValue("address", recipient),
                new ABIValue("uint256", feeBips),
                new ABIValue("address", feeRecipient));
        }

        return EncodeFunctionData("sweepToken(address,uint256,address)",
            new ABIValue("address", token.Address),
            new ABIValue("uint256", amountMinimum),
            new ABIValue("address", recipient));
    }

    public static string EncodeRefundETH()
    {
        return EncodeFunctionData("refundETH()");
    }

    public interface IFeeOptions
    {
        Percent Fee { get; set; }
        string Recipient { get; set; }
    }

    public class FeeOptions : IFeeOptions
    {
        public required Percent Fee { get; set; }
        public required string Recipient { get; set; }
    }
}
