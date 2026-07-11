using System.Numerics;
using System.Text;
using Nethereum.Util;

namespace UniswapSharp.Permit2;

/// <summary>One EIP-712 struct field: its <c>name</c> and ABI <c>type</c> (e.g. <c>uint256</c>, <c>TokenPermissions[]</c>).</summary>
public readonly record struct TypedDataField(string Name, string Type);

/// <summary>
/// A minimal, self-contained port of ethers' <c>@ethersproject/hash</c> <c>_TypedDataEncoder</c>: computes the
/// EIP-712 signing hash (<c>keccak256(0x1901 ‖ domainSeparator ‖ hashStruct(primaryType, message))</c>)
/// byte-for-byte, so the permit2-sdk hashes match the reference to the digit.
/// <para>
/// It reproduces ethers exactly: the primary type is the one not referenced by any other type; the canonical
/// type string is <c>Type(field …)</c> followed by every transitively-referenced struct definition sorted
/// alphabetically; the domain type is <c>EIP712Domain(…)</c> built from the present domain fields in the fixed
/// order <c>name, version, chainId, verifyingContract, salt</c>.
/// </para>
/// <para>
/// Values are supplied as a nested object graph: a struct is an
/// <see cref="T:System.Collections.Generic.IReadOnlyDictionary{System.String,System.Object}"/> keyed by field
/// name; an array is any enumerable of element values; numbers are <see cref="BigInteger"/> (or an integral /
/// decimal / <c>0x</c>-hex string); <c>address</c>/<c>bytes</c> are hex strings.
/// </para>
/// </summary>
public static class Eip712TypedDataEncoder
{
    /// <summary>Returns the raw 32-byte EIP-712 digest for the given domain, type set and message.</summary>
    public static byte[] Hash(
        Eip712Domain domain,
        IReadOnlyDictionary<string, IReadOnlyList<TypedDataField>> types,
        IReadOnlyDictionary<string, object?> message)
    {
        var encoder = new StructEncoder(types);
        byte[] domainSeparator = HashDomain(domain);
        byte[] structHash = encoder.HashStruct(encoder.PrimaryType, message);
        return Keccak(Concat(new byte[] { 0x19, 0x01 }, domainSeparator, structHash));
    }

    /// <summary>Returns the EIP-712 digest as a lower-case <c>0x</c>-prefixed hex string.</summary>
    public static string HashHex(
        Eip712Domain domain,
        IReadOnlyDictionary<string, IReadOnlyList<TypedDataField>> types,
        IReadOnlyDictionary<string, object?> message)
    {
        return "0x" + ToHex(Hash(domain, types, message));
    }

    /// <summary>Computes the EIP-712 domain separator for <paramref name="domain"/>.</summary>
    internal static byte[] HashDomain(Eip712Domain domain)
    {
        // ethers' fixed field order and types for EIP712Domain; only present fields are included.
        var fieldTypes = new (string Name, string Type)[]
        {
            ("name", "string"),
            ("version", "string"),
            ("chainId", "uint256"),
            ("verifyingContract", "address"),
            ("salt", "bytes32"),
        };

        var values = domain.ToMessage();
        var fields = fieldTypes
            .Where(ft => values.ContainsKey(ft.Name))
            .Select(ft => new TypedDataField(ft.Name, ft.Type))
            .ToList();

        var types = new Dictionary<string, IReadOnlyList<TypedDataField>>
        {
            ["EIP712Domain"] = fields,
        };
        return new StructEncoder(types).HashStruct("EIP712Domain", values);
    }

    /// <summary>Builds the canonical encoders (primary type, type strings) for a set of related EIP-712 types.</summary>
    private sealed class StructEncoder
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyList<TypedDataField>> _types;
        private readonly Dictionary<string, string> _encodedTypes = new();

        public string PrimaryType { get; }

        public StructEncoder(IReadOnlyDictionary<string, IReadOnlyList<TypedDataField>> types)
        {
            _types = types;

            var parents = types.Keys.ToDictionary(k => k, _ => new List<string>());
            var links = types.Keys.ToDictionary(k => k, _ => new HashSet<string>());

            foreach (var (name, fields) in types)
            {
                var seen = new HashSet<string>();
                foreach (var field in fields)
                {
                    if (!seen.Add(field.Name))
                    {
                        throw new ArgumentException($"duplicate variable name {field.Name} in {name}");
                    }

                    string baseType = BaseType(field.Type);
                    if (baseType == name)
                    {
                        throw new ArgumentException($"circular type reference to {baseType}");
                    }

                    if (!types.ContainsKey(baseType))
                    {
                        continue; // atomic type, not a struct
                    }

                    parents[baseType].Add(name);
                    links[name].Add(baseType);
                }
            }

            var primaryTypes = parents.Where(p => p.Value.Count == 0).Select(p => p.Key).ToList();
            if (primaryTypes.Count == 0)
            {
                throw new ArgumentException("missing primary type");
            }
            if (primaryTypes.Count > 1)
            {
                throw new ArgumentException("ambiguous primary types or unused types: " + string.Join(", ", primaryTypes));
            }
            PrimaryType = primaryTypes[0];

            var subtypes = types.Keys.ToDictionary(k => k, _ => new HashSet<string>());
            CheckCircular(PrimaryType, new HashSet<string>(), links, subtypes);

            foreach (var name in types.Keys)
            {
                var ordered = subtypes[name].ToList();
                ordered.Sort(StringComparer.Ordinal);
                _encodedTypes[name] = Definition(name) + string.Concat(ordered.Select(Definition));
            }
        }

