using System.Numerics;
using Nethereum.Signer;
using Nethereum.Util;
using UniswapSharp.Core.Utils;
using UniswapSharp.Permit2;
using UniswapSharp.UniswapX.Utils;
using UniswapSharp.V4.Utils;

namespace UniswapSharp.UniswapX.Order;

/// <summary>Unsigned order info for a V2 dutch order (uniswapx-sdk <c>UnsignedV2DutchOrderInfo</c>).</summary>
public record UnsignedV2DutchOrderInfo
{
    public required string Reactor { get; set; }
    public required string Swapper { get; set; }
    public required BigInteger Nonce { get; set; }
    public required long Deadline { get; set; }
    public required string AdditionalValidationContract { get; set; }
    public required string AdditionalValidationData { get; set; }
    public required string Cosigner { get; set; }
    public required DutchInput Input { get; set; }
    public required List<DutchOutput> Outputs { get; set; }
}

/// <summary>Cosigned order info for a V2 dutch order (uniswapx-sdk <c>CosignedV2DutchOrderInfo</c>).</summary>
public sealed record CosignedV2DutchOrderInfo : UnsignedV2DutchOrderInfo
{
    public required CosignerData CosignerData { get; set; }
    public required string Cosignature { get; set; }
}

/// <summary>Port of uniswapx-sdk <c>order/V2DutchOrder.ts</c> (<c>UnsignedV2DutchOrder</c>).</summary>
public class UnsignedV2DutchOrder : IOffChainOrder
{
    internal const string CosignerDataTupleAbi = "tuple(uint256,uint256,address,uint256,uint256,uint256[])";

    internal const string V2DutchOrderAbi =
        "tuple(tuple(address,address,uint256,uint256,address,bytes),address,tuple(address,uint256,uint256),tuple(address,uint256,uint256,address)[]," +
        CosignerDataTupleAbi + ",bytes)";

    internal static readonly IReadOnlyDictionary<string, IReadOnlyList<TypedDataField>> V2DutchOrderTypes =
        new Dictionary<string, IReadOnlyList<TypedDataField>>
        {
            ["V2DutchOrder"] = new[]
            {
                new TypedDataField("info", "OrderInfo"),
                new TypedDataField("cosigner", "address"),
                new TypedDataField("baseInputToken", "address"),
                new TypedDataField("baseInputStartAmount", "uint256"),
                new TypedDataField("baseInputEndAmount", "uint256"),
                new TypedDataField("baseOutputs", "DutchOutput[]"),
            },
            ["OrderInfo"] = OrderEncoding.OrderInfoFields,
            ["DutchOutput"] = new[]
            {
                new TypedDataField("token", "address"),
                new TypedDataField("startAmount", "uint256"),
                new TypedDataField("endAmount", "uint256"),
                new TypedDataField("recipient", "address"),
            },
        };

    public string Permit2Address { get; }

    public UnsignedV2DutchOrderInfo Info { get; }

    public int ChainId { get; }

    public UnsignedV2DutchOrder(UnsignedV2DutchOrderInfo info, int chainId, string? permit2Address = null)
    {
        Info = info;
        ChainId = chainId;
        Permit2Address = OrderUtils.GetPermit2(chainId, permit2Address);
    }

    public static UnsignedV2DutchOrder Parse(string encoded, int chainId, string? permit2 = null) =>
        new(ParseSerializedOrder(encoded), chainId, permit2);

    /// <summary>JSON form (uniswapx-sdk <c>UnsignedV2DutchOrderInfoJSON</c>).</summary>
    public sealed record UnsignedV2DutchOrderInfoJSON(
        string Reactor,
        string Swapper,
        string Nonce,
        long Deadline,
        string AdditionalValidationContract,
        string AdditionalValidationData,
        string Cosigner,
        DutchInputJSON Input,
        IReadOnlyList<DutchOutputJSON> Outputs);

