using System.Numerics;
using System.Text.RegularExpressions;
using UniswapSharp.V4.Utils;
using static UniswapSharp.V3.Utils.AbiFunctionEncoder;

namespace UniswapSharp.Router;

/// <summary>
/// Port of router-sdk <c>multicallExtended.ts</c>. Wraps V3 <c>Multicall</c> to optionally attach a
/// deadline (<c>multicall(uint256,bytes[])</c>) or previous block hash (<c>multicall(bytes32,bytes[])</c>).
/// </summary>
public abstract class MulticallExtended
{
    private MulticallExtended() { }

    public static string EncodeMulticall(string calldata, object? validation = null) =>
        EncodeMulticall(new[] { calldata }, validation);

    public static string EncodeMulticall(IEnumerable<string> calldatas, object? validation = null)
    {
        // if there's no validation, we can just fall back to regular multicall
        if (validation is null)
        {
            return V3.Multicall.EncodeMulticall(calldatas);
        }

        var calldataList = calldatas.ToList();

        // this means the validation value should be a previousBlockhash
        if (validation is string s && (s.StartsWith("0x") || s.StartsWith("0X")))
        {
            string previousBlockhash = ValidateAndParseBytes32(s);
            string body = AbiParamEncoder.Encode(
                new[] { "bytes32", "bytes[]" },
                new object?[] { previousBlockhash, calldataList });
            return "0x" + Selector("multicall(bytes32,bytes[])") + body[2..];
        }
        else
        {
            BigInteger deadline = ToBigInteger(validation);
            string body = AbiParamEncoder.Encode(
                new[] { "uint256", "bytes[]" },
                new object?[] { deadline, calldataList });
            return "0x" + Selector("multicall(uint256,bytes[])") + body[2..];
        }
    }

    private static string ValidateAndParseBytes32(string bytes32)
    {
        if (!Regex.IsMatch(bytes32, "^0x[0-9a-fA-F]{64}$"))
        {
            throw new ArgumentException($"{bytes32} is not valid bytes32.");
        }
        return bytes32.ToLowerInvariant();
    }

    private static BigInteger ToBigInteger(object validation) => validation switch
    {
        BigInteger b => b,
        int i => i,
        long l => l,
        uint u => u,
        ulong ul => ul,
        string str => BigInteger.Parse(str),
        _ => throw new ArgumentException($"Cannot convert {validation.GetType().Name} to a deadline"),
    };
}
