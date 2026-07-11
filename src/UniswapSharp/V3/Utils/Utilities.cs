using System.Numerics;

namespace UniswapSharp.V3.Utils;

public static class Utilities
{
    /// <summary>
    /// Converts a big int to a hex string
    /// </summary>
    /// <param name="bigintIsh">The BigInteger to convert</param>
    /// <returns>The hex encoded calldata</returns>
    public static string ToHex(BigInteger bigintIsh)
    {
        // Upstream toHex uses bigInt.toString(16) — the minimal lowercase hex.
        // BigInteger.ToString("X") sign-extends: a non-negative value whose top nibble
        // is >= 8 gets a leading "0" (so it isn't read as negative), e.g. 200 -> "0C8".
        // Strip those leading zeros to match JS's minimal form before even-length padding,
        // otherwise the value gains a spurious leading 00 byte.
        string hex = bigintIsh.ToString("X").ToLowerInvariant().TrimStart('0');
        if (hex.Length == 0)
        {
            hex = "0";
        }
        if (hex.Length % 2 != 0)
        {
            hex = $"0{hex}";
        }
        return $"0x{hex}";
    }
}
