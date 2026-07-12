using AwesomeAssertions;
using UniswapSharp.Flashtestations.Config;
using UniswapSharp.Flashtestations.Types;

namespace UniswapSharp.Testing.Flashtestations;

// Ported 1:1 from sdks/flashtestations-sdk/test/config/chains.test.ts
public class ChainsTests
{
    // ----- CHAIN_CONFIGS -----

    [Fact]
    public void ChainConfigs_ContainsUnichainMainnet()
    {
        var config = Chains.ChainConfigs[130];
        config.Should().NotBeNull();
        config.ChainId.Should().Be(130);
        config.Name.Should().Be("Unichain Mainnet");
        config.Slug.Should().Be("unichain-mainnet");
        config.ContractAddress.Should().NotBeNullOrEmpty();
        config.DefaultRpcUrl.Should().Be("https://mainnet.unichain.org");
        config.BlockExplorerUrl.Should().Be("https://uniscan.xyz");
    }

    [Fact]
    public void ChainConfigs_ContainsUnichainSepolia()
    {
        var config = Chains.ChainConfigs[1301];
        config.Should().NotBeNull();
        config.ChainId.Should().Be(1301);
        config.Name.Should().Be("Unichain Sepolia");
        config.Slug.Should().Be("unichain-sepolia");
        config.ContractAddress.Should().NotBeNullOrEmpty();
        config.DefaultRpcUrl.Should().Be("https://sepolia.unichain.org");
        config.BlockExplorerUrl.Should().Be("https://sepolia.uniscan.xyz");
    }

    [Fact]
    public void ChainConfigs_HasUniqueSlugs()
    {
        var slugs = Chains.ChainConfigs.Values.Select(c => c.Slug).ToList();
        slugs.Count.Should().Be(slugs.ToHashSet().Count);
    }

    // ----- getContractAddress -----

    [Fact]
    public void GetContractAddress_ReturnsAddressForSupportedChain()
    {
        var address = Chains.GetContractAddress(130);
        address.Should().Be(Chains.ChainConfigs[130].ContractAddress);
        address.Should().NotBeEmpty();
    }

    [Fact]
    public void GetContractAddress_ReturnsAddressForSepolia()
    {
        var address = Chains.GetContractAddress(1301);
        address.Should().Be(Chains.ChainConfigs[1301].ContractAddress);
        address.Should().NotBeEmpty();
    }

    [Fact]
    public void GetContractAddress_ThrowsForUnsupportedChain()
    {
        ((Action)(() => Chains.GetContractAddress(9999))).Should().Throw<ChainNotSupportedError>()
            .WithMessage("*Chain 9999 not supported*");
        ((Action)(() => Chains.GetContractAddress(1))).Should().Throw<ChainNotSupportedError>()
            .WithMessage("*Chain 1 not supported*");

        var ex = ((Action)(() => Chains.GetContractAddress(9999))).Should().Throw<ChainNotSupportedError>().Which;
        ex.ChainId.Should().Be(9999);
    }

    // ----- getRpcUrl -----

    [Fact]
    public void GetRpcUrl_ReturnsUrlForMainnet() => Chains.GetRpcUrl(130).Should().Be("https://mainnet.unichain.org");

    [Fact]
    public void GetRpcUrl_ReturnsUrlForSepolia() => Chains.GetRpcUrl(1301).Should().Be("https://sepolia.unichain.org");

    [Fact]
    public void GetRpcUrl_ThrowsForUnsupportedChain()
    {
        ((Action)(() => Chains.GetRpcUrl(9999))).Should().Throw<ChainNotSupportedError>().WithMessage("*Chain 9999 not supported*");
        ((Action)(() => Chains.GetRpcUrl(42))).Should().Throw<ChainNotSupportedError>().WithMessage("*Chain 42 not supported*");
    }

    // ----- getBlockExplorerUrl -----

    [Fact]
    public void GetBlockExplorerUrl_ReturnsUrlForMainnet() =>
        Chains.GetBlockExplorerUrl(130).Should().Be("https://uniscan.xyz");

    [Fact]
    public void GetBlockExplorerUrl_ReturnsUrlForSepolia() =>
        Chains.GetBlockExplorerUrl(1301).Should().Be("https://sepolia.uniscan.xyz");

    [Fact]
    public void GetBlockExplorerUrl_ThrowsForUnsupportedChain() =>
        ((Action)(() => Chains.GetBlockExplorerUrl(9999))).Should().Throw<ChainNotSupportedError>()
            .WithMessage("*Chain 9999 not supported*");

    // ----- getChainConfig -----

    [Fact]
    public void GetChainConfig_ReturnsCompleteConfig()
    {
        var config = Chains.GetChainConfig(130);
        config.Should().Be(Chains.ChainConfigs[130]);
        config.ChainId.Should().Be(130);
        config.Name.Should().Be("Unichain Mainnet");
    }

    [Fact]
    public void GetChainConfig_ReturnsConfigForSepolia()
    {
        var config = Chains.GetChainConfig(1301);
        config.Should().Be(Chains.ChainConfigs[1301]);
        config.ChainId.Should().Be(1301);
    }

