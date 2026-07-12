using System.Numerics;
using UniswapSharp.Core.Utils;
using UniswapSharp.Permit2;
using UniswapSharp.UniswapX.Utils;
using UniswapSharp.V4.Utils;

namespace UniswapSharp.UniswapX.Order;

/// <summary>V3 dutch order cosigner data (uniswapx-sdk <c>V3CosignerData</c>).</summary>
public sealed record V3CosignerData
{
    public required long DecayStartBlock { get; set; }
    public required string ExclusiveFiller { get; set; }
    public required BigInteger ExclusivityOverrideBps { get; set; }
    public required BigInteger InputOverride { get; set; }
    public required IReadOnlyList<BigInteger> OutputOverrides { get; set; }
}

/// <summary>Unsigned order info for a V3 dutch order (uniswapx-sdk <c>UnsignedV3DutchOrderInfo</c>).</summary>
public record UnsignedV3DutchOrderInfo
{
    public required string Reactor { get; set; }
    public required string Swapper { get; set; }
    public required BigInteger Nonce { get; set; }
    public required long Deadline { get; set; }
    public required string AdditionalValidationContract { get; set; }
    public required string AdditionalValidationData { get; set; }
    public required string Cosigner { get; set; }
    public required BigInteger StartingBaseFee { get; set; }
    public required V3DutchInput Input { get; set; }
    public required List<V3DutchOutput> Outputs { get; set; }
}

/// <summary>Cosigned order info for a V3 dutch order (uniswapx-sdk <c>CosignedV3DutchOrderInfo</c>).</summary>
public sealed record CosignedV3DutchOrderInfo : UnsignedV3DutchOrderInfo
{
    public required V3CosignerData CosignerData { get; set; }
    public required string Cosignature { get; set; }
}

/// <summary>Port of uniswapx-sdk <c>order/V3DutchOrder.ts</c> (<c>UnsignedV3DutchOrder</c>).</summary>
public class UnsignedV3DutchOrder : IOffChainOrder
{
    internal const string CosignerDataTupleAbi = "tuple(uint256,address,uint256,uint256,uint256[])";

    internal const string V3DutchOrderAbi =
        "tuple(tuple(address,address,uint256,uint256,address,bytes),address,uint256," +
        "tuple(address,uint256,tuple(uint256,int256[]),uint256,uint256)," +
        "tuple(address,uint256,tuple(uint256,int256[]),address,uint256,uint256)[]," +
        CosignerDataTupleAbi + ",bytes)";

    internal static readonly IReadOnlyDictionary<string, IReadOnlyList<TypedDataField>> V3DutchOrderTypes =
        new Dictionary<string, IReadOnlyList<TypedDataField>>
        {
            ["V3DutchOrder"] = new[]
            {
                new TypedDataField("info", "OrderInfo"),
                new TypedDataField("cosigner", "address"),
                new TypedDataField("startingBaseFee", "uint256"),
                new TypedDataField("baseInput", "V3DutchInput"),
                new TypedDataField("baseOutputs", "V3DutchOutput[]"),
            },
            ["OrderInfo"] = OrderEncoding.OrderInfoFields,
            ["V3DutchInput"] = new[]
            {
                new TypedDataField("token", "address"),
                new TypedDataField("startAmount", "uint256"),
                new TypedDataField("curve", "NonlinearDutchDecay"),
                new TypedDataField("maxAmount", "uint256"),
                new TypedDataField("adjustmentPerGweiBaseFee", "uint256"),
            },
            ["V3DutchOutput"] = new[]
            {
                new TypedDataField("token", "address"),
                new TypedDataField("startAmount", "uint256"),
                new TypedDataField("curve", "NonlinearDutchDecay"),
                new TypedDataField("recipient", "address"),
                new TypedDataField("minAmount", "uint256"),
                new TypedDataField("adjustmentPerGweiBaseFee", "uint256"),
            },
            ["NonlinearDutchDecay"] = new[]
            {
                new TypedDataField("relativeBlocks", "uint256"),
                new TypedDataField("relativeAmounts", "int256[]"),
            },
        };

    public string Permit2Address { get; }

    public UnsignedV3DutchOrderInfo Info { get; }

    public int ChainId { get; }

    public UnsignedV3DutchOrder(UnsignedV3DutchOrderInfo info, int chainId, string? permit2Address = null)
    {
        Info = info;
        ChainId = chainId;
        Permit2Address = OrderUtils.GetPermit2(chainId, permit2Address);
    }

