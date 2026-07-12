using System.Collections;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace UniswapSharp.Tamperproof.Utils;

/// <summary>
/// Deterministic (canonical) JSON serialization. Port of
/// <c>tamperproof-transactions/src/utils/canonicalJson.ts</c>.
///
/// <para>Objects are modelled as <c>IEnumerable&lt;KeyValuePair&lt;string, object?&gt;&gt;</c> (e.g. a
/// <see cref="System.Collections.Generic.Dictionary{TKey, TValue}"/>), arrays as any other
/// <see cref="IEnumerable"/>, and the JS <c>undefined</c> value as <see cref="Undefined"/> so it can be
/// dropped from objects exactly as upstream does.</para>
/// </summary>
public static class CanonicalJson
{
    /// <summary>
    /// Sentinel for the JS <c>undefined</c> value: object properties whose value is this marker are
    /// dropped during serialization (distinct from <c>null</c>, which is preserved).
    /// </summary>
    public static readonly object Undefined = new UndefinedMarker();

    private sealed class UndefinedMarker
    {
    }

    /// <summary>
    /// Canonicalizes a value and serializes it: object keys sorted lexicographically, <c>undefined</c>
    /// properties dropped, arrays kept in order, primitives left as-is.
    /// </summary>
    public static string CanonicalStringify(object? value)
    {
        var sb = new StringBuilder();
        Write(sb, value, sort: true);
        return sb.ToString();
    }

    /// <summary>
    /// Serializes the canonical JSON of <paramref name="requestPayload"/> as deterministic UTF-8 bytes,
    /// used to produce stable byte sequences for signing/verification.
    /// </summary>
    public static byte[] SerializeRequestPayload(object? requestPayload) =>
        Encoding.UTF8.GetBytes(CanonicalStringify(requestPayload));

    /// <summary>
    /// Plain (insertion-order) JSON serialization, mirroring JS <c>JSON.stringify</c> without key
    /// sorting. Undefined object properties are still dropped. Used by <c>generate</c>.
    /// </summary>
    internal static string Stringify(object? value)
    {
        var sb = new StringBuilder();
        Write(sb, value, sort: false);
        return sb.ToString();
    }

    private static void Write(StringBuilder sb, object? value, bool sort)
    {
        switch (value)
        {
            case null:
                sb.Append("null");
                return;
            case bool b:
                sb.Append(b ? "true" : "false");
                return;
            case string s:
                WriteString(sb, s);
                return;
            case UndefinedMarker:
                // JSON.stringify(undefined) is undefined; in arrays it becomes null. Top-level use is
                // not exercised, but treat as null to stay defined.
                sb.Append("null");
                return;
            case IEnumerable<KeyValuePair<string, object?>> obj:
                WriteObject(sb, obj, sort);
                return;
            case IEnumerable en:
                WriteArray(sb, en, sort);
                return;
            default:
                WriteNumber(sb, value);
                return;
        }
    }

    private static void WriteObject(StringBuilder sb, IEnumerable<KeyValuePair<string, object?>> obj, bool sort)
    {
        var pairs = new List<KeyValuePair<string, object?>>();
        foreach (var kv in obj)
        {
            if (ReferenceEquals(kv.Value, Undefined))
            {
                continue; // drop undefined values
            }

            pairs.Add(kv);
        }

        if (sort)
        {
            pairs.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
        }

        sb.Append('{');
        for (int i = 0; i < pairs.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            WriteString(sb, pairs[i].Key);
            sb.Append(':');
            Write(sb, pairs[i].Value, sort);
        }

        sb.Append('}');
    }

    private static void WriteArray(StringBuilder sb, IEnumerable en, bool sort)
    {
        sb.Append('[');
        bool first = true;
        foreach (object? item in en)
        {
            if (!first)
            {
                sb.Append(',');
            }

            // JSON.stringify turns undefined array elements into null.
            Write(sb, ReferenceEquals(item, Undefined) ? null : item, sort);
            first = false;
        }

        sb.Append(']');
    }

    private static void WriteNumber(StringBuilder sb, object value)
    {
        switch (value)
        {
            case byte or sbyte or short or ushort or int or uint or long or ulong or BigInteger:
                sb.Append(((IFormattable)value).ToString(null, CultureInfo.InvariantCulture));
                return;
            case double d:
                sb.Append(double.IsFinite(d) ? d.ToString("R", CultureInfo.InvariantCulture) : "null");
                return;
            case float f:
                sb.Append(float.IsFinite(f) ? f.ToString("R", CultureInfo.InvariantCulture) : "null");
                return;
            case decimal m:
                sb.Append(m.ToString(CultureInfo.InvariantCulture));
                return;
            default:
                // Fallback: use the value's string form (should not be reached for JSON-like inputs).
                sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
        }
    }

    private static void WriteString(StringBuilder sb, string s)
    {
        sb.Append('"');
        foreach (char c in s)
        {
            switch (c)
            {
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '\b':
                    sb.Append("\\b");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\f':
                    sb.Append("\\f");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                default:
                    if (c < 0x20)
                    {
                        sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(c);
                    }

                    break;
            }
        }

        sb.Append('"');
    }
}
