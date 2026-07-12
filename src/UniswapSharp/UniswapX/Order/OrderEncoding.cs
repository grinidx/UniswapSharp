using System.Numerics;
using UniswapSharp.Permit2;

namespace UniswapSharp.UniswapX.Order;

/// <summary>Shared EIP-712 / ABI encoding helpers for the common <c>OrderInfo</c> struct present in every order.</summary>
internal static class OrderEncoding
{
    /// <summary>The <c>OrderInfo</c> EIP-712 field list (matches upstream <c>OrderInfo</c> type).</summary>
    public static readonly IReadOnlyList<TypedDataField> OrderInfoFields = new[]
    {
        new TypedDataField("reactor", "address"),
        new TypedDataField("swapper", "address"),
        new TypedDataField("nonce", "uint256"),
        new TypedDataField("deadline", "uint256"),
        new TypedDataField("additionalValidationContract", "address"),
        new TypedDataField("additionalValidationData", "bytes"),
    };

    /// <summary>Builds the ABI tuple value for an <c>OrderInfo</c> (<c>tuple(address,address,uint256,uint256,address,bytes)</c>).</summary>
    public static object?[] OrderInfoTuple(
        string reactor, string swapper, BigInteger nonce, long deadline, string additionalValidationContract, string additionalValidationData) =>
        new object?[] { reactor, swapper, nonce, deadline, additionalValidationContract, additionalValidationData };

    /// <summary>Builds the EIP-712 message dictionary for an <c>OrderInfo</c>.</summary>
    public static Dictionary<string, object?> OrderInfoDict(
        string reactor, string swapper, BigInteger nonce, long deadline, string additionalValidationContract, string additionalValidationData) => new()
        {
            ["reactor"] = reactor,
            ["swapper"] = swapper,
            ["nonce"] = nonce,
            ["deadline"] = (BigInteger)deadline,
            ["additionalValidationContract"] = additionalValidationContract,
            ["additionalValidationData"] = additionalValidationData,
        };
}
