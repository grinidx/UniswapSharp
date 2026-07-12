using System.Globalization;
using System.Text;
using UniswapSharp.Tamperproof.Constants;

namespace UniswapSharp.Tamperproof.Utils;

/// <summary>
/// DNS TXT record helpers. Port of <c>tamperproof-transactions/src/utils/txtRecord.ts</c>.
/// </summary>
public static class TxtRecord
{
    /// <summary>
    /// Decodes DNS TXT record wire-format bytes (RFC 1035) into a UTF-8 string. TXT RDATA is a
    /// sequence of length-prefixed character strings; this iterates the chunks, validates boundaries,
    /// decodes each as UTF-8, and concatenates them. Throws
    /// <see cref="Errors.InvalidTxtRecordFormat"/> if the data is truncated or malformed.
    /// </summary>
    public static string ParseTxtRecord(byte[] buffer)
    {
        var result = new StringBuilder();
        int offset = 0;

        while (offset < buffer.Length)
        {
            int length = buffer[offset];
            offset += 1;

            if (offset + length > buffer.Length)
            {
                throw new TamperproofException(Errors.InvalidTxtRecordFormat);
            }

            result.Append(Encoding.UTF8.GetString(buffer, offset, length));
            offset += length;
        }

        return result.ToString();
    }

    /// <summary>
    /// Normalizes DNS TXT record data into a string: a string is returned as-is; a byte array is
    /// decoded via <see cref="ParseTxtRecord"/>; anything else falls back to its string form. Used by
    /// verification code that consumes DoH responses (TXT answers may be raw wire-format bytes or plain
    /// strings).
    /// </summary>
    public static string ProcessTxtRecordData(object? data)
    {
        if (data is string s)
        {
            return s;
        }

        if (data is byte[] bytes)
        {
            return ParseTxtRecord(bytes);
        }

        // Fallback for other types (mirrors JS String(data)).
        return Convert.ToString(data, CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
