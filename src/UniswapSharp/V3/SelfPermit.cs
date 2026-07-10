using System.Numerics;
using Nethereum.ABI;
using Nethereum.Hex.HexConvertors.Extensions;
using UniswapSharp.Core.Entities;

namespace UniswapSharp.V3;

public static class SelfPermit
{
    public static ABIEncode INTERFACE = new ABIEncode();



    public static string EncodePermit(Token token, object options)
    {
        if (options is IAllowedPermitArguments allowedOptions)
        {
            return INTERFACE.GetABIEncoded("selfPermitAllowed",
                token.Address,
                allowedOptions.Nonce.ToHex(false),
                allowedOptions.Expiry.ToHex(false),
                allowedOptions.V,
                allowedOptions.R,
                allowedOptions.S).ToHex();
        }
        else if (options is IStandardPermitArguments standardOptions)
        {
            return INTERFACE.GetABIEncoded("selfPermit",
                token.Address,
                standardOptions.Amount.ToHex(false),
                standardOptions.Deadline.ToHex(false),
                standardOptions.V,
                standardOptions.R,
                standardOptions.S).ToHex();
        }
        else
        {
            throw new ArgumentException("Invalid permit options");
        }
    }

    public interface IAllowedPermitArguments
    {
        byte V { get; }
        string R { get; }
        string S { get; }
        BigInteger Nonce { get; }
        BigInteger Expiry { get; }
    }
    public interface IStandardPermitArguments
    {
        byte V { get; }
        string R { get; }
        string S { get; }
        BigInteger Amount { get; }
        BigInteger Deadline { get; }
    }
}