        public byte[] HashStruct(string type, IReadOnlyDictionary<string, object?> value) => Keccak(EncodeData(type, value));

        private byte[] EncodeData(string type, IReadOnlyDictionary<string, object?> value)
        {
            var parts = new List<byte[]> { TypeHash(type) };
            foreach (var field in _types[type])
            {
                parts.Add(EncodeField(field.Type, value[field.Name]));
            }
            return Concat(parts.ToArray());
        }

        private byte[] EncodeField(string type, object? value)
        {
            if (_types.ContainsKey(type))
            {
                // Nested struct: hash its encoded data.
                return Keccak(EncodeData(type, AsDictionary(value)));
            }

            if (type.EndsWith(']'))
            {
                string elementType = type[..type.LastIndexOf('[')];
                var encoded = AsList(value).Select(item => EncodeField(elementType, item));
                return Keccak(Concat(encoded.ToArray()));
            }

            return EncodeAtomic(type, value);
        }

        private byte[] TypeHash(string type) => Keccak(Encoding.UTF8.GetBytes(_encodedTypes[type]));

        private string Definition(string name)
        {
            var fields = _types[name].Select(f => $"{f.Type} {f.Name}");
            return $"{name}({string.Join(",", fields)})";
        }

        private static void CheckCircular(
            string type,
            HashSet<string> found,
            IReadOnlyDictionary<string, HashSet<string>> links,
            IReadOnlyDictionary<string, HashSet<string>> subtypes)
        {
            if (!found.Add(type))
            {
                throw new ArgumentException($"circular type reference to {type}");
            }

            foreach (var child in links[type])
            {
                if (!subtypes.ContainsKey(child))
                {
                    continue;
                }

                CheckCircular(child, found, links, subtypes);
                foreach (var ancestor in found)
                {
                    subtypes[ancestor].Add(child);
                }
            }

            found.Remove(type);
        }
    }

    private static string BaseType(string type)
    {
        int bracket = type.IndexOf('[');
        return bracket < 0 ? type : type[..bracket];
    }

    private static byte[] EncodeAtomic(string type, object? value)
    {
        switch (type)
        {
            case "address":
                return LeftPad(ToBytes(value));
            case "bool":
                return EncodeWord(ToBigInteger(value) != BigInteger.Zero ? BigInteger.One : BigInteger.Zero);
            case "string":
                return Keccak(Encoding.UTF8.GetBytes((string)value!));
            case "bytes":
                return Keccak(ToBytes(value));
        }

        if (type.StartsWith("uint") || type.StartsWith("int"))
        {
            return EncodeWord(ToBigInteger(value));
        }

        if (type.StartsWith("bytes"))
        {
            return RightPad(ToBytes(value)); // fixed bytesN
        }

        throw new ArgumentException($"unsupported EIP-712 atomic type: {type}");
    }

    private static IReadOnlyDictionary<string, object?> AsDictionary(object? value) =>
        value as IReadOnlyDictionary<string, object?>
        ?? throw new ArgumentException("expected a struct value (IReadOnlyDictionary<string, object?>)");

    private static IReadOnlyList<object?> AsList(object? value)
    {
        if (value is IEnumerable<object?> typed)
        {
            return typed.ToList();
        }
        if (value is System.Collections.IEnumerable enumerable and not string)
        {
            var list = new List<object?>();
            foreach (var item in enumerable)
            {
                list.Add(item);
            }
            return list;
        }
        throw new ArgumentException("expected an array value");
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
        string str => ParseNumber(str),
        null => throw new ArgumentNullException(nameof(value), "numeric value cannot be null"),
        _ => throw new ArgumentException($"cannot convert {value.GetType().Name} to a numeric value"),
    };

    private static BigInteger ParseNumber(string s)
    {
        s = s.Trim();
        if (s.StartsWith("0x") || s.StartsWith("0X"))
        {
            byte[] bytes = ToBytes(s);
            return new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
        }
        return BigInteger.Parse(s);
    }

    private static byte[] ToBytes(object? value)
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
                throw new ArgumentException($"cannot convert {value?.GetType().Name ?? "null"} to bytes");
        }
    }

    /// <summary>Encodes an integer into a 32-byte big-endian word (two's complement for negatives).</summary>
    private static byte[] EncodeWord(BigInteger value)
    {
        if (value.Sign < 0)
        {
            value += BigInteger.One << 256;
        }
        return LeftPad(value.ToByteArray(isUnsigned: true, isBigEndian: true));
    }

    private static byte[] LeftPad(byte[] bytes)
    {
        var word = new byte[32];
        Array.Copy(bytes, 0, word, 32 - bytes.Length, bytes.Length);
        return word;
    }

    private static byte[] RightPad(byte[] bytes)
    {
        var word = new byte[32];
        Array.Copy(bytes, 0, word, 0, bytes.Length);
        return word;
    }

    private static byte[] Keccak(byte[] data) => Sha3Keccack.Current.CalculateHash(data);

    private static byte[] Concat(params byte[][] arrays)
    {
        int length = arrays.Sum(a => a.Length);
        var result = new byte[length];
        int offset = 0;
        foreach (var array in arrays)
        {
            Array.Copy(array, 0, result, offset, array.Length);
            offset += array.Length;
        }
        return result;
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
