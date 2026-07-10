using Nethereum.ABI;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace UniswapSharp.V3.Utils;

/// <summary>
/// Equivalent of ethers' <c>Interface.encodeFunctionData</c>: the 4-byte selector
/// (first 4 bytes of keccak256 of the canonical function signature) followed by the
/// standard, 32-byte-padded ABI encoding of the arguments.
/// </summary>
public static class AbiFunctionEncoder
{
    private static readonly ABIEncode Abi = new();

    public static string EncodeFunctionData(string signature, params ABIValue[] parameters)
    {
        string hash = Sha3Keccack.Current.CalculateHash(signature);
        if (hash.StartsWith("0x"))
        {
            hash = hash.Substring(2);
        }
        string selector = hash.Substring(0, 8);
        string encodedParams = parameters.Length == 0 ? string.Empty : Abi.GetABIEncoded(parameters).ToHex();
        return "0x" + selector + encodedParams;
    }
}
