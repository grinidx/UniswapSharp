using System.Numerics;

namespace UniswapSharp.Permit2;

/// <summary>The token/amount pair permitted by a signature transfer (permit2-sdk <c>TokenPermissions</c>).</summary>
public sealed record TokenPermissions(string Token, BigInteger Amount);

/// <summary>A single-token <c>PermitTransferFrom</c> (permit2-sdk).</summary>
public sealed record PermitTransferFrom(TokenPermissions Permitted, string Spender, BigInteger Nonce, BigInteger Deadline);

/// <summary>A multi-token <c>PermitBatchTransferFrom</c> (permit2-sdk).</summary>
public sealed record PermitBatchTransferFrom(IReadOnlyList<TokenPermissions> Permitted, string Spender, BigInteger Nonce, BigInteger Deadline);

/// <summary>
/// An extra witness struct appended to a signature-transfer permit (permit2-sdk <c>Witness</c>). <see cref="WitnessValue"/>
/// is the witness message (keyed by field name), <see cref="WitnessTypeName"/> the witness struct's type name, and
/// <see cref="WitnessType"/> its EIP-712 type definition(s).
/// </summary>
public sealed record Witness(
    IReadOnlyDictionary<string, object?> WitnessValue,
    string WitnessTypeName,
    IReadOnlyDictionary<string, IReadOnlyList<TypedDataField>> WitnessType);

/// <summary>Port of permit2-sdk <c>signatureTransfer.ts</c>: EIP-712 typed data + hash for (batch) signature transfers, with optional witness.</summary>
public static class SignatureTransfer
{
    private static readonly IReadOnlyList<TypedDataField> TokenPermissionsFields = new[]
    {
        new TypedDataField("token", "address"),
        new TypedDataField("amount", "uint256"),
    };

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<TypedDataField>> PermitTransferFromTypes =
        new Dictionary<string, IReadOnlyList<TypedDataField>>
        {
            ["PermitTransferFrom"] = new[]
            {
                new TypedDataField("permitted", "TokenPermissions"),
                new TypedDataField("spender", "address"),
                new TypedDataField("nonce", "uint256"),
                new TypedDataField("deadline", "uint256"),
            },
            ["TokenPermissions"] = TokenPermissionsFields,
        };

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<TypedDataField>> PermitBatchTransferFromTypes =
        new Dictionary<string, IReadOnlyList<TypedDataField>>
        {
            ["PermitBatchTransferFrom"] = new[]
            {
                new TypedDataField("permitted", "TokenPermissions[]"),
                new TypedDataField("spender", "address"),
                new TypedDataField("nonce", "uint256"),
                new TypedDataField("deadline", "uint256"),
            },
            ["TokenPermissions"] = TokenPermissionsFields,
        };

    /// <summary>Returns the typed data to sign for <paramref name="permit"/>, optionally including a <paramref name="witness"/>.</summary>
    public static PermitData GetPermitData(PermitTransferFrom permit, string permit2Address, int chainId, Witness? witness = null)
    {
        Invariant(Constants.MaxSigDeadline >= permit.Deadline, "SIG_DEADLINE_OUT_OF_RANGE");
        Invariant(Constants.MaxUnorderedNonce >= permit.Nonce, "NONCE_OUT_OF_RANGE");

        var domain = Domain.Permit2Domain(permit2Address, chainId);
        ValidateTokenPermissions(permit.Permitted);

        var types = witness is null ? PermitTransferFromTypes : PermitTransferFromWithWitnessType(witness);
        var values = ToMessage(permit);
        if (witness is not null)
        {
            values["witness"] = witness.WitnessValue;
        }
        return new PermitData(domain, types, values);
    }

