using UniswapSharp.Core;

namespace UniswapSharp.Testing.Core;

public class AddressesTests
{
    [Fact]
    public void SwapRouter02Addresses_ShouldReturnCorrectAddress_ForBase()
    {
        string address = Addresses.SWAP_ROUTER_02_ADDRESSES(ChainId.BASE);
        Assert.Equal("0x2626664c2603336E57B271c5C0b26F421741e481", address);
    }

    [Fact]
    public void SwapRouter02Addresses_ShouldReturnCorrectAddress_ForBaseGoerli()
    {
        string address = Addresses.SWAP_ROUTER_02_ADDRESSES(ChainId.BASE_GOERLI);
        Assert.Equal("0x8357227D4eDc78991Db6FDB9bD6ADE250536dE1d", address);
    }

    [Fact]
    public void SwapRouter02Addresses_ShouldReturnCorrectAddress_ForAvalanche()
    {
        string address = Addresses.SWAP_ROUTER_02_ADDRESSES(ChainId.AVALANCHE);
        Assert.Equal("0xbb00FF08d01D300023C629E8fFfFcb65A5a578cE", address);
    }

    [Fact]
    public void SwapRouter02Addresses_ShouldReturnCorrectAddress_ForBNB()
    {
        string address = Addresses.SWAP_ROUTER_02_ADDRESSES(ChainId.BNB);
        Assert.Equal("0xB971eF87ede563556b2ED4b1C0b0019111Dd85d2", address);
    }

    [Fact]
    public void SwapRouter02Addresses_ShouldReturnCorrectAddress_ForArbitrumGoerli()
    {
        string address = Addresses.SWAP_ROUTER_02_ADDRESSES(ChainId.ARBITRUM_GOERLI);
        Assert.Equal("0x68b3465833fb72A70ecDF485E0e4C7bD8665Fc45", address);
    }

    [Fact]
    public void SwapRouter02Addresses_ShouldReturnCorrectAddress_ForOptimismSepolia()
    {
        string address = Addresses.SWAP_ROUTER_02_ADDRESSES(ChainId.OPTIMISM_SEPOLIA);
        Assert.Equal("0x94cC0AaC535CCDB3C01d6787D6413C739ae12bc4", address);
    }

    [Fact]
    public void SwapRouter02Addresses_ShouldReturnCorrectAddress_ForSepolia()
    {
        string address = Addresses.SWAP_ROUTER_02_ADDRESSES(ChainId.SEPOLIA);
        Assert.Equal("0x3bFA4769FB09eefC5a80d6E87c3B9C650f7Ae48E", address);
    }

    [Fact]
    public void SwapRouter02Addresses_ShouldReturnCorrectAddress_ForBlast()
    {
        string address = Addresses.SWAP_ROUTER_02_ADDRESSES(ChainId.BLAST);
        Assert.Equal("0x549FEB8c9bd4c12Ad2AB27022dA12492aC452B66", address);
    }
}