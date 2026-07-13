using System.Numerics;
using Nethereum.ABI;
using Nethereum.Hex.HexConvertors.Extensions;
using UniswapSharp.Core.Entities;
using static UniswapSharp.V3.Utils.AbiFunctionEncoder;

namespace UniswapSharp.V3;

public static class SelfPermit
{
    /// <summary>
    /// Marker for the upstream union <c>PermitOptions = StandardPermitArguments | AllowedPermitArguments</c>.
    /// </summary>
    public interface IPermitOptions;

    public static string EncodePermit(Token token, IPermitOptions options)
    {
        if (options is IAllowedPermitArguments allowedOptions)
        {
            return EncodeFunctionData("selfPermitAllowed(address,uint256,uint256,uint8,bytes32,bytes32)",
                new ABIValue("address", token.Address),
                new ABIValue("uint256", allowedOptions.Nonce),
                new ABIValue("uint256", allowedOptions.Expiry),
                new ABIValue("uint8", allowedOptions.V),
                new ABIValue("bytes32", allowedOptions.R.HexToByteArray()),
                new ABIValue("bytes32", allowedOptions.S.HexToByteArray()));
        }
        else if (options is IStandardPermitArguments standardOptions)
        {
            return EncodeFunctionData("selfPermit(address,uint256,uint256,uint8,bytes32,bytes32)",
                new ABIValue("address", token.Address),
                new ABIValue("uint256", standardOptions.Amount),
                new ABIValue("uint256", standardOptions.Deadline),
                new ABIValue("uint8", standardOptions.V),
                new ABIValue("bytes32", standardOptions.R.HexToByteArray()),
                new ABIValue("bytes32", standardOptions.S.HexToByteArray()));
        }
        else
        {
            throw new ArgumentException("Invalid permit options");
        }
    }

    public interface IAllowedPermitArguments : IPermitOptions
    {
        byte V { get; }
        string R { get; }
        string S { get; }
        BigInteger Nonce { get; }
        BigInteger Expiry { get; }
    }

    public interface IStandardPermitArguments : IPermitOptions
    {
        byte V { get; }
        string R { get; }
        string S { get; }
        BigInteger Amount { get; }
        BigInteger Deadline { get; }
    }

    public class AllowedPermitArguments : IAllowedPermitArguments
    {
        public byte V { get; set; }
        public required string R { get; set; }
        public required string S { get; set; }
        public BigInteger Nonce { get; set; }
        public BigInteger Expiry { get; set; }
    }

    public class StandardPermitArguments : IStandardPermitArguments
    {
        public byte V { get; set; }
        public required string R { get; set; }
        public required string S { get; set; }
        public BigInteger Amount { get; set; }
        public BigInteger Deadline { get; set; }
    }
}
