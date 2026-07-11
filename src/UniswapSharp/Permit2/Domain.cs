using System.Numerics;

namespace UniswapSharp.Permit2;

/// <summary>
/// An EIP-712 typed-data domain (ethers <c>TypedDataDomain</c>). Only the non-null fields participate in the
/// domain separator, in the fixed order <c>name, version, chainId, verifyingContract, salt</c>.
/// </summary>
public sealed record Eip712Domain
{
    public string? Name { get; init; }
    public string? Version { get; init; }
    public BigInteger? ChainId { get; init; }
    public string? VerifyingContract { get; init; }
    public byte[]? Salt { get; init; }

    /// <summary>Projects the present domain fields to the message dictionary consumed by the EIP-712 encoder.</summary>
    internal IReadOnlyDictionary<string, object?> ToMessage()
    {
        var message = new Dictionary<string, object?>();
        if (Name is not null)
        {
            message["name"] = Name;
        }
        if (Version is not null)
        {
            message["version"] = Version;
        }
        if (ChainId is not null)
        {
            message["chainId"] = ChainId.Value;
        }
        if (VerifyingContract is not null)
        {
            message["verifyingContract"] = VerifyingContract;
        }
        if (Salt is not null)
        {
            message["salt"] = Salt;
        }
        return message;
    }
}

/// <summary>
/// The data to be sent in an <c>eth_signTypedData</c> RPC call: the EIP-712 <see cref="Domain"/>, the struct
/// <see cref="Types"/> and the message <see cref="Values"/>. Mirrors permit2-sdk's <c>PermitData</c> (and the
/// per-shape <c>PermitTransferFromData</c> / <c>PermitBatchTransferFromData</c> / <c>PermitSingleData</c> /
/// <c>PermitBatchData</c> aliases, which are structurally identical in C#).
/// </summary>
public sealed record PermitData(
    Eip712Domain Domain,
    IReadOnlyDictionary<string, IReadOnlyList<TypedDataField>> Types,
    IReadOnlyDictionary<string, object?> Values);

/// <summary>Port of permit2-sdk <c>domain.ts</c>.</summary>
public static class Domain
{
    private const string Permit2DomainName = "Permit2";

    /// <summary>Builds the Permit2 EIP-712 domain (<c>name: "Permit2"</c>, <paramref name="chainId"/>, <paramref name="permit2Address"/>).</summary>
    public static Eip712Domain Permit2Domain(string permit2Address, int chainId) => new()
    {
        Name = Permit2DomainName,
        ChainId = chainId,
        VerifyingContract = permit2Address,
    };
}
