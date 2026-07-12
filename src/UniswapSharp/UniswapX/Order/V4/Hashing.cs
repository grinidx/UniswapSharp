using System.Numerics;
using System.Text;
using Nethereum.Util;
using UniswapSharp.Permit2;
using UniswapSharp.V4.Utils;

namespace UniswapSharp.UniswapX.Order.V4;

/// <summary>Port of uniswapx-sdk <c>order/v4/hashing.ts</c>: struct hashing for V4 hybrid orders.</summary>
public static class Hashing
{
    /// <summary>EIP-712 type string for OrderInfoV4.</summary>
    public const string OrderInfoV4TypeString =
        "OrderInfo(" +
        "address reactor," +
        "address swapper," +
        "uint256 nonce," +
        "uint256 deadline," +
        "address preExecutionHook," +
        "bytes preExecutionHookData," +
        "address postExecutionHook," +
        "bytes postExecutionHookData," +
        "address auctionResolver)";

    /// <summary>EIP-712 type hash for OrderInfoV4.</summary>
    public static readonly string OrderInfoV4TypeHash = Keccak256Utf8(OrderInfoV4TypeString);

    private const string HybridInputTypeString = "HybridInput(address token,uint256 maxAmount)";
    private static readonly string HybridInputTypeHash = Keccak256Utf8(HybridInputTypeString);

    private const string HybridOutputTypeString = "HybridOutput(address token,uint256 minAmount,address recipient)";
    private static readonly string HybridOutputTypeHash = Keccak256Utf8(HybridOutputTypeString);

    private const string HybridOrderTypeString =
        "HybridOrder(" +
        "OrderInfo info," +
        "address cosigner," +
        "HybridInput input," +
        "HybridOutput[] outputs," +
        "uint256 auctionStartBlock," +
        "uint256 baselinePriorityFee," +
        "uint256 scalingFactor," +
        "uint256[] priceCurve)";

    private static readonly string HybridOrderTypeHash =
        Keccak256Utf8(HybridOrderTypeString + HybridInputTypeString + HybridOutputTypeString + OrderInfoV4TypeString);

    private const string DcaCosignerDataType =
        "DCAOrderCosignerData(address swapper,uint96 nonce,uint160 execAmount,uint96 orderNonce,uint160 limitAmount)";

    /// <summary>EIP-712 type hash for DCAOrderCosignerData (re-exported by constants/v4).</summary>
    public static readonly string DcaCosignerDataTypeHash = Keccak256Utf8(DcaCosignerDataType);

    /// <summary>EIP-712 witness types for the HybridOrder Permit2 signature.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<TypedDataField>> HybridOrderTypes =
        new Dictionary<string, IReadOnlyList<TypedDataField>>
        {
            ["HybridInput"] = new[]
            {
                new TypedDataField("token", "address"),
                new TypedDataField("maxAmount", "uint256"),
            },
            ["HybridOrder"] = new[]
            {
                new TypedDataField("info", "OrderInfo"),
                new TypedDataField("cosigner", "address"),
                new TypedDataField("input", "HybridInput"),
                new TypedDataField("outputs", "HybridOutput[]"),
                new TypedDataField("auctionStartBlock", "uint256"),
                new TypedDataField("baselinePriorityFee", "uint256"),
                new TypedDataField("scalingFactor", "uint256"),
                new TypedDataField("priceCurve", "uint256[]"),
            },
            ["HybridOutput"] = new[]
            {
                new TypedDataField("token", "address"),
                new TypedDataField("minAmount", "uint256"),
                new TypedDataField("recipient", "address"),
            },
            ["OrderInfo"] = new[]
            {
                new TypedDataField("reactor", "address"),
                new TypedDataField("swapper", "address"),
                new TypedDataField("nonce", "uint256"),
                new TypedDataField("deadline", "uint256"),
                new TypedDataField("preExecutionHook", "address"),
                new TypedDataField("preExecutionHookData", "bytes"),
                new TypedDataField("postExecutionHook", "address"),
                new TypedDataField("postExecutionHookData", "bytes"),
                new TypedDataField("auctionResolver", "address"),
            },
        };

    /// <summary>Hashes an OrderInfoV4 (uniswapx-sdk <c>hashOrderInfoV4</c>).</summary>
    public static string HashOrderInfoV4(UnsignedHybridOrderInfo info)
    {
        string encoded = AbiParamEncoder.Encode(
            new[] { "bytes32", "address", "address", "uint256", "uint256", "address", "bytes32", "address", "bytes32", "address" },
            new object?[]
            {
                OrderInfoV4TypeHash,
                info.Reactor,
                info.Swapper,
                info.Nonce,
                info.Deadline,
                info.PreExecutionHook,
                Keccak256Bytes(info.PreExecutionHookData),
                info.PostExecutionHook,
                Keccak256Bytes(info.PostExecutionHookData),
                info.AuctionResolver,
            });
        return Keccak256Hex(encoded);
    }

