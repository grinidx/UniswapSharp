using UniswapSharp.V4.Utils;

namespace UniswapSharp.UniversalRouter.Utils;

/// <summary>A split ECDSA signature: r, s and the recovery parameter (0/1).</summary>
public sealed record SplitSig(byte[] R, byte[] S, int RecoveryParam);

/// <summary>
/// Port of the ethers <c>splitSignature</c>/<c>joinSignature</c> pair used by <c>encodePermit</c> to sanitize
/// signatures (malformed EIP-2098 compact sigs, and <c>v</c> supplied as a 0/1 recovery id).
/// </summary>
public static class Signatures
{
    /// <summary>Split a 64-byte (EIP-2098 compact) or 65-byte signature into r/s/recoveryParam.</summary>
    public static SplitSig SplitSignature(string hex)
    {
        byte[] bytes = AbiParamEncoder.ToBytes(hex);

        if (bytes.Length == 64)
        {
            byte[] r = bytes[..32];
            byte[] yParityAndS = bytes[32..64];
            int recoveryParam = yParityAndS[0] >> 7;
            byte[] s = (byte[])yParityAndS.Clone();
            s[0] &= 0x7f;
            return new SplitSig(r, s, recoveryParam);
        }

        if (bytes.Length == 65)
        {
            byte[] r = bytes[..32];
            byte[] s = bytes[32..64];
            int v = bytes[64];
            if (v < 27)
            {
                if (v is 0 or 1)
                {
                    v += 27;
                }
                else
                {
                    throw new ArgumentException($"signature invalid v byte: {v}");
                }
            }
            int recoveryParam = 1 - (v % 2);
            return new SplitSig(r, s, recoveryParam);
        }

        throw new ArgumentException($"invalid signature length: {bytes.Length}");
    }

    /// <summary>Reassemble a canonical 65-byte signature (r + s + v, v ∈ {0x1b, 0x1c}).</summary>
    public static string JoinSignature(SplitSig sig)
    {
        byte vByte = sig.RecoveryParam != 0 ? (byte)0x1c : (byte)0x1b;
        return "0x" + Convert.ToHexStringLower(sig.R) + Convert.ToHexStringLower(sig.S) + vByte.ToString("x2");
    }
}
