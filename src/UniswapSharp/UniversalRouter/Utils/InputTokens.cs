using System.Numerics;
using UniswapSharp.Permit2;
using UniswapSharp.V4.Utils;

namespace UniswapSharp.UniversalRouter.Utils;

/// <summary>
/// A Permit2 <c>PermitSingle</c> plus its signature. Port of universal-router-sdk
/// <c>utils/inputTokens.ts</c> (<c>Permit2Permit extends PermitSingle</c>).
/// </summary>
public sealed record Permit2Permit(
    PermitDetails Details,
    string Spender,
    BigInteger SigDeadline,
    string Signature);

/// <summary>A Permit2 transfer-from. Port of <c>Permit2TransferFrom</c>.</summary>
public sealed record Permit2TransferFrom(string Token, string Amount, string? Recipient = null);

/// <summary>Input-token options. Port of <c>InputTokenOptions</c>.</summary>
public sealed record InputTokenOptions(
    Permit2Permit? Permit2Permit = null,
    Permit2TransferFrom? Permit2TransferFrom = null);

/// <summary>
/// Port of universal-router-sdk <c>utils/inputTokens.ts</c> — commands to gather input tokens for a trade.
/// </summary>
public static class InputTokens
{
    private const int SIGNATURE_LENGTH = 65;
    private const int EIP_2098_SIGNATURE_LENGTH = 64;

    /// <summary>Encodes a PERMIT2_PERMIT command, sanitizing the signature (ECDSA / EIP-2098 edge cases).</summary>
    public static void EncodePermit(RoutePlanner planner, Permit2Permit permit2)
    {
        string signature = permit2.Signature;

        byte[] sigBytes = AbiParamEncoder.ToBytes(permit2.Signature);
        // signature data provided for EIP-1271 may have length different from ECDSA signature
        if (sigBytes.Length is SIGNATURE_LENGTH or EIP_2098_SIGNATURE_LENGTH)
        {
            // sanitizes signature to cover edge cases of malformed EIP-2098 sigs and v used as recovery id
            signature = Signatures.JoinSignature(Signatures.SplitSignature(permit2.Signature));
        }

        planner.AddCommand(CommandType.PERMIT2_PERMIT, new object?[] { PermitToTuple(permit2), signature });
    }

    /// <summary>Encodes the commands needed to gather input tokens (optional permit + transfer-from).</summary>
    public static void EncodeInputTokenOptions(RoutePlanner planner, InputTokenOptions options)
    {
        // first ensure that all tokens provided for encoding are the same
        if (options.Permit2TransferFrom is not null && options.Permit2Permit is not null &&
            options.Permit2TransferFrom.Token != options.Permit2Permit.Details.Token)
        {
            throw new InvalidOperationException("inconsistent token");
        }

        if (options.Permit2Permit is not null)
        {
            EncodePermit(planner, options.Permit2Permit);
        }

        if (options.Permit2TransferFrom is not null)
        {
            planner.AddCommand(CommandType.PERMIT2_TRANSFER_FROM, new object?[]
            {
                options.Permit2TransferFrom.Token,
                options.Permit2TransferFrom.Recipient ?? Constants.ROUTER_AS_RECIPIENT,
                options.Permit2TransferFrom.Amount,
            });
        }
    }

    internal static object?[] PermitToTuple(Permit2Permit p) => new object?[]
    {
        new object?[] { p.Details.Token, p.Details.Amount, p.Details.Expiration, p.Details.Nonce },
        p.Spender,
        p.SigDeadline,
    };
}
