using System.Collections;
using System.Numerics;
using System.Text;

namespace UniswapSharp.V4.Utils;

/// <summary>
/// A minimal, self-contained equivalent of ethers' <c>defaultAbiCoder</c>: raw ABI encoding and
/// decoding (no function selector) of arbitrary parameter lists, including nested tuples
/// (structs), arrays-of-tuples (<c>(...)[]</c>), dynamic <c>bytes</c>, and dynamic value arrays
/// such as <c>uint256[]</c> / <c>bytes[]</c>.
/// <para>
/// The V4 planner encodes its per-action inputs with ethers type strings that Nethereum's simple
/// type-string API cannot parse (bare and named tuples such as
/// <c>(address currency0,...)</c> or <c>(...)[]</c>), so this implements the standard head/tail
/// ABI layout directly over <see cref="BigInteger"/> to guarantee byte-for-byte parity with the
/// upstream <c>ethers/lib/utils.defaultAbiCoder</c>.
/// </para>
/// </summary>
public static class AbiParamEncoder
{
    /// <summary>
    /// Encodes <paramref name="values"/> according to the ethers type strings in
    /// <paramref name="types"/>. Values are supplied positionally in ABI order:
    /// numbers as <see cref="BigInteger"/> (or any integral / numeric string), <c>address</c> as a
    /// hex string, <c>bool</c> as <see cref="bool"/>, <c>bytes</c> as a <c>0x</c> hex string or
    /// <see cref="T:byte[]"/>, tuples as <see cref="T:object[]"/> of their components in order, and
    /// arrays as any enumerable of their element values.
    /// </summary>
    /// <returns>The ABI encoding as a lower-case <c>0x</c>-prefixed hex string.</returns>
    public static string Encode(string[] types, object?[] values)
    {
        var parsed = types.Select(AbiTypeParser.Parse).ToList();
        byte[] encoded = EncodeHeadTail(parsed, values.ToList());
        return "0x" + ToHex(encoded);
    }

    private static byte[] EncodeHeadTail(IReadOnlyList<AbiType> types, IReadOnlyList<object?> values)
    {
        int headSize = types.Sum(t => t.IsDynamic ? 32 : t.StaticSize);
        var heads = new byte[types.Count][];
        var tails = new byte[types.Count][];

        for (int i = 0; i < types.Count; i++)
        {
            if (types[i].IsDynamic)
            {
                tails[i] = EncodeValue(types[i], values[i]);
            }
            else
            {
                heads[i] = EncodeValue(types[i], values[i]);
            }
        }

        // Fill in the dynamic-element offsets now that the tail lengths are known.
        int running = headSize;
        for (int i = 0; i < types.Count; i++)
        {
            if (types[i].IsDynamic)
            {
                heads[i] = EncodeWord(running);
                running += tails[i].Length;
            }
        }

        using var ms = new MemoryStream();
        foreach (var head in heads)
        {
            ms.Write(head);
        }
        for (int i = 0; i < types.Count; i++)
        {
            if (tails[i] != null)
            {
                ms.Write(tails[i]);
            }
        }
        return ms.ToArray();
    }

    private static byte[] EncodeValue(AbiType type, object? value)
    {
        switch (type)
        {
            case NumberType:
                return EncodeWord(ToBigInteger(value));
            case AddressType:
                return LeftPad(ToBytes(value));
            case BoolType:
                return EncodeWord((bool)value! ? BigInteger.One : BigInteger.Zero);
            case BytesType:
                return EncodeBytes(ToBytes(value));
            case FixedBytesType fb:
                {
                    byte[] b = ToBytes(value);
                    var word = new byte[32];
                    Array.Copy(b, 0, word, 0, Math.Min(b.Length, fb.Size));
                    return word;
                }
            case TupleType tuple:
                return EncodeHeadTail(tuple.Components, ToList(value));
            case ArrayType array:
                {
                    var items = ToList(value);
                    var elementTypes = Enumerable.Repeat(array.Element, items.Count).ToList();
                    byte[] body = EncodeHeadTail(elementTypes, items);
                    if (array.Length is null)
                    {
                        using var ms = new MemoryStream();
                        ms.Write(EncodeWord(items.Count));
                        ms.Write(body);
                        return ms.ToArray();
                    }
                    return body;
                }
            default:
                throw new ArgumentException($"Unsupported ABI type: {type.GetType().Name}");
        }
    }

    private static byte[] EncodeBytes(byte[] data)
    {
        int padded = (data.Length + 31) / 32 * 32;
        var buffer = new byte[32 + padded];
        Array.Copy(EncodeWord(data.Length), 0, buffer, 0, 32);
        Array.Copy(data, 0, buffer, 32, data.Length);
        return buffer;
    }

    /// <summary>Encodes an integer into a 32-byte big-endian word (two's complement for negatives).</summary>
    private static byte[] EncodeWord(BigInteger value)
    {
        if (value.Sign < 0)
        {
            value += BigInteger.One << 256;
        }
        byte[] raw = value.ToByteArray(isUnsigned: true, isBigEndian: true);
        return LeftPad(raw);
    }

