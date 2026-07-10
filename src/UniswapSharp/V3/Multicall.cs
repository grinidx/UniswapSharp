using Nethereum.Contracts;
using Newtonsoft.Json.Linq;

namespace UniswapSharp.V3;

public abstract class Multicall
{
    public static Contract INTERFACE;

    static Multicall()
    {
        string jsonContent = System.IO.File.ReadAllText("IMulticall.json");
        JObject jsonObject = JObject.Parse(jsonContent);
        JArray abiArray = (JArray)jsonObject["abi"];
        string abiString = abiArray.ToString();

        // INTERFACE = new Contract(null, abiString);
    }

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

        var function = INTERFACE.GetFunction("multicall");
        return function.GetData(calldataList);
    }
}
