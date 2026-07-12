using System.Numerics;
using UniswapSharp.Core.Utils;
using UniswapSharp.Permit2;
using UniswapSharp.UniswapX.Utils;
using UniswapSharp.V4.Utils;

namespace UniswapSharp.UniswapX.Order;

/// <summary>A relay order input (uniswapx-sdk <c>RelayInput</c>).</summary>
public sealed record RelayInput
{
    public required string Token { get; set; }
    public required BigInteger Amount { get; set; }
    public required string Recipient { get; set; }
}

/// <summary>A relay order fee escalator (uniswapx-sdk <c>RelayFee</c>).</summary>
public sealed record RelayFee
{
    public required string Token { get; set; }
    public required BigInteger StartAmount { get; set; }
    public required BigInteger EndAmount { get; set; }
    public required long StartTime { get; set; }
    public required long EndTime { get; set; }
}

/// <summary>Full order info for a <see cref="RelayOrder"/> (uniswapx-sdk <c>RelayOrderInfo</c>).</summary>
public sealed record RelayOrderInfo
{
    public required string Reactor { get; set; }
    public required string Swapper { get; set; }
    public required BigInteger Nonce { get; set; }
    public required long Deadline { get; set; }
    public required RelayInput Input { get; set; }
    public required RelayFee Fee { get; set; }
    public required string UniversalRouterCalldata { get; set; }
}

/// <summary>Port of uniswapx-sdk <c>order/RelayOrder.ts</c>.</summary>
public sealed class RelayOrder : IOffChainOrder
{
    private const string RelayOrderAbi =
        "tuple(tuple(address,address,uint256,uint256),tuple(address,uint256,address),tuple(address,uint256,uint256,uint256,uint256),bytes)";

    internal static readonly IReadOnlyDictionary<string, IReadOnlyList<TypedDataField>> RelayWitnessTypes =
        new Dictionary<string, IReadOnlyList<TypedDataField>>
        {
            ["RelayOrder"] = new[]
            {
                new TypedDataField("info", "RelayOrderInfo"),
                new TypedDataField("input", "Input"),
                new TypedDataField("fee", "FeeEscalator"),
                new TypedDataField("universalRouterCalldata", "bytes"),
            },
            ["RelayOrderInfo"] = new[]
            {
                new TypedDataField("reactor", "address"),
                new TypedDataField("swapper", "address"),
                new TypedDataField("nonce", "uint256"),
                new TypedDataField("deadline", "uint256"),
            },
            ["Input"] = new[]
            {
                new TypedDataField("token", "address"),
                new TypedDataField("amount", "uint256"),
                new TypedDataField("recipient", "address"),
            },
            ["FeeEscalator"] = new[]
            {
                new TypedDataField("token", "address"),
                new TypedDataField("startAmount", "uint256"),
                new TypedDataField("endAmount", "uint256"),
                new TypedDataField("startTime", "uint256"),
                new TypedDataField("endTime", "uint256"),
            },
        };

    public string Permit2Address { get; }

    public RelayOrderInfo Info { get; }

    public int ChainId { get; }

    public RelayOrder(RelayOrderInfo info, int chainId, string? permit2Address = null)
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

    public static RelayOrder Parse(string encoded, int chainId, string? permit2 = null)
    {
        var decoded = AbiParamDecoder.Decode(new[] { RelayOrderAbi }, encoded);
        var tuple = (List<object?>)decoded[0]!;
        var info = (List<object?>)tuple[0]!;
        var input = (List<object?>)tuple[1]!;
        var fee = (List<object?>)tuple[2]!;

        return new RelayOrder(
            new RelayOrderInfo
            {
                Reactor = AddressValidator.GetAddress((string)info[0]!),
                Swapper = AddressValidator.GetAddress((string)info[1]!),
                Nonce = (BigInteger)info[2]!,
                Deadline = (long)(BigInteger)info[3]!,
                Input = new RelayInput
                {
                    Token = AddressValidator.GetAddress((string)input[0]!),
                    Amount = (BigInteger)input[1]!,
                    Recipient = AddressValidator.GetAddress((string)input[2]!),
                },
                Fee = new RelayFee
                {
                    Token = AddressValidator.GetAddress((string)fee[0]!),
                    StartAmount = (BigInteger)fee[1]!,
                    EndAmount = (BigInteger)fee[2]!,
                    StartTime = (long)(BigInteger)fee[3]!,
                    EndTime = (long)(BigInteger)fee[4]!,
                },
                UniversalRouterCalldata = (string)tuple[3]!,
            },
            chainId,
            permit2);
    }

    public BlockOverrides? BlockOverrides => null;

    public string Serialize()
    {
        return AbiParamEncoder.Encode(new[] { RelayOrderAbi }, new object?[]
        {
            new object?[]
            {
                new object?[] { Info.Reactor, Info.Swapper, Info.Nonce, Info.Deadline },
                new object?[] { Info.Input.Token, Info.Input.Amount, Info.Input.Recipient },
                new object?[] { Info.Fee.Token, Info.Fee.StartAmount, Info.Fee.EndAmount, Info.Fee.StartTime, Info.Fee.EndTime },
                Info.UniversalRouterCalldata,
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

    public string Hash() => Eip712TypedDataEncoder.HashStructHex(RelayWitnessTypes, WitnessInfo());

    /// <summary>Resolves the order at <paramref name="options"/> (uniswapx-sdk <c>resolve</c>).</summary>
    public ResolvedRelayOrder Resolve(OrderResolutionOptions options)
    {
        BigInteger amount = DutchDecay.GetDecayedAmount(
            new DutchDecayConfig(Info.Fee.StartAmount, Info.Fee.EndAmount, Info.Fee.StartTime, Info.Fee.EndTime),
            options.Timestamp);
        return new ResolvedRelayOrder(new ResolvedRelayFee(Info.Fee.Token, amount));
    }

    private PermitBatchTransferFrom ToPermit() => new(
        new[]
        {
            new TokenPermissions(Info.Input.Token, Info.Input.Amount),
            new TokenPermissions(Info.Fee.Token, Info.Fee.EndAmount),
        },
        Info.Reactor,
        Info.Nonce,
        Info.Deadline);

    private Dictionary<string, object?> WitnessInfo() => new()
    {
        ["info"] = new Dictionary<string, object?>
        {
            ["reactor"] = Info.Reactor,
            ["swapper"] = Info.Swapper,
            ["nonce"] = Info.Nonce,
            ["deadline"] = (BigInteger)Info.Deadline,
        },
        ["input"] = new Dictionary<string, object?>
        {
            ["token"] = Info.Input.Token,
            ["amount"] = Info.Input.Amount,
            ["recipient"] = Info.Input.Recipient,
        },
        ["fee"] = new Dictionary<string, object?>
        {
            ["token"] = Info.Fee.Token,
            ["startAmount"] = Info.Fee.StartAmount,
            ["endAmount"] = Info.Fee.EndAmount,
            ["startTime"] = (BigInteger)Info.Fee.StartTime,
            ["endTime"] = (BigInteger)Info.Fee.EndTime,
        },
        ["universalRouterCalldata"] = Info.UniversalRouterCalldata,
    };

    private Witness Witness() => new(WitnessInfo(), "RelayOrder", RelayWitnessTypes);
}