    private static byte[] LeftPad(byte[] bytes)
    {
        var word = new byte[32];
        Array.Copy(bytes, 0, word, 32 - bytes.Length, bytes.Length);
        return word;
    }

    internal static BigInteger ToBigInteger(object? value) => value switch
    {
        BigInteger b => b,
        int i => i,
        long l => l,
        uint u => u,
        ulong ul => ul,
        short s => s,
        byte by => by,
        bool bo => bo ? BigInteger.One : BigInteger.Zero,
        // BigNumberish: accept 0x-prefixed hex as well as decimal strings
        string str => str.StartsWith("0x") || str.StartsWith("0X")
            ? BigInteger.Parse("0" + str[2..], System.Globalization.NumberStyles.HexNumber)
            : BigInteger.Parse(str),
        null => throw new ArgumentNullException(nameof(value), "Numeric ABI value cannot be null"),
        _ => throw new ArgumentException($"Cannot convert {value.GetType().Name} to a numeric ABI value"),
    };

    internal static byte[] ToBytes(object? value)
    {
        switch (value)
        {
            case byte[] bytes:
                return bytes;
            case string s:
                {
                    string hex = s.StartsWith("0x") || s.StartsWith("0X") ? s[2..] : s;
                    if (hex.Length % 2 != 0)
                    {
                        hex = "0" + hex;
                    }
                    var result = new byte[hex.Length / 2];
                    for (int i = 0; i < result.Length; i++)
                    {
                        result[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
                    }
                    return result;
                }
            default:
                throw new ArgumentException($"Cannot convert {value?.GetType().Name ?? "null"} to bytes");
        }
    }

    private static List<object?> ToList(object? value)
    {
        if (value is object?[] array)
        {
            return array.ToList();
        }
        if (value is IEnumerable enumerable and not string)
        {
            var list = new List<object?>();
            foreach (var item in enumerable)
            {
                list.Add(item);
            }
            return list;
        }
        throw new ArgumentException($"Expected a tuple/array value but got {value?.GetType().Name ?? "null"}");
    }

    private static string ToHex(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (byte b in bytes)
        {
            sb.Append(b.ToString("x2"));
        }
        return sb.ToString();
    }
}

/// <summary>
/// The decoding counterpart of <see cref="AbiParamEncoder"/>. Decodes an ABI blob against a list of
/// ethers type strings into a positional object graph: numbers as <see cref="BigInteger"/>,
/// <c>address</c>/<c>bytes</c> as lower-case <c>0x</c> hex strings, <c>bool</c> as <see cref="bool"/>,
/// tuples and arrays as <see cref="T:System.Collections.Generic.List{System.Object}"/>.
/// </summary>
public static class AbiParamDecoder
{
    /// <summary>Decodes <paramref name="hex"/> against <paramref name="types"/> into positional values.</summary>
    public static List<object?> Decode(string[] types, string hex)
    {
        var parsed = types.Select(AbiTypeParser.Parse).ToList();
        byte[] data = AbiParamEncoder.ToBytes(hex);
        return DecodeHeadTail(parsed, data, 0);
    }

    private static List<object?> DecodeHeadTail(IReadOnlyList<AbiType> types, byte[] data, int baseOffset)
    {
        var results = new List<object?>();
        int headPos = baseOffset;
        foreach (var type in types)
        {
            if (type.IsDynamic)
            {
                int offset = (int)ReadUint(data, headPos);
                results.Add(DecodeValue(type, data, baseOffset + offset));
                headPos += 32;
            }
            else
            {
                results.Add(DecodeValue(type, data, headPos));
                headPos += type.StaticSize;
            }
        }
        return results;
    }

    private static object? DecodeValue(AbiType type, byte[] data, int pos)
    {
        switch (type)
        {
            case NumberType n:
                return ReadInt(data, pos, n.Signed);
            case AddressType:
                {
                    var b = new byte[20];
                    Array.Copy(data, pos + 12, b, 0, 20);
                    return "0x" + ToHex(b);
                }
            case BoolType:
                return ReadUint(data, pos) != BigInteger.Zero;
            case BytesType:
                {
                    int len = (int)ReadUint(data, pos);
                    var b = new byte[len];
                    Array.Copy(data, pos + 32, b, 0, len);
                    return "0x" + ToHex(b);
                }
            case FixedBytesType fb:
                {
                    var b = new byte[fb.Size];
                    Array.Copy(data, pos, b, 0, fb.Size);
                    return "0x" + ToHex(b);
                }
            case TupleType tuple:
                return DecodeHeadTail(tuple.Components, data, pos);
            case ArrayType array:
                {
                    if (array.Length is null)
                    {
                        int len = (int)ReadUint(data, pos);
                        return DecodeHeadTail(Enumerable.Repeat(array.Element, len).ToList(), data, pos + 32);
                    }
                    return DecodeHeadTail(Enumerable.Repeat(array.Element, array.Length.Value).ToList(), data, pos);
                }
            default:
                throw new ArgumentException($"Unsupported ABI type: {type.GetType().Name}");
        }
    }

