using System.Numerics;
using Nethereum.ABI;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;

namespace UniswapSharp.V3;

public abstract class Payments
{
    public static ABIEncode INTERFACE = new ABIEncode();

    private Payments() { }

    private static string EncodeFeeBips(Percent fee)
    {
        return fee.Multiply(10_000).Quotient.ToHex(false);
    }

    public static string EncodeUnwrapWETH9(BigInteger amountMinimum, string recipient, IFeeOptions feeOptions = null)
    {
        recipient = AddressUtil.Current.ConvertToChecksumAddress(recipient);

        if (feeOptions != null)
        {
            var feeBips = EncodeFeeBips(feeOptions.Fee);
            var feeRecipient = AddressUtil.Current.ConvertToChecksumAddress(feeOptions.Recipient);

            //return INTERFACE.GetABIEncodedTransactionData("unwrapWETH9WithFee",
            //    amountMinimum.ToHex(),
            //    recipient,
            //    feeBips,
            //    feeRecipient);
        }
        else
        {
            //return INTERFACE.GetABIEncodedTransactionData("unwrapWETH9",
            //    amountMinimum.ToHex(),
            //    recipient);
        }
        throw new NotImplementedException();
    }

    public static string EncodeSweepToken(Token token, BigInteger amountMinimum, string recipient, IFeeOptions feeOptions = null)
    {
        recipient = AddressUtil.Current.ConvertToChecksumAddress(recipient);

        if (feeOptions != null)
        {
            var feeBips = EncodeFeeBips(feeOptions.Fee);
            var feeRecipient = AddressUtil.Current.ConvertToChecksumAddress(feeOptions.Recipient);

            //return INTERFACE.GetABIEncodedTransactionData("sweepTokenWithFee",
            //    token.Address,
            //    amountMinimum.ToHex(false),
            //    recipient,
            //    feeBips,
            //    feeRecipient);
        }
        else
        {
            //return INTERFACE.GetABIEncodedTransactionData("sweepToken",
            //    token.Address,
            //    amountMinimum.ToHex(false),
            //    recipient);
        }

        throw new NotImplementedException();
    }

    public static string EncodeRefundETH()
    {
        //return INTERFACE.GetABIEncodedTransactionData("refundETH");
        throw new NotImplementedException();
    }
    public interface IFeeOptions
    {
        Percent Fee { get; set; }
        string Recipient { get; set; }
    }

    public class FeeOptions : IFeeOptions
    {
        public Percent Fee { get; set; }
        public string Recipient { get; set; }
    }

}