    private static string HashHybridInput(HybridInput input)
    {
        string encoded = AbiParamEncoder.Encode(
            new[] { "bytes32", "address", "uint256" },
            new object?[] { HybridInputTypeHash, input.Token, input.MaxAmount });
        return Keccak256Hex(encoded);
    }

    private static string HashHybridOutput(HybridOutput output)
    {
        string encoded = AbiParamEncoder.Encode(
            new[] { "bytes32", "address", "uint256", "address" },
            new object?[] { HybridOutputTypeHash, output.Token, output.MinAmount, output.Recipient });
        return Keccak256Hex(encoded);
    }

    private static string HashHybridOutputs(IReadOnlyList<HybridOutput> outputs)
    {
        if (outputs.Count == 0)
        {
            return Keccak256Hex("0x");
        }
        byte[] packed = Concat(outputs.Select(o => FromHex(HashHybridOutput(o))).ToArray());
        return Keccak256Hex(packed);
    }

    private static string HashPriceCurve(IReadOnlyList<BigInteger> curve)
    {
        if (curve.Count == 0)
        {
            return Keccak256Hex("0x");
        }
        byte[] packed = Concat(curve.Select(Word).ToArray());
        return Keccak256Hex(packed);
    }

    /// <summary>Hashes a hybrid order (uniswapx-sdk <c>hashHybridOrder</c>). Cosigner data is not part of the order hash.</summary>
    public static string HashHybridOrder(UnsignedHybridOrderInfo order)
    {
        string infoHash = HashOrderInfoV4(order);
        string inputHash = HashHybridInput(order.Input);
        string outputsHash = HashHybridOutputs(order.Outputs);
        string priceCurveHash = HashPriceCurve(order.PriceCurve);

        string encoded = AbiParamEncoder.Encode(
            new[] { "bytes32", "bytes32", "address", "bytes32", "bytes32", "uint256", "uint256", "uint256", "bytes32" },
            new object?[]
            {
                HybridOrderTypeHash,
                infoHash,
                order.Cosigner,
                inputHash,
                outputsHash,
                order.AuctionStartBlock,
                order.BaselinePriorityFee,
                order.ScalingFactor,
                priceCurveHash,
            });
        return Keccak256Hex(encoded);
    }

    /// <summary>Computes the cosigner digest (<c>orderHash || chainId || cosignerData</c>) (uniswapx-sdk <c>hashHybridCosignerData</c>).</summary>
    public static string HashHybridCosignerData(string orderHash, HybridCosignerData cosignerData, int chainId)
    {
        string encodedCosignerData = AbiParamEncoder.Encode(
            new[] { "tuple(uint256,uint256[],address,uint256,uint256)" },
            new object?[]
            {
                new object?[]
                {
                    cosignerData.AuctionTargetBlock,
                    cosignerData.SupplementalPriceCurve.ToArray(),
                    cosignerData.ExclusiveFiller,
                    cosignerData.ExclusivityOverrideBps,
                    cosignerData.ExclusivityEndBlock,
                },
            });

        byte[] packed = Concat(FromHex(orderHash), Word(chainId), FromHex(encodedCosignerData));
        return Keccak256Hex(packed);
    }

    // ---- keccak / byte helpers ----

    internal static string Keccak256Utf8(string text) =>
        "0x" + Convert.ToHexStringLower(Sha3Keccack.Current.CalculateHash(Encoding.UTF8.GetBytes(text)));

    private static string Keccak256Bytes(string hex) => Keccak256Hex(hex);

    private static string Keccak256Hex(string hex) =>
        "0x" + Convert.ToHexStringLower(Sha3Keccack.Current.CalculateHash(FromHex(hex)));

    private static string Keccak256Hex(byte[] data) =>
        "0x" + Convert.ToHexStringLower(Sha3Keccack.Current.CalculateHash(data));

    private static byte[] FromHex(string hex)
    {
        string h = hex.StartsWith("0x") ? hex[2..] : hex;
        return h.Length == 0 ? Array.Empty<byte>() : Convert.FromHexString(h);
    }

    private static byte[] Word(BigInteger value)
    {
        byte[] raw = value.ToByteArray(isUnsigned: true, isBigEndian: true);
        var word = new byte[32];
        Array.Copy(raw, 0, word, 32 - raw.Length, raw.Length);
        return word;
    }

    private static byte[] Concat(params byte[][] arrays)
    {
        var result = new byte[arrays.Sum(a => a.Length)];
        int off = 0;
        foreach (var a in arrays)
        {
            Array.Copy(a, 0, result, off, a.Length);
            off += a.Length;
        }
        return result;
    }
}