    /// <summary>Returns the typed data to sign for the batch <paramref name="permit"/>, optionally including a <paramref name="witness"/>.</summary>
    public static PermitData GetPermitData(PermitBatchTransferFrom permit, string permit2Address, int chainId, Witness? witness = null)
    {
        Invariant(Constants.MaxSigDeadline >= permit.Deadline, "SIG_DEADLINE_OUT_OF_RANGE");
        Invariant(Constants.MaxUnorderedNonce >= permit.Nonce, "NONCE_OUT_OF_RANGE");

        var domain = Domain.Permit2Domain(permit2Address, chainId);
        foreach (var permitted in permit.Permitted)
        {
            ValidateTokenPermissions(permitted);
        }

        var types = witness is null ? PermitBatchTransferFromTypes : PermitBatchTransferFromWithWitnessType(witness);
        var values = ToMessage(permit);
        if (witness is not null)
        {
            values["witness"] = witness.WitnessValue;
        }
        return new PermitData(domain, types, values);
    }

    /// <summary>Returns the EIP-712 signing hash (lower-case <c>0x</c>-prefixed) for <paramref name="permit"/>.</summary>
    public static string Hash(PermitTransferFrom permit, string permit2Address, int chainId, Witness? witness = null)
    {
        var (domain, types, values) = GetPermitData(permit, permit2Address, chainId, witness);
        return Eip712TypedDataEncoder.HashHex(domain, types, values);
    }

    /// <summary>Returns the EIP-712 signing hash (lower-case <c>0x</c>-prefixed) for the batch <paramref name="permit"/>.</summary>
    public static string Hash(PermitBatchTransferFrom permit, string permit2Address, int chainId, Witness? witness = null)
    {
        var (domain, types, values) = GetPermitData(permit, permit2Address, chainId, witness);
        return Eip712TypedDataEncoder.HashHex(domain, types, values);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<TypedDataField>> PermitTransferFromWithWitnessType(Witness witness) =>
        WithWitness("PermitWitnessTransferFrom", "TokenPermissions", witness);

    private static IReadOnlyDictionary<string, IReadOnlyList<TypedDataField>> PermitBatchTransferFromWithWitnessType(Witness witness) =>
        WithWitness("PermitBatchWitnessTransferFrom", "TokenPermissions[]", witness);

    private static IReadOnlyDictionary<string, IReadOnlyList<TypedDataField>> WithWitness(string primaryType, string permittedType, Witness witness)
    {
        var types = new Dictionary<string, IReadOnlyList<TypedDataField>>
        {
            [primaryType] = new[]
            {
                new TypedDataField("permitted", permittedType),
                new TypedDataField("spender", "address"),
                new TypedDataField("nonce", "uint256"),
                new TypedDataField("deadline", "uint256"),
                new TypedDataField("witness", witness.WitnessTypeName),
            },
            ["TokenPermissions"] = TokenPermissionsFields,
        };
        foreach (var (name, fields) in witness.WitnessType)
        {
            types[name] = fields;
        }
        return types;
    }

    private static Dictionary<string, object?> ToMessage(PermitTransferFrom permit) => new()
    {
        ["permitted"] = ToMessage(permit.Permitted),
        ["spender"] = permit.Spender,
        ["nonce"] = permit.Nonce,
        ["deadline"] = permit.Deadline,
    };

    private static Dictionary<string, object?> ToMessage(PermitBatchTransferFrom permit) => new()
    {
        ["permitted"] = permit.Permitted.Select(ToMessage).ToArray<object?>(),
        ["spender"] = permit.Spender,
        ["nonce"] = permit.Nonce,
        ["deadline"] = permit.Deadline,
    };

    private static Dictionary<string, object?> ToMessage(TokenPermissions permitted) => new()
    {
        ["token"] = permitted.Token,
        ["amount"] = permitted.Amount,
    };

    private static void ValidateTokenPermissions(TokenPermissions permissions) =>
        Invariant(Constants.MaxSignatureTransferAmount >= permissions.Amount, "AMOUNT_OUT_OF_RANGE");

    private static void Invariant(bool condition, string message)
    {
        if (!condition)
        {
            throw new ArgumentException(message);
        }
    }
}
