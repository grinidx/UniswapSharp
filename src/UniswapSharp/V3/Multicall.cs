using Nethereum.ABI;
using Nethereum.Hex.HexConvertors.Extensions;
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
}
