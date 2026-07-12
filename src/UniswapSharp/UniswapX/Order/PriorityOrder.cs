using System.Numerics;
using Nethereum.Signer;
using Nethereum.Util;
using UniswapSharp.Core.Utils;
using UniswapSharp.Permit2;
using UniswapSharp.UniswapX.Utils;
using UniswapSharp.V4.Utils;

namespace UniswapSharp.UniswapX.Order;

/// <summary>Thrown when a priority order cannot be filled at the given block (uniswapx-sdk <c>OrderNotFillable</c>).</summary>
public sealed class OrderNotFillable : Exception
{
    public OrderNotFillable(string message) : base(message)
    {
    }
}

/// <summary>Priority order cosigner data (uniswapx-sdk <c>PriorityCosignerData</c>).</summary>
public sealed record PriorityCosignerData
{
    public required BigInteger AuctionTargetBlock { get; set; }
}

/// <summary>Unsigned order info for a priority order (uniswapx-sdk <c>UnsignedPriorityOrderInfo</c>).</summary>
public record UnsignedPriorityOrderInfo
{
    public required string Reactor { get; set; }
    public required string Swapper { get; set; }
    public required BigInteger Nonce { get; set; }
    public required long Deadline { get; set; }
    public required string AdditionalValidationContract { get; set; }
    public required string AdditionalValidationData { get; set; }
    public required string Cosigner { get; set; }
    public required BigInteger AuctionStartBlock { get; set; }
    public required BigInteger BaselinePriorityFeeWei { get; set; }
    public required PriorityInput Input { get; set; }
    public required List<PriorityOutput> Outputs { get; set; }
}

/// <summary>Cosigned order info for a priority order (uniswapx-sdk <c>CosignedPriorityOrderInfo</c>).</summary>
public sealed record CosignedPriorityOrderInfo : UnsignedPriorityOrderInfo
{
    public required PriorityCosignerData CosignerData { get; set; }
    public required string Cosignature { get; set; }
}

/// <summary>Port of uniswapx-sdk <c>order/PriorityOrder.ts</c> (<c>UnsignedPriorityOrder</c>).</summary>
public class UnsignedPriorityOrder : IOffChainOrder
{
    internal const string PriorityOrderAbi =
        "tuple(tuple(address,address,uint256,uint256,address,bytes),address,uint256,uint256,tuple(address,uint256,uint256),tuple(address,uint256,uint256,address)[],tuple(uint256),bytes)";

    internal static readonly IReadOnlyDictionary<string, IReadOnlyList<TypedDataField>> PriorityOrderTypes =
        new Dictionary<string, IReadOnlyList<TypedDataField>>
        {
            ["PriorityOrder"] = new[]
            {
                new TypedDataField("info", "OrderInfo"),
                new TypedDataField("cosigner", "address"),
                new TypedDataField("auctionStartBlock", "uint256"),
                new TypedDataField("baselinePriorityFeeWei", "uint256"),
                new TypedDataField("input", "PriorityInput"),
                new TypedDataField("outputs", "PriorityOutput[]"),
            },
            ["OrderInfo"] = OrderEncoding.OrderInfoFields,
            ["PriorityInput"] = new[]
            {
                new TypedDataField("token", "address"),
                new TypedDataField("amount", "uint256"),
                new TypedDataField("mpsPerPriorityFeeWei", "uint256"),
            },
            ["PriorityOutput"] = new[]
            {
                new TypedDataField("token", "address"),
                new TypedDataField("amount", "uint256"),
                new TypedDataField("mpsPerPriorityFeeWei", "uint256"),
                new TypedDataField("recipient", "address"),
            },
        };

    public string Permit2Address { get; }

    public UnsignedPriorityOrderInfo Info { get; }

    public int ChainId { get; }

    public UnsignedPriorityOrder(UnsignedPriorityOrderInfo info, int chainId, string? permit2Address = null)
    {
        Info = info;
        ChainId = chainId;
        Permit2Address = OrderUtils.GetPermit2(chainId, permit2Address);
    }

    public static UnsignedPriorityOrder Parse(string encoded, int chainId, string? permit2 = null) =>
        new(ParseSerializedOrder(encoded), chainId, permit2);

    public virtual BlockOverrides? BlockOverrides => new(OrderUtils.HexStripZeros(Info.AuctionStartBlock));