    public static UnsignedV3DutchOrder Parse(string encoded, int chainId, string? permit2 = null) =>
        new(ParseSerializedOrder(encoded), chainId, permit2);

    public BlockOverrides? BlockOverrides => null;

    public virtual string Serialize()
    {
        BigInteger encodedInputBlocks = EncodeRelativeBlocks(Info.Input.Curve.RelativeBlocks);
        return AbiParamEncoder.Encode(new[] { V3DutchOrderAbi }, new object?[]
        {
            new object?[]
            {
                OrderEncoding.OrderInfoTuple(Info.Reactor, Info.Swapper, Info.Nonce, Info.Deadline, Info.AdditionalValidationContract, Info.AdditionalValidationData),
                Info.Cosigner,
                Info.StartingBaseFee,
                new object?[]
                {
                    Info.Input.Token,
                    Info.Input.StartAmount,
                    new object?[] { encodedInputBlocks, Info.Input.Curve.RelativeAmounts.ToArray() },
                    Info.Input.MaxAmount,
                    Info.Input.AdjustmentPerGweiBaseFee,
                },
                Info.Outputs.Select(o => new object?[]
                {
                    o.Token,
                    o.StartAmount,
                    new object?[] { EncodeRelativeBlocks(o.Curve.RelativeBlocks), o.Curve.RelativeAmounts.ToArray() },
                    o.Recipient,
                    o.MinAmount,
                    o.AdjustmentPerGweiBaseFee,
                }).ToArray(),
                new object?[] { 0, AddressZero, 0, 0, new object?[] { 0 } },
                "0x",
            },
        });
    }

    public PermitData PermitData() =>
        SignatureTransfer.GetPermitData(ToPermit(), Permit2Address, ChainId, Witness());

    public string GetSigner(string signature)
    {
        var (domain, types, values) = SignatureTransfer.GetPermitData(ToPermit(), Permit2Address, ChainId, Witness());
        byte[] digest = Eip712TypedDataEncoder.Hash(domain, types, values);
        return OrderSigning.RecoverSigner(digest, signature);
    }

    public string Hash() => Eip712TypedDataEncoder.HashStructHex(V3DutchOrderTypes, WitnessInfo());

    /// <summary>Full order hash that should be signed over by the cosigner (uniswapx-sdk <c>cosignatureHash</c>).</summary>
    public string CosignatureHash(V3CosignerData cosignerData)
    {
        byte[] hashBytes = Convert.FromHexString(Hash()[2..]);
        byte[] chainIdWord = UnsignedPriorityOrder.Word(ChainId);
        string encoded = AbiParamEncoder.Encode(new[] { CosignerDataTupleAbi }, new object?[]
        {
            new object?[]
            {
                cosignerData.DecayStartBlock,
                cosignerData.ExclusiveFiller,
                cosignerData.ExclusivityOverrideBps,
                cosignerData.InputOverride,
                cosignerData.OutputOverrides.ToArray(),
            },
        });
        byte[] encodedBytes = Convert.FromHexString(encoded[2..]);
        var packed = new byte[hashBytes.Length + chainIdWord.Length + encodedBytes.Length];
        int off = 0;
        Array.Copy(hashBytes, 0, packed, off, hashBytes.Length); off += hashBytes.Length;
        Array.Copy(chainIdWord, 0, packed, off, chainIdWord.Length); off += chainIdWord.Length;
        Array.Copy(encodedBytes, 0, packed, off, encodedBytes.Length);
        return "0x" + Convert.ToHexStringLower(Nethereum.Util.Sha3Keccack.Current.CalculateHash(packed));
    }

    internal const string AddressZero = "0x0000000000000000000000000000000000000000";

    private PermitTransferFrom ToPermit() => new(
        new TokenPermissions(Info.Input.Token, Info.Input.MaxAmount),
        Info.Reactor,
        Info.Nonce,
        Info.Deadline);

    private Dictionary<string, object?> CurveDict(NonlinearDutchDecay curve) => new()
    {
        ["relativeBlocks"] = EncodeRelativeBlocks(curve.RelativeBlocks),
        ["relativeAmounts"] = curve.RelativeAmounts.Cast<object?>().ToArray(),
    };

