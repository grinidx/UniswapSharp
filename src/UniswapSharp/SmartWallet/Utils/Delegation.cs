namespace UniswapSharp.SmartWallet.Utils;

/// <summary>
/// EIP-7702 delegation helpers. Port of <c>smart-wallet-sdk/src/utils/delegation.ts</c>.
/// <para>
/// Only the pure <see cref="ParseFromCode"/> is ported. The upstream
/// <c>parseAuthorizationList</c> / <c>parseAuthorizationListFromTransaction</c> helpers recover
/// signers from a signed authorization list via viem's experimental
/// <c>recoverAuthorizationAddress</c> (ECDSA signature recovery over EIP-7702 authorizations);
/// they are omitted as network/crypto surface outside the scope of the calldata port.
/// </para>
/// </summary>
public static class Delegation
{
    /// <summary>
    /// Parses a delegation from an address's code. Throws if the code is not a valid EIP-7702
    /// delegation designator (a <c>0xef0100</c> magic prefix followed by a 20-byte address).
    /// </summary>
    /// <param name="code">The account code, e.g. <c>0xef0100&lt;address&gt;</c> (48 hex chars incl. <c>0x</c>).</param>
    /// <returns>The delegated-to address.</returns>
    public static string ParseFromCode(string code)
    {
        if (code.Length != 48)
        {
            throw new ArgumentException($"Invalid delegation length: {code.Length}");
        }

        // Parse out the magic prefix, which is "0x" + 4 bytes worth of hex (8 chars).
        string magicPrefix = code[..8];
        if (magicPrefix != Constants.DELEGATION_MAGIC_PREFIX)
        {
            throw new ArgumentException($"Invalid delegation magic prefix: {magicPrefix}");
        }

        string delegation = code[8..];
        return "0x" + delegation;
    }
}
