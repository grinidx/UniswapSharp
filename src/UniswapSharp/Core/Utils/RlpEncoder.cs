using Nethereum.Hex.HexConvertors.Extensions;

namespace UniswapSharp.Core.Utils;

public static class RlpEncoder
{
    private static byte[] ArrayifyInteger(int value)
    {
        var result = new List<byte>();
        while (value != 0)
        {
            result.Insert(0, (byte)(value & 0xff));
            value >>= 8;
        }
        return result.ToArray();
    }

    public static byte[] Encode(object obj)
    {
        if (obj is IEnumerable<object> enumerable)
        {
            var payload = new List<byte>();
            foreach (var child in enumerable)
            {
                payload.AddRange(Encode(child));
            }

            if (payload.Count <= 55)
            {
                payload.Insert(0, (byte)(0xc0 + payload.Count));
                return payload.ToArray();
            }

            var length = ArrayifyInteger(payload.Count);
            var lengthPrefix = new byte[] { (byte)(0xf7 + length.Length) };
            return lengthPrefix.Concat(length).Concat(payload).ToArray();
        }

        byte[] data;
        if (obj is string str)
            data = str.HexToByteArray();
        else if (obj is byte[] bytes)
            data = bytes;
        else
            throw new ArgumentException("Unsupported object type for RLP encoding");

        if (data.Length == 1 && data[0] <= 0x7f)
        {
            return data;
        }
        else if (data.Length <= 55)
        {
            return new byte[] { (byte)(0x80 + data.Length) }.Concat(data).ToArray();
        }

        var dataLength = ArrayifyInteger(data.Length);
        var dataLengthPrefix = new byte[] { (byte)(0xb7 + dataLength.Length) };
        return dataLengthPrefix.Concat(dataLength).Concat(data).ToArray();
    }

    public static string EncodeRlp(object obj)
    {
        var encoded = Encode(obj);
        return encoded.ToHex(true);
    }
}