    private Dictionary<string, object?> WitnessInfo() => new()
    {
        ["info"] = OrderEncoding.OrderInfoDict(Info.Reactor, Info.Swapper, Info.Nonce, Info.Deadline, Info.AdditionalValidationContract, Info.AdditionalValidationData),
        ["cosigner"] = Info.Cosigner,
        ["startingBaseFee"] = Info.StartingBaseFee,
        ["baseInput"] = new Dictionary<string, object?>
        {
            ["token"] = Info.Input.Token,
            ["startAmount"] = Info.Input.StartAmount,
            ["curve"] = CurveDict(Info.Input.Curve),
            ["maxAmount"] = Info.Input.MaxAmount,
            ["adjustmentPerGweiBaseFee"] = Info.Input.AdjustmentPerGweiBaseFee,
        },
        ["baseOutputs"] = Info.Outputs.Select(o => new Dictionary<string, object?>
        {
            ["token"] = o.Token,
            ["startAmount"] = o.StartAmount,
            ["curve"] = CurveDict(o.Curve),
            ["recipient"] = o.Recipient,
            ["minAmount"] = o.MinAmount,
            ["adjustmentPerGweiBaseFee"] = o.AdjustmentPerGweiBaseFee,
        }).ToArray(),
    };

    private Witness Witness() => new(WitnessInfo(), "V3DutchOrder", V3DutchOrderTypes);

    /// <summary>JSON form (uniswapx-sdk <c>UnsignedV3DutchOrderInfoJSON</c>).</summary>
    public sealed record NonlinearDutchDecayJSON(IReadOnlyList<int> RelativeBlocks, IReadOnlyList<string> RelativeAmounts);
    public sealed record V3DutchInputJSON(string Token, string StartAmount, NonlinearDutchDecayJSON Curve, string MaxAmount, string AdjustmentPerGweiBaseFee);
    public sealed record V3DutchOutputJSON(string Token, string StartAmount, NonlinearDutchDecayJSON Curve, string Recipient, string MinAmount, string AdjustmentPerGweiBaseFee);
    public sealed record UnsignedV3DutchOrderInfoJSON(
        string Reactor, string Swapper, string Nonce, long Deadline,
        string AdditionalValidationContract, string AdditionalValidationData,
        string Cosigner, string StartingBaseFee, V3DutchInputJSON Input, IReadOnlyList<V3DutchOutputJSON> Outputs);

    public static UnsignedV3DutchOrder FromJSON(UnsignedV3DutchOrderInfoJSON json, int chainId, string? permit2Address = null) =>
        new(
            new UnsignedV3DutchOrderInfo
            {
                Reactor = json.Reactor,
                Swapper = json.Swapper,
                Nonce = BigInteger.Parse(json.Nonce),
                Deadline = json.Deadline,
                AdditionalValidationContract = json.AdditionalValidationContract,
                AdditionalValidationData = json.AdditionalValidationData,
                Cosigner = json.Cosigner,
                StartingBaseFee = BigInteger.Parse(json.StartingBaseFee),
                Input = new V3DutchInput
                {
                    Token = json.Input.Token,
                    StartAmount = BigInteger.Parse(json.Input.StartAmount),
                    Curve = new NonlinearDutchDecay
                    {
                        RelativeBlocks = json.Input.Curve.RelativeBlocks,
                        RelativeAmounts = json.Input.Curve.RelativeAmounts.Select(BigInteger.Parse).ToList(),
                    },
                    MaxAmount = BigInteger.Parse(json.Input.MaxAmount),
                    AdjustmentPerGweiBaseFee = BigInteger.Parse(json.Input.AdjustmentPerGweiBaseFee),
                },
                Outputs = json.Outputs.Select(o => new V3DutchOutput
                {
                    Token = o.Token,
                    StartAmount = BigInteger.Parse(o.StartAmount),
                    Curve = new NonlinearDutchDecay
                    {
                        RelativeBlocks = o.Curve.RelativeBlocks,
                        RelativeAmounts = o.Curve.RelativeAmounts.Select(BigInteger.Parse).ToList(),
                    },
                    Recipient = o.Recipient,
                    MinAmount = BigInteger.Parse(o.MinAmount),
                    AdjustmentPerGweiBaseFee = BigInteger.Parse(o.AdjustmentPerGweiBaseFee),
                }).ToList(),
            },
            chainId,
            permit2Address);