    private static BigInteger ReadUint(byte[] data, int pos)
    {
        var word = new byte[32];
        Array.Copy(data, pos, word, 0, 32);
        return new BigInteger(word, isUnsigned: true, isBigEndian: true);
    }

    private static BigInteger ReadInt(byte[] data, int pos, bool signed)
    {
        var value = ReadUint(data, pos);
        if (signed && (data[pos] & 0x80) != 0)
        {
            value -= BigInteger.One << 256;
        }
        return value;
    }

    private static string ToHex(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (byte b in bytes)
        {
            sb.Append(b.ToString("x2"));
        }
        return sb.ToString();
    }
}

/// <summary>Base of the small ABI type model used by <see cref="AbiParamEncoder"/>/<see cref="AbiParamDecoder"/>.</summary>
internal abstract class AbiType
{
    public abstract bool IsDynamic { get; }

    /// <summary>Size, in bytes, that a value of this (static) type occupies in the head region.</summary>
    public virtual int StaticSize => 32;
}

internal sealed class NumberType : AbiType
{
    public required bool Signed { get; init; }
    public override bool IsDynamic => false;
}

internal sealed class AddressType : AbiType
{
    public override bool IsDynamic => false;
}

internal sealed class BoolType : AbiType
{
    public override bool IsDynamic => false;
}

internal sealed class BytesType : AbiType
{
    public override bool IsDynamic => true;
}

internal sealed class FixedBytesType : AbiType
{
    public required int Size { get; init; }
    public override bool IsDynamic => false;
}

internal sealed class ArrayType : AbiType
{
    public required AbiType Element { get; init; }

    /// <summary><c>null</c> for a dynamic array (<c>T[]</c>); otherwise the fixed length (<c>T[k]</c>).</summary>
    public required int? Length { get; init; }

    public override bool IsDynamic => Length is null || Element.IsDynamic;

    public override int StaticSize =>
        Length is int k ? k * Element.StaticSize : throw new InvalidOperationException("dynamic array has no static size");
}

internal sealed class TupleType : AbiType
{
    public required IReadOnlyList<AbiType> Components { get; init; }

    public override bool IsDynamic => Components.Any(c => c.IsDynamic);

    public override int StaticSize => Components.Sum(c => c.StaticSize);
}

/// <summary>Parses ethers type strings (possibly carrying field names inside tuples) into <see cref="AbiType"/>s.</summary>
internal static class AbiTypeParser
{
    public static AbiType Parse(string type)
    {
        type = type.Trim();

        if (type.EndsWith("]"))
        {
            int open = type.LastIndexOf('[');
            string inner = type.Substring(open + 1, type.Length - open - 2);
            AbiType element = Parse(type[..open]);
            return new ArrayType { Element = element, Length = inner.Length == 0 ? null : int.Parse(inner) };
        }

        // Accept the ethers "tuple(...)" spelling in addition to the bare "(...)" form.
        if (type.StartsWith("tuple("))
        {
            type = type["tuple".Length..];
        }

        if (type.StartsWith("("))
        {
            string inner = type.Substring(1, type.Length - 2);
            var components = SplitTopLevel(inner).Select(ParseComponent).ToList();
            return new TupleType { Components = components };
        }

        if (type == "address")
        {
            return new AddressType();
        }
        if (type == "bool")
        {
            return new BoolType();
        }
        if (type is "bytes" or "string")
        {
            return new BytesType();
        }
        if (type.StartsWith("bytes"))
        {
            return new FixedBytesType { Size = int.Parse(type[5..]) };
        }
        if (type.StartsWith("uint"))
        {
            return new NumberType { Signed = false };
        }
        if (type.StartsWith("int"))
        {
            return new NumberType { Signed = true };
        }
        throw new ArgumentException($"Unknown ABI type: {type}");
    }

    /// <summary>Parses one tuple component, which may be "&lt;type&gt; name" or just "&lt;type&gt;".</summary>
    private static AbiType ParseComponent(string component)
    {
        component = component.Trim();
        string typeString;
        if (component.StartsWith("("))
        {
            int close = IndexOfMatchingParen(component);
            int end = close + 1;
            while (end < component.Length && component[end] == '[')
            {
                end = component.IndexOf(']', end) + 1;
            }
            typeString = component[..end];
        }
        else
        {
            int space = component.IndexOf(' ');
            typeString = space < 0 ? component : component[..space];
        }
        return Parse(typeString);
    }

    private static List<string> SplitTopLevel(string s)
    {
        var parts = new List<string>();
        int depth = 0;
        int start = 0;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c is '(' or '[')
            {
                depth++;
            }
            else if (c is ')' or ']')
            {
                depth--;
            }
            else if (c == ',' && depth == 0)
            {
                parts.Add(s[start..i]);
                start = i + 1;
            }
        }
        if (start < s.Length)
        {
            parts.Add(s[start..]);
        }
        return parts;
    }

    private static int IndexOfMatchingParen(string s)
    {
        int depth = 0;
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '(')
            {
                depth++;
            }
            else if (s[i] == ')')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }
        throw new ArgumentException($"Unbalanced parentheses in type: {s}");
    }
}
