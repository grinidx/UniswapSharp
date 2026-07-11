using System.Numerics;

namespace UniswapSharp.Testing.SmartWallet;

// Shared test constants + ABI-word helpers.
// Ported from sdks/smart-wallet-sdk/src/utils/testConstants.ts (TEST_ADDRESS_1 is viem's zeroAddress).
internal static class SwTestConstants
{
    public const string TEST_ADDRESS_1 = "0x0000000000000000000000000000000000000000";
    public const string TEST_DATA_1 = "0x123456";
    public const string TEST_DATA_2 = "0xabcdef0123456789";
    public static readonly BigInteger TEST_VALUE_1 = 100;
    public static readonly BigInteger TEST_VALUE_2 = 200;

    // Left-pad a hex fragment to a full 32-byte ABI word (numbers / offsets / lengths).
    public static string Word(string hexNoPrefix) => hexNoPrefix.PadLeft(64, '0');

    // Right-pad a hex fragment to a full 32-byte ABI word (dynamic-bytes payloads).
    public static string RightPad(string hexNoPrefix) => hexNoPrefix.PadRight(64, '0');

    // A left-padded address word (lower-cased, 0x stripped).
    public static string AddrWord(string address) =>
        (address.StartsWith("0x") ? address[2..] : address).ToLowerInvariant().PadLeft(64, '0');
}