    /// <summary>Packs relative blocks into a single uint256 (16 bits each) (uniswapx-sdk <c>encodeRelativeBlocks</c>).</summary>
    public static BigInteger EncodeRelativeBlocks(IReadOnlyList<int> relativeBlocks)
    {
        BigInteger packedData = BigInteger.Zero;
        for (int i = 0; i < relativeBlocks.Count; i++)
        {
            packedData |= (BigInteger)relativeBlocks[i] << (i * 16);
        }
        return packedData;
    }

    internal static List<int> DecodeRelativeBlocks(BigInteger packedData, int relativeAmountsLength)
    {
        var relativeBlocks = new List<int>();
        for (int i = 0; i < relativeAmountsLength; i++)
        {
            int block = (int)((packedData >> (i * 16)) & 0xffff);
            relativeBlocks.Add(block);
        }
        return relativeBlocks;
    }

    internal static CosignedV3DutchOrderInfo ParseSerializedOrder(string serialized)
    {
        var decoded = AbiParamDecoder.Decode(new[] { V3DutchOrderAbi }, serialized);
        var tuple = (List<object?>)decoded[0]!;
        var info = (List<object?>)tuple[0]!;
        var input = (List<object?>)tuple[3]!;
        var inputCurve = (List<object?>)input[2]!;
        var outputs = (List<object?>)tuple[4]!;
        var cosignerData = (List<object?>)tuple[5]!;

        var inputRelativeAmounts = ((List<object?>)inputCurve[1]!).Select(x => (BigInteger)x!).ToList();

        return new CosignedV3DutchOrderInfo
        {
            Reactor = AddressValidator.GetAddress((string)info[0]!),
            Swapper = AddressValidator.GetAddress((string)info[1]!),
            Nonce = (BigInteger)info[2]!,
            Deadline = (long)(BigInteger)info[3]!,
            AdditionalValidationContract = AddressValidator.GetAddress((string)info[4]!),
            AdditionalValidationData = (string)info[5]!,
            Cosigner = AddressValidator.GetAddress((string)tuple[1]!),
            StartingBaseFee = (BigInteger)tuple[2]!,
            Input = new V3DutchInput
            {
                Token = AddressValidator.GetAddress((string)input[0]!),
                StartAmount = (BigInteger)input[1]!,
                Curve = new NonlinearDutchDecay
                {
                    RelativeBlocks = DecodeRelativeBlocks((BigInteger)inputCurve[0]!, inputRelativeAmounts.Count),
                    RelativeAmounts = inputRelativeAmounts,
                },
                MaxAmount = (BigInteger)input[3]!,
                AdjustmentPerGweiBaseFee = (BigInteger)input[4]!,
            },
            Outputs = outputs.Select(o =>
            {
                var output = (List<object?>)o!;
                var outputCurve = (List<object?>)output[2]!;
                var outputRelativeAmounts = ((List<object?>)outputCurve[1]!).Select(x => (BigInteger)x!).ToList();
                return new V3DutchOutput
                {
                    Token = AddressValidator.GetAddress((string)output[0]!),
                    StartAmount = (BigInteger)output[1]!,
                    Curve = new NonlinearDutchDecay
                    {
                        RelativeBlocks = DecodeRelativeBlocks((BigInteger)outputCurve[0]!, outputRelativeAmounts.Count),
                        RelativeAmounts = outputRelativeAmounts,
                    },
                    Recipient = AddressValidator.GetAddress((string)output[3]!),
                    MinAmount = (BigInteger)output[4]!,
                    AdjustmentPerGweiBaseFee = (BigInteger)output[5]!,
                };
            }).ToList(),
            CosignerData = new V3CosignerData
            {
                DecayStartBlock = (long)(BigInteger)cosignerData[0]!,
                ExclusiveFiller = AddressValidator.GetAddress((string)cosignerData[1]!),
                ExclusivityOverrideBps = (BigInteger)cosignerData[2]!,
                InputOverride = (BigInteger)cosignerData[3]!,
                OutputOverrides = ((List<object?>)cosignerData[4]!).Select(x => (BigInteger)x!).ToList(),
            },
            Cosignature = (string)tuple[6]!,
        };
    }
}

/// <summary>Port of uniswapx-sdk <c>order/V3DutchOrder.ts</c> (<c>CosignedV3DutchOrder</c>).</summary>
public sealed class CosignedV3DutchOrder : UnsignedV3DutchOrder
{
    public new CosignedV3DutchOrderInfo Info { get; }

