using UniswapSharp.SmartWallet;
using UniswapSharp.SmartWallet.Utils;

namespace UniswapSharp.Testing.SmartWallet;

// Ported 1:1 from sdks/smart-wallet-sdk/src/utils/delegation.test.ts.
// Only parseFromCode is ported; the signature-recovery helpers are omitted (see Delegation.cs).
public class DelegationTests
{
    private const string Address = "1111111111111111111111111111111111111111"; // 40 hex chars, no 0x

    [Fact]
    public void ParseFromCode_ParsesOutTheDelegation()
    {
        string delegation = Delegation.ParseFromCode($"{Constants.DELEGATION_MAGIC_PREFIX}{Address}");
        Assert.Equal($"0x{Address}", delegation);
    }

    [Fact]
    public void ParseFromCode_ThrowsWhenNoDelegation()
    {
        Assert.Throws<ArgumentException>(() => Delegation.ParseFromCode(string.Empty));
    }

    [Fact]
    public void ParseFromCode_ThrowsWhenMagicPrefixIncorrect()
    {
        const string incorrectMagicPrefix = "0x000000";
        var ex = Assert.Throws<ArgumentException>(
            () => Delegation.ParseFromCode($"{incorrectMagicPrefix}{Address}"));
        Assert.Equal($"Invalid delegation magic prefix: {incorrectMagicPrefix}", ex.Message);
    }
}