    public static UnsignedV2DutchOrder FromJSON(UnsignedV2DutchOrderInfoJSON json, int chainId, string? permit2Address = null) =>
        new(
            new UnsignedV2DutchOrderInfo
            {
                Reactor = json.Reactor,
                Swapper = json.Swapper,
                Nonce = BigInteger.Parse(json.Nonce),
                Deadline = json.Deadline,
                AdditionalValidationContract = json.AdditionalValidationContract,
                AdditionalValidationData = json.AdditionalValidationData,
                Cosigner = json.Cosigner,
                Input = new DutchInput
                {
                    Token = json.Input.Token,
                    StartAmount = BigInteger.Parse(json.Input.StartAmount),
                    EndAmount = BigInteger.Parse(json.Input.EndAmount),
                },
                Outputs = json.Outputs.Select(o => new DutchOutput
                {
                    Token = o.Token,
                    StartAmount = BigInteger.Parse(o.StartAmount),
                    EndAmount = BigInteger.Parse(o.EndAmount),
                    Recipient = o.Recipient,
                }).ToList(),
            },
            chainId,
            permit2Address);

    public BlockOverrides? BlockOverrides => null;

    public virtual string Serialize()
    {
        return AbiParamEncoder.Encode(new[] { V2DutchOrderAbi }, new object?[]
        {
            new object?[]
            {
                OrderEncoding.OrderInfoTuple(Info.Reactor, Info.Swapper, Info.Nonce, Info.Deadline, Info.AdditionalValidationContract, Info.AdditionalValidationData),
                Info.Cosigner,
                new object?[] { Info.Input.Token, Info.Input.StartAmount, Info.Input.EndAmount },
                Info.Outputs.Select(o => new object?[] { o.Token, o.StartAmount, o.EndAmount, o.Recipient }).ToArray(),
                new object?[] { 0, 0, AddressZero, 0, 0, new object?[] { 0 } },
                "0x",
            },
        });
    }

    public string GetSigner(string signature)
    {
        var (domain, types, values) = SignatureTransfer.GetPermitData(ToPermit(), Permit2Address, ChainId, Witness());
        byte[] digest = Eip712TypedDataEncoder.Hash(domain, types, values);
        return OrderSigning.RecoverSigner(digest, signature);
    }

    public PermitData PermitData() =>
        SignatureTransfer.GetPermitData(ToPermit(), Permit2Address, ChainId, Witness());

    public string Hash() => Eip712TypedDataEncoder.HashStructHex(V2DutchOrderTypes, WitnessInfo());

    public virtual ResolvedUniswapXOrder Resolve(OrderResolutionOptions options) =>
        throw new InvalidOperationException("Method not implemented");

    public CustomOrderValidation Validation => UniswapX.Order.Validation.ParseValidation(new OrderInfo
    {
        Reactor = Info.Reactor,
        Swapper = Info.Swapper,
        Nonce = Info.Nonce,
        Deadline = Info.Deadline,
        AdditionalValidationContract = Info.AdditionalValidationContract,
        AdditionalValidationData = Info.AdditionalValidationData,
    });

    /// <summary>Full order hash that should be signed over by the cosigner (uniswapx-sdk <c>cosignatureHash</c>).</summary>
    public string CosignatureHash(CosignerData cosignerData)
    {
        byte[] hashBytes = Convert.FromHexString(Hash()[2..]);
        string encoded = AbiParamEncoder.Encode(new[] { CosignerDataTupleAbi }, new object?[]
        {
            new object?[]
            {
                cosignerData.DecayStartTime,
                cosignerData.DecayEndTime,
                cosignerData.ExclusiveFiller,
                cosignerData.ExclusivityOverrideBps,
                cosignerData.InputOverride,
                cosignerData.OutputOverrides.ToArray(),
            },
        });
        byte[] encodedBytes = Convert.FromHexString(encoded[2..]);
        byte[] packed = new byte[hashBytes.Length + encodedBytes.Length];
        Array.Copy(hashBytes, 0, packed, 0, hashBytes.Length);
        Array.Copy(encodedBytes, 0, packed, hashBytes.Length, encodedBytes.Length);
        return "0x" + Convert.ToHexStringLower(Sha3Keccack.Current.CalculateHash(packed));
    }