    public virtual string Serialize()
    {
        return AbiParamEncoder.Encode(new[] { PriorityOrderAbi }, new object?[]
        {
            new object?[]
            {
                OrderEncoding.OrderInfoTuple(Info.Reactor, Info.Swapper, Info.Nonce, Info.Deadline, Info.AdditionalValidationContract, Info.AdditionalValidationData),
                Info.Cosigner,
                Info.AuctionStartBlock,
                Info.BaselinePriorityFeeWei,
                new object?[] { Info.Input.Token, Info.Input.Amount, Info.Input.MpsPerPriorityFeeWei },
                Info.Outputs.Select(o => new object?[] { o.Token, o.Amount, o.MpsPerPriorityFeeWei, o.Recipient }).ToArray(),
                new object?[] { 0 },
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

    public string Hash() => Eip712TypedDataEncoder.HashStructHex(PriorityOrderTypes, WitnessInfo());

    public virtual ResolvedUniswapXOrder Resolve(PriorityOrderResolutionOptions options) =>
        throw new InvalidOperationException("Method not implemented.");

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
    public string CosignatureHash(PriorityCosignerData cosignerData)
    {
        byte[] hashBytes = Convert.FromHexString(Hash()[2..]);
        byte[] chainIdWord = Word(ChainId);
        string encoded = AbiParamEncoder.Encode(new[] { "tuple(uint256)" }, new object?[]
        {
            new object?[] { cosignerData.AuctionTargetBlock },
        });
        byte[] encodedBytes = Convert.FromHexString(encoded[2..]);
        var packed = new byte[hashBytes.Length + chainIdWord.Length + encodedBytes.Length];
        int off = 0;
        Array.Copy(hashBytes, 0, packed, off, hashBytes.Length); off += hashBytes.Length;
        Array.Copy(chainIdWord, 0, packed, off, chainIdWord.Length); off += chainIdWord.Length;
        Array.Copy(encodedBytes, 0, packed, off, encodedBytes.Length);
        return "0x" + Convert.ToHexStringLower(Sha3Keccack.Current.CalculateHash(packed));
    }

    internal static byte[] Word(BigInteger value)
    {
        byte[] raw = value.ToByteArray(isUnsigned: true, isBigEndian: true);
        var word = new byte[32];
        Array.Copy(raw, 0, word, 32 - raw.Length, raw.Length);
        return word;
    }

    private PermitTransferFrom ToPermit() => new(
        new TokenPermissions(Info.Input.Token, Info.Input.Amount),
        Info.Reactor,
        Info.Nonce,
        Info.Deadline);

    private Dictionary<string, object?> WitnessInfo() => new()
    {
        ["info"] = OrderEncoding.OrderInfoDict(Info.Reactor, Info.Swapper, Info.Nonce, Info.Deadline, Info.AdditionalValidationContract, Info.AdditionalValidationData),
        ["cosigner"] = Info.Cosigner,
        ["auctionStartBlock"] = Info.AuctionStartBlock,
        ["baselinePriorityFeeWei"] = Info.BaselinePriorityFeeWei,
        ["input"] = new Dictionary<string, object?>
        {
            ["token"] = Info.Input.Token,
            ["amount"] = Info.Input.Amount,
            ["mpsPerPriorityFeeWei"] = Info.Input.MpsPerPriorityFeeWei,
        },
        ["outputs"] = Info.Outputs.Select(o => new Dictionary<string, object?>
        {
            ["token"] = o.Token,
            ["amount"] = o.Amount,
            ["mpsPerPriorityFeeWei"] = o.MpsPerPriorityFeeWei,
            ["recipient"] = o.Recipient,
        }).ToArray(),
    };

    private Witness Witness() => new(WitnessInfo(), "PriorityOrder", PriorityOrderTypes);

    internal static CosignedPriorityOrderInfo ParseSerializedOrder(string serialized)
    {
        var decoded = AbiParamDecoder.Decode(new[] { PriorityOrderAbi }, serialized);
        var tuple = (List<object?>)decoded[0]!;
        var info = (List<object?>)tuple[0]!;
        var input = (List<object?>)tuple[4]!;
        var outputs = (List<object?>)tuple[5]!;
        var cosignerData = (List<object?>)tuple[6]!;

        return new CosignedPriorityOrderInfo
        {
            Reactor = AddressValidator.GetAddress((string)info[0]!),
            Swapper = AddressValidator.GetAddress((string)info[1]!),
            Nonce = (BigInteger)info[2]!,
            Deadline = (long)(BigInteger)info[3]!,
            AdditionalValidationContract = AddressValidator.GetAddress((string)info[4]!),
            AdditionalValidationData = (string)info[5]!,
            Cosigner = AddressValidator.GetAddress((string)tuple[1]!),
            AuctionStartBlock = (BigInteger)tuple[2]!,
            BaselinePriorityFeeWei = (BigInteger)tuple[3]!,
            Input = new PriorityInput
            {
                Token = AddressValidator.GetAddress((string)input[0]!),
                Amount = (BigInteger)input[1]!,
                MpsPerPriorityFeeWei = (BigInteger)input[2]!,
            },
            Outputs = outputs.Select(o =>
            {
                var output = (List<object?>)o!;
                return new PriorityOutput
                {
                    Token = AddressValidator.GetAddress((string)output[0]!),
                    Amount = (BigInteger)output[1]!,
                    MpsPerPriorityFeeWei = (BigInteger)output[2]!,
                    Recipient = AddressValidator.GetAddress((string)output[3]!),
                };
            }).ToList(),
            CosignerData = new PriorityCosignerData { AuctionTargetBlock = (BigInteger)cosignerData[0]! },
            Cosignature = (string)tuple[7]!,
        };
    }
}

/// <summary>Port of uniswapx-sdk <c>order/PriorityOrder.ts</c> (<c>CosignedPriorityOrder</c>).</summary>
public sealed class CosignedPriorityOrder : UnsignedPriorityOrder
{
    public new CosignedPriorityOrderInfo Info { get; }

    public CosignedPriorityOrder(CosignedPriorityOrderInfo info, int chainId, string? permit2Address = null)
        : base(info, chainId, permit2Address)
    {
        Info = info;
    }

    public static CosignedPriorityOrder FromUnsignedOrder(
        UnsignedPriorityOrder order, PriorityCosignerData cosignerData, string cosignature)
    {
        return new CosignedPriorityOrder(
            new CosignedPriorityOrderInfo
            {
                Reactor = order.Info.Reactor,
                Swapper = order.Info.Swapper,
                Nonce = order.Info.Nonce,
                Deadline = order.Info.Deadline,
                AdditionalValidationContract = order.Info.AdditionalValidationContract,
                AdditionalValidationData = order.Info.AdditionalValidationData,
                Cosigner = order.Info.Cosigner,
                AuctionStartBlock = order.Info.AuctionStartBlock,
                BaselinePriorityFeeWei = order.Info.BaselinePriorityFeeWei,
                Input = order.Info.Input,
                Outputs = order.Info.Outputs,
                CosignerData = cosignerData,
                Cosignature = cosignature,
            },
            order.ChainId,
            order.Permit2Address);
    }

    public static new CosignedPriorityOrder Parse(string encoded, int chainId, string? permit2 = null) =>
        new(ParseSerializedOrder(encoded), chainId, permit2);

    public override BlockOverrides? BlockOverrides => new(OrderUtils.HexStripZeros(Info.CosignerData.AuctionTargetBlock));

    public override ResolvedUniswapXOrder Resolve(PriorityOrderResolutionOptions options)
    {
        if (options.CurrentBlock is BigInteger currentBlock)
        {
            if (Info.CosignerData.AuctionTargetBlock > 0 && currentBlock < Info.CosignerData.AuctionTargetBlock)
            {
                throw new OrderNotFillable("Target block in the future");
            }
            if (currentBlock < Info.AuctionStartBlock)
            {
                throw new OrderNotFillable("Start block in the future");
            }
        }
        return new ResolvedUniswapXOrder(
            new TokenAmount(Info.Input.Token, ScaleInput(Info.Input, options.PriorityFee)),
            ScaleOutputs(Info.Outputs, options.PriorityFee));
    }

    public override string Serialize()
    {
        return AbiParamEncoder.Encode(new[] { PriorityOrderAbi }, new object?[]
        {
            new object?[]
            {
                OrderEncoding.OrderInfoTuple(Info.Reactor, Info.Swapper, Info.Nonce, Info.Deadline, Info.AdditionalValidationContract, Info.AdditionalValidationData),
                Info.Cosigner,
                Info.AuctionStartBlock,
                Info.BaselinePriorityFeeWei,
                new object?[] { Info.Input.Token, Info.Input.Amount, Info.Input.MpsPerPriorityFeeWei },
                Info.Outputs.Select(o => new object?[] { o.Token, o.Amount, o.MpsPerPriorityFeeWei, o.Recipient }).ToArray(),
                new object?[] { Info.CosignerData.AuctionTargetBlock },
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

    private static readonly BigInteger Mps = Constants.Mps;

    private static BigInteger ScaleInput(PriorityInput input, BigInteger priorityFee)
    {
        if (priorityFee * input.MpsPerPriorityFeeWei >= Mps)
        {
            return BigInteger.Zero;
        }
        return input.Amount * (Mps - priorityFee * input.MpsPerPriorityFeeWei) / Mps;
    }

    private static List<TokenAmount> ScaleOutputs(IReadOnlyList<PriorityOutput> outputs, BigInteger priorityFee)
    {
        return outputs.Select(output =>
        {
            BigInteger product = output.Amount * (Mps + priorityFee * output.MpsPerPriorityFeeWei);
            BigInteger mod = product % Mps;
            BigInteger div = product / Mps;
            return new TokenAmount(output.Token, mod.IsZero ? div : div + 1);
        }).ToList();
    }
}
