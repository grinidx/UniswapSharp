using System.Numerics;

namespace UniswapSharp.Permit2;

/// <summary>The per-token allowance details of a permit (permit2-sdk <c>PermitDetails</c>).</summary>
public sealed record PermitDetails(string Token, BigInteger Amount, BigInteger Expiration, BigInteger Nonce);

/// <summary>A single-token allowance <c>PermitSingle</c> (permit2-sdk).</summary>
public sealed record PermitSingle(PermitDetails Details, string Spender, BigInteger SigDeadline);

/// <summary>A multi-token allowance <c>PermitBatch</c> (permit2-sdk).</summary>
public sealed record PermitBatch(IReadOnlyList<PermitDetails> Details, string Spender, BigInteger SigDeadline);

/// <summary>Port of permit2-sdk <c>allowanceTransfer.ts</c>: EIP-712 typed data + hash for (batch) allowance permits.</summary>
public static class AllowanceTransfer
{
    private static readonly IReadOnlyList<TypedDataField> PermitDetailsFields = new[]
    {
        new TypedDataField("token", "address"),
        new TypedDataField("amount", "uint160"),
        new TypedDataField("expiration", "uint48"),
        new TypedDataField("nonce", "uint48"),
    };

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<TypedDataField>> PermitTypes =
        new Dictionary<string, IReadOnlyList<TypedDataField>>
        {
            ["PermitSingle"] = new[]
            {
                new TypedDataField("details", "PermitDetails"),
                new TypedDataField("spender", "address"),
                new TypedDataField("sigDeadline", "uint256"),
            },
            ["PermitDetails"] = PermitDetailsFields,
        };

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<TypedDataField>> PermitBatchTypes =
        new Dictionary<string, IReadOnlyList<TypedDataField>>
        {
            ["PermitBatch"] = new[]
            {
                new TypedDataField("details", "PermitDetails[]"),
                new TypedDataField("spender", "address"),
                new TypedDataField("sigDeadline", "uint256"),
            },
            ["PermitDetails"] = PermitDetailsFields,
        };

    /// <summary>Returns the typed data to sign for the single-token <paramref name="permit"/>.</summary>
    public static PermitData GetPermitData(PermitSingle permit, string permit2Address, int chainId)
    {
        Invariant(Constants.MaxSigDeadline >= permit.SigDeadline, "SIG_DEADLINE_OUT_OF_RANGE");

        var domain = Domain.Permit2Domain(permit2Address, chainId);
        ValidatePermitDetails(permit.Details);
        return new PermitData(domain, PermitTypes, ToMessage(permit));
    }

    /// <summary>Returns the typed data to sign for the batch <paramref name="permit"/>.</summary>
    public static PermitData GetPermitData(PermitBatch permit, string permit2Address, int chainId)
    {
        Invariant(Constants.MaxSigDeadline >= permit.SigDeadline, "SIG_DEADLINE_OUT_OF_RANGE");

        var domain = Domain.Permit2Domain(permit2Address, chainId);
        foreach (var details in permit.Details)
        {
            ValidatePermitDetails(details);
        }
        return new PermitData(domain, PermitBatchTypes, ToMessage(permit));
    }

    /// <summary>Returns the EIP-712 signing hash (lower-case <c>0x</c>-prefixed) for the single-token <paramref name="permit"/>.</summary>
    public static string Hash(PermitSingle permit, string permit2Address, int chainId)
    {
        var (domain, types, values) = GetPermitData(permit, permit2Address, chainId);
        return Eip712TypedDataEncoder.HashHex(domain, types, values);
    }

    /// <summary>Returns the EIP-712 signing hash (lower-case <c>0x</c>-prefixed) for the batch <paramref name="permit"/>.</summary>
    public static string Hash(PermitBatch permit, string permit2Address, int chainId)
    {
        var (domain, types, values) = GetPermitData(permit, permit2Address, chainId);
        return Eip712TypedDataEncoder.HashHex(domain, types, values);
    }

    private static Dictionary<string, object?> ToMessage(PermitSingle permit) => new()
    {
        ["details"] = ToMessage(permit.Details),
        ["spender"] = permit.Spender,
        ["sigDeadline"] = permit.SigDeadline,
    };

    private static Dictionary<string, object?> ToMessage(PermitBatch permit) => new()
    {
        ["details"] = permit.Details.Select(ToMessage).ToArray<object?>(),
        ["spender"] = permit.Spender,
        ["sigDeadline"] = permit.SigDeadline,
    };

    private static Dictionary<string, object?> ToMessage(PermitDetails details) => new()
    {
        ["token"] = details.Token,
        ["amount"] = details.Amount,
        ["expiration"] = details.Expiration,
        ["nonce"] = details.Nonce,
    };

    private static void ValidatePermitDetails(PermitDetails details)
    {
        Invariant(Constants.MaxOrderedNonce >= details.Nonce, "NONCE_OUT_OF_RANGE");
        Invariant(Constants.MaxAllowanceTransferAmount >= details.Amount, "AMOUNT_OUT_OF_RANGE");
        Invariant(Constants.MaxAllowanceExpiration >= details.Expiration, "EXPIRATION_OUT_OF_RANGE");
    }

    private static void Invariant(bool condition, string message)
    {
        if (!condition)
        {
            throw new ArgumentException(message);
        }
    }
}