    internal const string AddressZero = "0x0000000000000000000000000000000000000000";

    private PermitTransferFrom ToPermit() => new(
        new TokenPermissions(Info.Input.Token, Info.Input.EndAmount),
        Info.Reactor,
        Info.Nonce,
        Info.Deadline);

    private Dictionary<string, object?> WitnessInfo() => new()
    {
        ["info"] = OrderEncoding.OrderInfoDict(Info.Reactor, Info.Swapper, Info.Nonce, Info.Deadline, Info.AdditionalValidationContract, Info.AdditionalValidationData),
        ["cosigner"] = Info.Cosigner,
        ["baseInputToken"] = Info.Input.Token,
        ["baseInputStartAmount"] = Info.Input.StartAmount,
        ["baseInputEndAmount"] = Info.Input.EndAmount,
        ["baseOutputs"] = Info.Outputs.Select(o => new Dictionary<string, object?>
        {
            ["token"] = o.Token,
            ["startAmount"] = o.StartAmount,
            ["endAmount"] = o.EndAmount,
            ["recipient"] = o.Recipient,
        }).ToArray(),
    };

    private Witness Witness() => new(WitnessInfo(), "V2DutchOrder", V2DutchOrderTypes);

    internal static CosignedV2DutchOrderInfo ParseSerializedOrder(string serialized)
    {
        var decoded = AbiParamDecoder.Decode(new[] { V2DutchOrderAbi }, serialized);
        var tuple = (List<object?>)decoded[0]!;
        var info = (List<object?>)tuple[0]!;
        var input = (List<object?>)tuple[2]!;
        var outputs = (List<object?>)tuple[3]!;
        var cosignerData = (List<object?>)tuple[4]!;

        return new CosignedV2DutchOrderInfo
        {
            Reactor = AddressValidator.GetAddress((string)info[0]!),
            Swapper = AddressValidator.GetAddress((string)info[1]!),
            Nonce = (BigInteger)info[2]!,
            Deadline = (long)(BigInteger)info[3]!,
            AdditionalValidationContract = AddressValidator.GetAddress((string)info[4]!),
            AdditionalValidationData = (string)info[5]!,
            Cosigner = AddressValidator.GetAddress((string)tuple[1]!),
            Input = new DutchInput
            {
                Token = AddressValidator.GetAddress((string)input[0]!),
                StartAmount = (BigInteger)input[1]!,
                EndAmount = (BigInteger)input[2]!,
            },
            Outputs = outputs.Select(o =>
            {
                var output = (List<object?>)o!;
                return new DutchOutput
                {
                    Token = AddressValidator.GetAddress((string)output[0]!),
                    StartAmount = (BigInteger)output[1]!,
                    EndAmount = (BigInteger)output[2]!,
                    Recipient = AddressValidator.GetAddress((string)output[3]!),
                };
            }).ToList(),
            CosignerData = new CosignerData
            {
                DecayStartTime = (long)(BigInteger)cosignerData[0]!,
                DecayEndTime = (long)(BigInteger)cosignerData[1]!,
                ExclusiveFiller = AddressValidator.GetAddress((string)cosignerData[2]!),
                ExclusivityOverrideBps = (BigInteger)cosignerData[3]!,
                InputOverride = (BigInteger)cosignerData[4]!,
                OutputOverrides = ((List<object?>)cosignerData[5]!).Select(x => (BigInteger)x!).ToList(),
            },
            Cosignature = (string)tuple[5]!,
        };
    }
}

