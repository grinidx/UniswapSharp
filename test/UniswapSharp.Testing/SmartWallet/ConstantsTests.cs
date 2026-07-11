using UniswapSharp.SmartWallet;
using UniswapSharp.SmartWallet.Utils;
using UniswapSharp.V3.Utils;

namespace UniswapSharp.Testing.SmartWallet;

// Ported from sdks/smart-wallet-sdk/src/constants.test.ts, plus digit-level anchors for the
// canonical addresses and the function selectors the SDK depends on.
public class ConstantsTests
{
    private static IEnumerable<SupportedChainIds> AllChains() => Enum.GetValues<SupportedChainIds>();

    [Fact]
    public void SmartWalletAddresses_AreTheLatestVersions()
    {
        foreach (var chainId in AllChains())
        {
            Assert.Equal(
                Constants.SmartWalletVersions[chainId][SmartWalletVersion.LATEST],
                Constants.SmartWalletAddresses[(int)chainId]);
        }
    }

    [Fact]
    public void GetAllSmartWalletVersions_ReturnsAllVersionsForAChainId()
    {
        foreach (var chainId in AllChains())
        {
            Assert.Equal(
                Constants.SmartWalletVersions[chainId].Values,
                Constants.GetAllSmartWalletVersions(chainId));
        }
    }

    [Fact]
    public void CanonicalLatestAddress_IsV1_1_0()
    {
        Assert.Equal(
            "0x000000005c84F8Fd50b21CAC312528A64437030e",
            Constants.SmartWalletAddresses[(int)SupportedChainIds.MAINNET]);
    }

    [Fact]
    public void ChainsWithStaging_HaveFourVersions()
    {
        // MAINNET / UNICHAIN / BASE / OPTIMISM / BNB / UNICHAIN_SEPOLIA / SEPOLIA carry the staging build.
        Assert.Equal(new[]
        {
            "0x000000005c84F8Fd50b21CAC312528A64437030e",
            "0x000000005c84F8Fd50b21CAC312528A64437030e",
            "0x000000009B1D0aF20D8C6d0A44e162d11F9b8f00",
            "0x3cbad1e3b9049ecdb9588fb48dd61d80faf41bd5",
        }, Constants.GetAllSmartWalletVersions(SupportedChainIds.SEPOLIA));
    }

    [Fact]
    public void ChainsWithoutStaging_HaveThreeVersions()
    {
        // ARBITRUM_ONE / XLAYER / ARC / ROBINHOOD have no staging build.
        Assert.Equal(new[]
        {
            "0x000000005c84F8Fd50b21CAC312528A64437030e",
            "0x000000005c84F8Fd50b21CAC312528A64437030e",
            "0x000000009B1D0aF20D8C6d0A44e162d11F9b8f00",
        }, Constants.GetAllSmartWalletVersions(SupportedChainIds.ARBITRUM_ONE));
    }

    [Fact]
    public void ModeToBytes32_MapsBothModes()
    {
        Assert.Equal(Constants.MODE_BATCHED_CALL, Constants.ModeToBytes32(ModeType.BATCHED_CALL));
        Assert.Equal(Constants.MODE_BATCHED_CALL_CAN_REVERT, Constants.ModeToBytes32(ModeType.BATCHED_CALL_CAN_REVERT));
    }

    [Fact]
    public void ModeToBytes32_ThrowsForUnknownMode()
    {
        Assert.Throws<ArgumentException>(() => Constants.ModeToBytes32((ModeType)99));
    }

    // Pins the selectors hard-coded in SmartWallet.cs to keccak256 of their canonical signatures.
    [Theory]
    [InlineData("execute(((address,uint256,bytes)[],bool))", "99e1d016")]
    [InlineData("execute(bytes32,bytes)", "e9ae5c53")]
    public void Selectors_MatchKeccak(string signature, string expected)
    {
        Assert.Equal(expected, AbiFunctionEncoder.Selector(signature));
    }
}
