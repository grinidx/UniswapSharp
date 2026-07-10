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

    // Ethers' Interface.encodeFunctionData equivalent: 4-byte selector
    // (keccak256 of the canonical signature) followed by the standard,
    // 32-byte-padded ABI encoding of the arguments.
    private static string EncodeFunctionData(string signature, params ABIValue[] parameters)
    {
        string hash = Sha3Keccack.Current.CalculateHash(signature);
        if (hash.StartsWith("0x"))
        {
            hash = hash.Substring(2);
        }
        string selector = hash.Substring(0, 8);
        string encodedParams = parameters.Length == 0 ? string.Empty : INTERFACE.GetABIEncoded(parameters).ToHex();
        return "0x" + selector + encodedParams;
    }

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
        public Percent Fee { get; set; }
        public string Recipient { get; set; }
    }
}
