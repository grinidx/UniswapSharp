using System.Text;
using UniswapSharp.Tamperproof.Constants;

namespace UniswapSharp.Tamperproof.Utils;

/// <summary>
/// Hex/base64 helpers. Port of <c>tamperproof-transactions/src/utils/hex.ts</c>.
/// </summary>
public static class Hex
{
    /// <summary>
    /// Strips an optional leading <c>0x</c>/<c>0X</c> prefix and all whitespace, mirroring the
    /// upstream <c>input.replace(/^0x/i, "").replace(/\s/g, "")</c> ordering.
    /// </summary>
    private static string Clean(string input)
    {
        int start = 0;
        if (input.Length >= 2 && input[0] == '0' && (input[1] == 'x' || input[1] == 'X'))
        {
            start = 2;
        }

        var sb = new StringBuilder(input.Length - start);
        for (int i = start; i < input.Length; i++)
        {
            char c = input[i];
            if (!char.IsWhiteSpace(c))
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    private static bool IsHexDigits(string s)
    {
        foreach (char c in s)
        {
            bool ok = c is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F');
            if (!ok)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Decodes a hex string into bytes.
    ///
    /// <para>Accepts input with or without a <c>0x</c> prefix; ignores whitespace. Requires even
    /// length and only <c>[0-9a-fA-F]</c> characters; throws on invalid length or characters.</para>
    /// </summary>
    public static byte[] FromHex(string hex)
    {
        string cleanHex = Clean(hex);
        if (cleanHex.Length % 2 != 0)
        {
            throw new TamperproofException(Errors.InvalidHexLengthEven);
        }

        if (!IsHexDigits(cleanHex))
        {
            throw new TamperproofException(Errors.InvalidHexString(cleanHex));
        }

        var outBytes = new byte[cleanHex.Length / 2];
        for (int i = 0; i < cleanHex.Length; i += 2)
        {
            outBytes[i / 2] = (byte)(HexNibble(cleanHex[i]) << 4 | HexNibble(cleanHex[i + 1]));
        }

        return outBytes;
    }

    private static int HexNibble(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        _ => c - 'A' + 10,
    };

    /// <summary>
    /// Encodes bytes as a lowercase hex string (no <c>0x</c> prefix).
    /// </summary>
    public static string ToHex(byte[] buffer) => Convert.ToHexStringLower(buffer);

    /// <summary>
    /// Normalizes a hex string to a canonical form: strips optional <c>0x</c> and whitespace, pads a
    /// leading <c>0</c> when odd-length, validates by round-tripping, and returns lowercase hex with an
    /// optional <c>0x</c> prefix when <paramref name="with0x"/> is <c>true</c>.
    /// </summary>
    public static string NormalizeHex(string input, bool with0x = true)
    {
        string cleaned = Clean(input);
        string padded = cleaned.Length % 2 == 1 ? "0" + cleaned : cleaned;
        byte[] bytes = FromHex(padded);
        string hex = ToHex(bytes);
        return with0x ? "0x" + hex : hex;
    }

    /// <summary>
    /// Decodes a base64 string into bytes (ignoring whitespace).
    ///
    /// <para>Divergence: upstream branches on <c>atob</c> (browser) vs Node <c>Buffer</c> — a
    /// JS-environment shim with no .NET analog. .NET always has <see cref="Convert.FromBase64String"/>,
    /// so the branch and its <c>ERROR_NO_BASE64_DECODER</c> path are intentionally omitted.</para>
    /// </summary>
    public static byte[] FromBase64(string base64)
    {
        var sb = new StringBuilder(base64.Length);
        foreach (char c in base64)
        {
            if (!char.IsWhiteSpace(c))
            {
                sb.Append(c);
            }
        }

        return Convert.FromBase64String(sb.ToString());
    }
}