/// <summary>Port of uniswapx-sdk <c>order/V2DutchOrder.ts</c> (<c>CosignedV2DutchOrder</c>).</summary>
public sealed class CosignedV2DutchOrder : UnsignedV2DutchOrder
{
    public new CosignedV2DutchOrderInfo Info { get; }

    public CosignedV2DutchOrder(CosignedV2DutchOrderInfo info, int chainId, string? permit2Address = null)
        : base(info, chainId, permit2Address)
    {
        Info = info;
    }

    public static CosignedV2DutchOrder FromUnsignedOrder(
        UnsignedV2DutchOrder order, CosignerData cosignerData, string cosignature)
    {
        return new CosignedV2DutchOrder(
            new CosignedV2DutchOrderInfo
            {
                Reactor = order.Info.Reactor,
                Swapper = order.Info.Swapper,
                Nonce = order.Info.Nonce,
                Deadline = order.Info.Deadline,
                AdditionalValidationContract = order.Info.AdditionalValidationContract,
                AdditionalValidationData = order.Info.AdditionalValidationData,
                Cosigner = order.Info.Cosigner,
                Input = order.Info.Input,
                Outputs = order.Info.Outputs,
                CosignerData = cosignerData,
                Cosignature = cosignature,
            },
            order.ChainId,
            order.Permit2Address);
    }

    public static new CosignedV2DutchOrder Parse(string encoded, int chainId, string? permit2 = null) =>
        new(ParseSerializedOrder(encoded), chainId, permit2);

    public override ResolvedUniswapXOrder Resolve(OrderResolutionOptions options)
    {
        var input = new TokenAmount(
            Info.Input.Token,
            DutchDecay.GetDecayedAmount(
                new DutchDecayConfig(
                    OrderUtils.OriginalIfZero(Info.CosignerData.InputOverride, Info.Input.StartAmount),
                    Info.Input.EndAmount,
                    Info.CosignerData.DecayStartTime,
                    Info.CosignerData.DecayEndTime),
                options.Timestamp));

        var outputs = Info.Outputs.Select((output, idx) => new TokenAmount(
            output.Token,
            DutchDecay.GetDecayedAmount(
                new DutchDecayConfig(
                    OrderUtils.OriginalIfZero(Info.CosignerData.OutputOverrides[idx], output.StartAmount),
                    output.EndAmount,
                    Info.CosignerData.DecayStartTime,
                    Info.CosignerData.DecayEndTime),
                options.Timestamp))).ToList();

        return new ResolvedUniswapXOrder(input, outputs);
    }

    public override string Serialize()
    {
        return AbiParamEncoder.Encode(new[] { V2DutchOrderAbi }, new object?[]
        {
            new object?[]
            {
                OrderEncoding.OrderInfoTuple(Info.Reactor, Info.Swapper, Info.Nonce, Info.Deadline, Info.AdditionalValidationContract, Info.AdditionalValidationData),
                Info.Cosigner,
                new object?[] { Info.Input.Token, Info.Input.StartAmount, Info.Input.EndAmount },
                Info.Outputs.Select(o => new object?[] { o.Token, o.StartAmount, o.EndAmount, o.Recipient }).ToArray(),
                new object?[]
                {
                    Info.CosignerData.DecayStartTime,
                    Info.CosignerData.DecayEndTime,
                    Info.CosignerData.ExclusiveFiller,
                    Info.CosignerData.ExclusivityOverrideBps,
                    Info.CosignerData.InputOverride,
                    Info.CosignerData.OutputOverrides.ToArray(),
                },
                Info.Cosignature,
            },
        });
    }

    /// <summary>Recovers the cosigner address from the cosignature over the full order hash (uniswapx-sdk <c>recoverCosigner</c>).</summary>
    public string RecoverCosigner()
    {
        string hash = CosignatureHash(Info.CosignerData);
        var signer = new EthereumMessageSigner();
        return AddressValidator.GetAddress(signer.EncodeUTF8AndEcRecover(hash, Info.Cosignature));
    }
}