    [Fact]
    public void GetChainConfig_ThrowsForUnsupportedChain() =>
        ((Action)(() => Chains.GetChainConfig(9999))).Should().Throw<ChainNotSupportedError>()
            .WithMessage("*Chain 9999 not supported*");

    // ----- getSupportedChains -----

    [Fact]
    public void GetSupportedChains_ReturnsChainIds()
    {
        var supportedChains = Chains.GetSupportedChains();
        supportedChains.Should().Contain(130);
        supportedChains.Should().Contain(1301);
    }

    // ----- isChainSupported -----

    [Fact]
    public void IsChainSupported_TrueForSupportedChains()
    {
        Chains.IsChainSupported(130).Should().BeTrue();
        Chains.IsChainSupported(1301).Should().BeTrue();
    }

    [Fact]
    public void IsChainSupported_FalseForUnsupportedChains()
    {
        Chains.IsChainSupported(1).Should().BeFalse();
        Chains.IsChainSupported(9999).Should().BeFalse();
        Chains.IsChainSupported(42).Should().BeFalse();
        Chains.IsChainSupported(0).Should().BeFalse();
    }

    [Fact]
    public void IsChainSupported_EdgeCases()
    {
        // NaN / Infinity from the upstream test are not representable for a C# int; -1 is.
        Chains.IsChainSupported(-1).Should().BeFalse();
    }

    // ----- error handling integration -----

    [Fact]
    public void ErrorHandling_ConsistentErrorInformation()
    {
        const int unsupportedChainId = 9999;

        void TestError(Action fn)
        {
            var ex = fn.Should().Throw<ChainNotSupportedError>().Which;
            ex.ChainId.Should().Be(unsupportedChainId);
            ex.Message.Should().Contain($"Chain {unsupportedChainId} not supported");
            ex.Message.Should().Contain("130, 1301");
        }

        TestError(() => Chains.GetContractAddress(unsupportedChainId));
        TestError(() => Chains.GetRpcUrl(unsupportedChainId));
        TestError(() => Chains.GetBlockExplorerUrl(unsupportedChainId));
        TestError(() => Chains.GetChainConfig(unsupportedChainId));
    }

    // ----- getChainBySlug -----

    [Fact]
    public void GetChainBySlug_ReturnsConfigForValidSlug()
    {
        var config = Chains.GetChainBySlug("unichain-mainnet");
        config.Should().NotBeNull();
        config!.ChainId.Should().Be(130);
        config.Name.Should().Be("Unichain Mainnet");
    }

    [Fact]
    public void GetChainBySlug_ReturnsConfigForSepolia()
    {
        var config = Chains.GetChainBySlug("unichain-sepolia");
        config.Should().NotBeNull();
        config!.ChainId.Should().Be(1301);
        config.Name.Should().Be("Unichain Sepolia");
    }

    [Fact]
    public void GetChainBySlug_ReturnsNullForInvalidSlug()
    {
        Chains.GetChainBySlug("invalid-chain").Should().BeNull();
        Chains.GetChainBySlug("").Should().BeNull();
        Chains.GetChainBySlug("ethereum-mainnet").Should().BeNull();
    }

    // ----- getSupportedChainSlugs -----

    [Fact]
    public void GetSupportedChainSlugs_ReturnsSlugs()
    {
        var slugs = Chains.GetSupportedChainSlugs();
        slugs.Should().Contain("unichain-mainnet");
        slugs.Should().Contain("unichain-sepolia");
        slugs.Should().Contain("unichain-alphanet");
        slugs.Should().Contain("unichain-experimental");
    }

    [Fact]
    public void GetSupportedChainSlugs_ReturnsNonEmptyStrings()
    {
        foreach (var slug in Chains.GetSupportedChainSlugs())
        {
            slug.Should().NotBeNullOrEmpty();
        }
    }

    // ----- isValidChainSlug -----

    [Fact]
    public void IsValidChainSlug_TrueForValidSlugs()
    {
        Chains.IsValidChainSlug("unichain-mainnet").Should().BeTrue();
        Chains.IsValidChainSlug("unichain-sepolia").Should().BeTrue();
        Chains.IsValidChainSlug("unichain-alphanet").Should().BeTrue();
        Chains.IsValidChainSlug("unichain-experimental").Should().BeTrue();
    }

    [Fact]
    public void IsValidChainSlug_FalseForInvalidSlugs()
    {
        Chains.IsValidChainSlug("invalid").Should().BeFalse();
        Chains.IsValidChainSlug("").Should().BeFalse();
        Chains.IsValidChainSlug("ethereum").Should().BeFalse();
        Chains.IsValidChainSlug("UNICHAIN-MAINNET").Should().BeFalse(); // case sensitive
    }

    // ----- getDefaultChainSlug -----

    [Fact]
    public void GetDefaultChainSlug_ReturnsMainnet() => Chains.GetDefaultChainSlug().Should().Be("unichain-mainnet");

    [Fact]
    public void GetDefaultChainSlug_ReturnsValidSlug() =>
        Chains.IsValidChainSlug(Chains.GetDefaultChainSlug()).Should().BeTrue();
}
