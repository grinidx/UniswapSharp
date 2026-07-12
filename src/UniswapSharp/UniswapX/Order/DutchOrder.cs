using System.Numerics;
using System.Text;
using Nethereum.Util;
using UniswapSharp.Core.Utils;
using UniswapSharp.Permit2;
using UniswapSharp.UniswapX.Utils;
using UniswapSharp.V4.Utils;

namespace UniswapSharp.UniswapX.Order;

/// <summary>Full order info for a <see cref="DutchOrder"/> (uniswapx-sdk <c>DutchOrderInfo</c>).</summary>
public sealed record DutchOrderInfo
{
    public required string Reactor { get; set; }
    public required string Swapper { get; set; }
    public required BigInteger Nonce { get; set; }
    public required long Deadline { get; set; }
    public required string AdditionalValidationContract { get; set; }
    public required string AdditionalValidationData { get; set; }
    public required long DecayStartTime { get; set; }
    public required long DecayEndTime { get; set; }
    public required string ExclusiveFiller { get; set; }
    public required BigInteger ExclusivityOverrideBps { get; set; }
    public required DutchInput Input { get; set; }
    public required List<DutchOutput> Outputs { get; set; }
}

/// <summary>Port of uniswapx-sdk <c>order/DutchOrder.ts</c>: an ExclusiveDutchOrder with EIP-712 hashing + serialization.</summary>
public sealed class DutchOrder : IOffChainOrder
{
    private const string DutchOrderAbi =
        "tuple(tuple(address,address,uint256,uint256,address,bytes),uint256,uint256,address,uint256,tuple(address,uint256,uint256),tuple(address,uint256,uint256,address)[])";

