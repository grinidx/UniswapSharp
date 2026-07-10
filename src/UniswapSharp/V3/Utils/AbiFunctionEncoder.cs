using System.Numerics;
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

    /// <summary>The 4-byte function selector (8 hex chars, no 0x) for a canonical signature.</summary>
    public static string Selector(string signature)
    {
        string hash = Sha3Keccack.Current.CalculateHash(signature);
        if (hash.StartsWith("0x"))
        {
            hash = hash.Substring(2);
        }
        return hash.Substring(0, 8);
    }

    public static string EncodeFunctionData(string signature, params ABIValue[] parameters)
    {
        string encodedParams = parameters.Length == 0 ? string.Empty : Abi.GetABIEncoded(parameters).ToHex();
        return "0x" + Selector(signature) + encodedParams;
    }

    /// <summary>
    /// Encodes a call whose single argument is a <em>dynamic</em> tuple (one containing a
    /// dynamic member such as <c>bytes</c>). Such a tuple is encoded as an offset (0x20)
    /// followed by the tuple's own head/tail encoding.
    /// </summary>
    public static string EncodeFunctionDataDynamicTuple(string signature, params ABIValue[] tupleFields)
    {
        string offset = Abi.GetABIEncoded(new ABIValue("uint256", (BigInteger)32)).ToHex();
        string tuple = Abi.GetABIEncoded(tupleFields).ToHex();
        return "0x" + Selector(signature) + offset + tuple;
    }
}
