using System.Text;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace UniswapSharp.Core.Utils;

public static class ZksyncAddressComputer
{
    public static string ComputeZksyncCreate2Address(
        string sender,
        string bytecodeHash,
        string salt,
        string input = "0x")
    {
        var sha3Keccack = new Sha3Keccack();

        var prefix = sha3Keccack.CalculateHash(Encoding.UTF8.GetBytes("zksyncCreate2"));
        var inputHash = sha3Keccack.CalculateHash(input.HexToByteArray());

        var concatenated = ConcatByteArrays(
            prefix,
            sender.HexToByteArray().PadLeft(32),
            salt.HexToByteArray(),
            bytecodeHash.HexToByteArray(),
            inputHash
        );

        // Upstream keeps `keccak256(...).slice(26)` — i.e. the hex string from char 26 onward,
        // which is the last 20 bytes (bytes 12..31) of the 32-byte hash, the standard address.
        var addressBytes = sha3Keccack.CalculateHash(concatenated).Skip(12).Take(20).ToArray();
        return new AddressUtil().ConvertToChecksumAddress(addressBytes);
    }

    private static byte[] ConcatByteArrays(params byte[][] arrays)
    {
        var result = new byte[arrays.Sum(a => a.Length)];
        var offset = 0;
        foreach (var array in arrays)
        {
            Buffer.BlockCopy(array, 0, result, offset, array.Length);
            offset += array.Length;
        }
        return result;
    }

    private static byte[] PadLeft(this byte[] array, int length)
    {
        if (array.Length >= length)
            return array;

        var result = new byte[length];
        Buffer.BlockCopy(array, 0, result, length - array.Length, array.Length);
        return result;
    }
}