    internal static readonly IReadOnlyDictionary<string, IReadOnlyList<TypedDataField>> DutchOrderTypes =
        new Dictionary<string, IReadOnlyList<TypedDataField>>
        {
            ["ExclusiveDutchOrder"] = new[]
            {
                new TypedDataField("info", "OrderInfo"),
                new TypedDataField("decayStartTime", "uint256"),
                new TypedDataField("decayEndTime", "uint256"),
                new TypedDataField("exclusiveFiller", "address"),
                new TypedDataField("exclusivityOverrideBps", "uint256"),
                new TypedDataField("inputToken", "address"),
                new TypedDataField("inputStartAmount", "uint256"),
                new TypedDataField("inputEndAmount", "uint256"),
                new TypedDataField("outputs", "DutchOutput[]"),
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

    public DutchOrderInfo Info { get; }

    public int ChainId { get; }

    public DutchOrder(DutchOrderInfo info, int chainId, string? permit2Address = null)
    {
        Info = info;
        ChainId = chainId;
        if (permit2Address != null)
        {
            Permit2Address = permit2Address;
        }
        else if (Constants.Permit2Mapping.TryGetValue(chainId, out var mapped))
        {
            Permit2Address = mapped;
        }
        else
        {
            throw new MissingConfiguration("permit2", chainId.ToString());
        }
    }

    /// <summary>keccak256(utf8(text)) (uniswapx-sdk <c>id</c>).</summary>
    public static string Id(string text) =>
        "0x" + Convert.ToHexStringLower(Sha3Keccack.Current.CalculateHash(Encoding.UTF8.GetBytes(text)));

    public static DutchOrder Parse(string encoded, int chainId, string? permit2 = null)
    {
        var decoded = AbiParamDecoder.Decode(new[] { DutchOrderAbi }, encoded);
        var tuple = (List<object?>)decoded[0]!;
        var info = (List<object?>)tuple[0]!;
        var input = (List<object?>)tuple[5]!;
        var outputs = (List<object?>)tuple[6]!;

        return new DutchOrder(
            new DutchOrderInfo
            {
                Reactor = AddressValidator.GetAddress((string)info[0]!),
                Swapper = AddressValidator.GetAddress((string)info[1]!),
                Nonce = (BigInteger)info[2]!,
                Deadline = (long)(BigInteger)info[3]!,
                AdditionalValidationContract = AddressValidator.GetAddress((string)info[4]!),
                AdditionalValidationData = (string)info[5]!,
                DecayStartTime = (long)(BigInteger)tuple[1]!,
                DecayEndTime = (long)(BigInteger)tuple[2]!,
                ExclusiveFiller = AddressValidator.GetAddress((string)tuple[3]!),
                ExclusivityOverrideBps = (BigInteger)tuple[4]!,
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
            },
            chainId,
            permit2);
    }

    public BlockOverrides? BlockOverrides => null;

    public string Serialize()
    {
        return AbiParamEncoder.Encode(new[] { DutchOrderAbi }, new object?[]
        {
            new object?[]
            {
                OrderEncoding.OrderInfoTuple(Info.Reactor, Info.Swapper, Info.Nonce, Info.Deadline, Info.AdditionalValidationContract, Info.AdditionalValidationData),
                Info.DecayStartTime,
                Info.DecayEndTime,
                Info.ExclusiveFiller,
                Info.ExclusivityOverrideBps,
                new object?[] { Info.Input.Token, Info.Input.StartAmount, Info.Input.EndAmount },
                Info.Outputs.Select(o => new object?[] { o.Token, o.StartAmount, o.EndAmount, o.Recipient }).ToArray(),
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

    public string Hash() => Eip712TypedDataEncoder.HashStructHex(DutchOrderTypes, WitnessInfo());

    /// <summary>Resolves the order at <paramref name="options"/> (uniswapx-sdk <c>resolve</c>).</summary>
    public ResolvedUniswapXOrder Resolve(OrderResolutionOptions options)
    {
        bool useOverride =
            Info.ExclusiveFiller != AddressZero &&
            options.Timestamp <= Info.DecayStartTime &&
            options.Filler != Info.ExclusiveFiller;

        var input = new TokenAmount(
            Info.Input.Token,
            DutchDecay.GetDecayedAmount(
                new DutchDecayConfig(Info.Input.StartAmount, Info.Input.EndAmount, Info.DecayStartTime, Info.DecayEndTime),
                options.Timestamp));

        var outputs = Info.Outputs.Select(output =>
        {
            BigInteger baseAmount = DutchDecay.GetDecayedAmount(
                new DutchDecayConfig(output.StartAmount, output.EndAmount, Info.DecayStartTime, Info.DecayEndTime),
                options.Timestamp);
            BigInteger amount = baseAmount;
            if (useOverride)
            {
                if (Info.ExclusivityOverrideBps.IsZero)
                {
                    amount = MaxUint256;
                }
                else
                {
                    amount = baseAmount * (Info.ExclusivityOverrideBps + Constants.Bps) / Constants.Bps;
                }
            }
            return new TokenAmount(output.Token, amount);
        }).ToList();

        return new ResolvedUniswapXOrder(input, outputs);
    }

    /// <summary>The parsed custom validation for this order (uniswapx-sdk <c>validation</c>).</summary>
    public CustomOrderValidation Validation => UniswapX.Order.Validation.ParseValidation(new OrderInfo
    {
        Reactor = Info.Reactor,
        Swapper = Info.Swapper,
        Nonce = Info.Nonce,
        Deadline = Info.Deadline,
        AdditionalValidationContract = Info.AdditionalValidationContract,
        AdditionalValidationData = Info.AdditionalValidationData,
    });

    private const string AddressZero = "0x0000000000000000000000000000000000000000";
    private static readonly BigInteger MaxUint256 = (BigInteger.One << 256) - 1;

    private PermitTransferFrom ToPermit() => new(
        new TokenPermissions(Info.Input.Token, Info.Input.EndAmount),
        Info.Reactor,
        Info.Nonce,
        Info.Deadline);

    private Dictionary<string, object?> WitnessInfo() => new()
    {
        ["info"] = OrderEncoding.OrderInfoDict(Info.Reactor, Info.Swapper, Info.Nonce, Info.Deadline, Info.AdditionalValidationContract, Info.AdditionalValidationData),
        ["decayStartTime"] = (BigInteger)Info.DecayStartTime,
        ["decayEndTime"] = (BigInteger)Info.DecayEndTime,
        ["exclusiveFiller"] = Info.ExclusiveFiller,
        ["exclusivityOverrideBps"] = Info.ExclusivityOverrideBps,
        ["inputToken"] = Info.Input.Token,
        ["inputStartAmount"] = Info.Input.StartAmount,
        ["inputEndAmount"] = Info.Input.EndAmount,
        ["outputs"] = Info.Outputs.Select(o => new Dictionary<string, object?>
        {
            ["token"] = o.Token,
            ["startAmount"] = o.StartAmount,
            ["endAmount"] = o.EndAmount,
            ["recipient"] = o.Recipient,
        }).ToArray(),
    };

    private Witness Witness() => new(WitnessInfo(), "ExclusiveDutchOrder", DutchOrderTypes);
}
