using Nethereum.ABI;
using Nethereum.Hex.HexConvertors.Extensions;
using UniswapSharp.V4.Utils;
using static UniswapSharp.V3.Utils.AbiFunctionEncoder;

namespace UniswapSharp.V3;

public abstract class Multicall
{
    private Multicall() { }

    public static string EncodeMulticall(string calldata)
    {
        return EncodeMulticall(new[] { calldata });
    }

    public static string EncodeMulticall(IEnumerable<string> calldatas)
    {
        var calldataList = new List<string>(calldatas);
        if (calldataList.Count == 1)
        {
            return calldataList[0];
        }

        var bytesArray = calldataList.Select(c => c.HexToByteArray()).ToArray();
        return EncodeFunctionData("multicall(bytes[])", new ABIValue("bytes[]", bytesArray));
    }

    /// <summary>
    /// Decodes an encoded <c>multicall(bytes[])</c> call back into its individual calldatas.
    /// Mirrors upstream <c>Multicall.decodeMulticall</c>.
    /// </summary>
    public static List<string> DecodeMulticall(string encodedCalldata)
    {
        // Strip the 4-byte (8 hex char) selector, then decode the single bytes[] argument.
        string hex = encodedCalldata.StartsWith("0x") || encodedCalldata.StartsWith("0X")
            ? encodedCalldata[2..]
            : encodedCalldata;
        string body = "0x" + hex[8..];
        var decoded = AbiParamDecoder.Decode(new[] { "bytes[]" }, body);
        var items = (List<object?>)decoded[0]!;
        return items.Select(x => (string)x!).ToList();
    }
}