    public CosignedV3DutchOrder(CosignedV3DutchOrderInfo info, int chainId, string? permit2Address = null)
        : base(info, chainId, permit2Address)
    {
        Info = info;
    }

    public static CosignedV3DutchOrder FromUnsignedOrder(
        UnsignedV3DutchOrder order, V3CosignerData cosignerData, string cosignature)
    {
        return new CosignedV3DutchOrder(
            new CosignedV3DutchOrderInfo
            {
                Reactor = order.Info.Reactor,
                Swapper = order.Info.Swapper,
                Nonce = order.Info.Nonce,
                Deadline = order.Info.Deadline,
                AdditionalValidationContract = order.Info.AdditionalValidationContract,
                AdditionalValidationData = order.Info.AdditionalValidationData,
                Cosigner = order.Info.Cosigner,
                StartingBaseFee = order.Info.StartingBaseFee,
                Input = order.Info.Input,
                Outputs = order.Info.Outputs,
                CosignerData = cosignerData,
                Cosignature = cosignature,
            },
            order.ChainId,
            order.Permit2Address);
    }

    public static new CosignedV3DutchOrder Parse(string encoded, int chainId, string? permit2 = null) =>
        new(ParseSerializedOrder(encoded), chainId, permit2);

    public override string Serialize()
    {
        BigInteger encodedInputBlocks = EncodeRelativeBlocks(Info.Input.Curve.RelativeBlocks);
        return AbiParamEncoder.Encode(new[] { V3DutchOrderAbi }, new object?[]
        {
            new object?[]
            {
                OrderEncoding.OrderInfoTuple(Info.Reactor, Info.Swapper, Info.Nonce, Info.Deadline, Info.AdditionalValidationContract, Info.AdditionalValidationData),
                Info.Cosigner,
                Info.StartingBaseFee,
                new object?[]
                {
                    Info.Input.Token,
                    Info.Input.StartAmount,
                    new object?[] { encodedInputBlocks, Info.Input.Curve.RelativeAmounts.ToArray() },
                    Info.Input.MaxAmount,
                    Info.Input.AdjustmentPerGweiBaseFee,
                },
                Info.Outputs.Select(o => new object?[]
                {
                    o.Token,
                    o.StartAmount,
                    new object?[] { EncodeRelativeBlocks(o.Curve.RelativeBlocks), o.Curve.RelativeAmounts.ToArray() },
                    o.Recipient,
                    o.MinAmount,
                    o.AdjustmentPerGweiBaseFee,
                }).ToArray(),
                new object?[]
                {
                    Info.CosignerData.DecayStartBlock,
                    Info.CosignerData.ExclusiveFiller,
                    Info.CosignerData.ExclusivityOverrideBps,
                    Info.CosignerData.InputOverride,
                    Info.CosignerData.OutputOverrides.ToArray(),
                },
                Info.Cosignature,
            },
        });
    }

    /// <summary>Recovers the cosigner via raw ECDSA recovery over the full order hash (uniswapx-sdk <c>recoverCosigner</c>).</summary>
    public string RecoverCosigner()
    {
        string messageHash = CosignatureHash(Info.CosignerData);
        byte[] hashBytes = Convert.FromHexString(messageHash[2..]);
        return OrderSigning.RecoverSigner(hashBytes, Info.Cosignature);
    }

    public ResolvedUniswapXOrder Resolve(V3OrderResolutionOptions options)
    {
        var input = new TokenAmount(
            Info.Input.Token,
            DutchBlockDecay.GetBlockDecayedAmount(
                new DutchBlockDecayConfig(
                    Info.CosignerData.DecayStartBlock,
                    OrderUtils.OriginalIfZero(Info.CosignerData.InputOverride, Info.Input.StartAmount),
                    Info.Input.Curve.RelativeBlocks,
                    Info.Input.Curve.RelativeAmounts),
                options.CurrentBlock));

        var outputs = Info.Outputs.Select((output, idx) => new TokenAmount(
            output.Token,
            DutchBlockDecay.GetBlockDecayedAmount(
                new DutchBlockDecayConfig(
                    Info.CosignerData.DecayStartBlock,
                    OrderUtils.OriginalIfZero(Info.CosignerData.OutputOverrides[idx], output.StartAmount),
                    output.Curve.RelativeBlocks,
                    output.Curve.RelativeAmounts),
                options.CurrentBlock))).ToList();

        return new ResolvedUniswapXOrder(input, outputs);
    }
}
