using System.Numerics;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace UniswapSharp.Core.Utils;

public static class AddressValidator
{
    /**
 * Validates an address and returns the parsed (checksummed) version of that address
 * @param address the unchecksummed hex address
 */
    public static string ValidateAndParseAddress(string address)
    {
        try
        {
            return GetAddress(address);
        }
        catch (Exception)
        {
            throw new ArgumentException($"{address} is not a valid address.");
        }
    }

    // Checks a string starts with 0x, is 42 characters long and contains only hex characters after 0x
    private static readonly Regex StartsWith0xLen42HexRegex = new Regex(@"^0x[0-9a-fA-F]{40}$", RegexOptions.Compiled);

    /**
 * Checks if an address is valid by checking 0x prefix, length === 42 and hex encoding.
 * @param address the unchecksummed hex address
 */
    public static string CheckValidAddress(string address)
    {
        if (StartsWith0xLen42HexRegex.IsMatch(address))
        {
            return address;
        }

        throw new ArgumentException($"{address} is not a valid address.");
    }


    /****/

    private static readonly BigInteger BN_0 = BigInteger.Zero;
    private static readonly BigInteger BN_36 = new BigInteger(36);

    private static string GetChecksumAddress(string address)
    {
        address = address.ToLowerInvariant();
        var chars = address.Substring(2).ToCharArray();

        var expanded = new byte[40];
        for (int i = 0; i < 40; i++)
        {
            expanded[i] = (byte)chars[i];
        }

        var hashed = new Sha3Keccack().CalculateHash(expanded);

        for (int i = 0; i < 40; i += 2)
        {
            if ((hashed[i >> 1] >> 4) >= 8)
            {
                chars[i] = char.ToUpperInvariant(chars[i]);
            }
            if ((hashed[i >> 1] & 0x0f) >= 8)
            {
                chars[i + 1] = char.ToUpperInvariant(chars[i + 1]);
            }
        }

        return "0x" + new string(chars);
    }

    private static readonly Dictionary<char, string> IbanLookup =
        Enumerable.Range(0, 36)
            .ToDictionary(
                i => i < 10 ? (char)('0' + i) : (char)('A' + i - 10),
                i => i.ToString()
            );

    private const int SafeDigits = 15;

    private static string IbanChecksum(string address)
    {
        address = address.ToUpperInvariant();
        address = address.Substring(4) + address.Substring(0, 2) + "00";

        var expanded = string.Join("", address.Select(c => IbanLookup[c]));

        while (expanded.Length >= SafeDigits)
        {
            var block = expanded.Substring(0, SafeDigits);
            expanded = (int.Parse(block) % 97).ToString() + expanded.Substring(block.Length);
        }

        var checksum = (98 - int.Parse(expanded) % 97).ToString("D2");
        return checksum;
    }

    private static readonly Dictionary<char, BigInteger> Base36 = "0123456789abcdefghijklmnopqrstuvwxyz"
        .Select((c, i) => (c, i))
        .ToDictionary(t => t.c, t => new BigInteger(t.i));

    private static BigInteger FromBase36(string value)
    {
        value = value.ToLowerInvariant();

        BigInteger result = BN_0;
        foreach (var c in value)
        {
            result = result * BN_36 + Base36[c];
        }
        return result;
    }

    public static string GetAddress(string address)
    {
        if (address == null)
            throw new ArgumentException("Invalid address", nameof(address));

        if (System.Text.RegularExpressions.Regex.IsMatch(address, @"^(0x)?[0-9a-fA-F]{40}$"))
        {
            if (!address.StartsWith("0x"))
                address = "0x" + address;

            var result = GetChecksumAddress(address);

            if (System.Text.RegularExpressions.Regex.IsMatch(address, @"([A-F].*[a-f])|([a-f].*[A-F])") && result != address)
                throw new ArgumentException("Bad address checksum", nameof(address));

            return result;
        }

        if (System.Text.RegularExpressions.Regex.IsMatch(address, @"^XE[0-9]{2}[0-9A-Za-z]{30,31}$"))
        {
            if (address.Substring(2, 2) != IbanChecksum(address))
                throw new ArgumentException("Bad ICAP checksum", nameof(address));

            var result = FromBase36(address.Substring(4)).ToString("X").PadLeft(40, '0');
            return GetChecksumAddress("0x" + result);
        }

        throw new ArgumentException("Invalid address", nameof(address));
    }

    public static string GetIcapAddress(string address)
    {
        var base36 = BigInteger.Parse(GetAddress(address).Substring(2), System.Globalization.NumberStyles.HexNumber).ToString("X36").ToUpperInvariant().PadLeft(30, '0');
        return "XE" + IbanChecksum("XE00" + base36) + base36;
    }


    /// <summary>
    /// Returns the address that would result from a CREATE for tx.
    /// </summary>
    public static string GetCreateAddress(string from, BigInteger nonce)
    {
        var addressUtil = new AddressUtil();
        var fromAddress = addressUtil.ConvertToChecksumAddress(from);

        string nonceHex = nonce.ToString("X");
        if (nonceHex == "0")
        {
            nonceHex = "0x";
        }
        else if (nonceHex.Length % 2 != 0)
        {
            nonceHex = "0x0" + nonceHex;
        }
        else
        {
            nonceHex = "0x" + nonceHex;
        }

    
        var encodedRlp = RlpEncoder.Encode(new[] { fromAddress.HexToByteArray(), nonceHex.HexToByteArray() });

        var keccak = new Sha3Keccack();
        var hash = keccak.CalculateHash(encodedRlp).ToHex();

        return addressUtil.ConvertToChecksumAddress("0x" + hash.Substring(24));
    }

    /// <summary>
    /// Returns the address that would result from a CREATE2 operation with the given from, salt and initCodeHash.
    /// </summary>
    public static string GetCreate2Address(string from, byte[] salt, byte[] initCodeHash)
    {
        var addressUtil = new AddressUtil();
        var fromAddress = addressUtil.ConvertToChecksumAddress(from);

        if (salt.Length != 32)
        {
            throw new ArgumentException("salt must be 32 bytes", nameof(salt));
        }

        if (initCodeHash.Length != 32)
        {
            throw new ArgumentException("initCodeHash must be 32 bytes", nameof(initCodeHash));
        }

        var prefix = new byte[] { 0xFF };
        var concatenated = new byte[1 + 20 + 32 + 32];
        Buffer.BlockCopy(prefix, 0, concatenated, 0, 1);
        Buffer.BlockCopy(fromAddress.HexToByteArray(), 0, concatenated, 1, 20);
        Buffer.BlockCopy(salt, 0, concatenated, 21, 32);
        Buffer.BlockCopy(initCodeHash, 0, concatenated, 53, 32);

        var keccak = new Sha3Keccack();
        var hash = keccak.CalculateHash(concatenated).ToHex();

        return addressUtil.ConvertToChecksumAddress("0x" + hash.Substring(24));
    }


}