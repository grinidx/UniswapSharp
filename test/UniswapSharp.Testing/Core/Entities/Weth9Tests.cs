using UniswapSharp.Core;
using UniswapSharp.Core.Entities;

namespace UniswapSharp.Testing.Core.Entities;

// Pins the WETH9 registry against upstream entities/weth9.ts. The regression cases are the
// chains previously missing from the C# map (incl. SEPOLIA), which made Ether.OnChain(id).Wrapped() throw.
public class Weth9Tests
{
    [Theory]
    [InlineData(11155111, "0xfFf9976782d46CC05630D1f6eBAb18b2324d6B14", "WETH")]
    [InlineData(84532, "0x4200000000000000000000000000000000000006", "WETH")]
    [InlineData(7777777, "0x4200000000000000000000000000000000000006", "WETH")]
    [InlineData(81457, "0x4300000000000000000000000000000000000004", "WETH")]
    [InlineData(480, "0x4200000000000000000000000000000000000006", "WETH")]
    [InlineData(1301, "0x4200000000000000000000000000000000000006", "WETH")]
    [InlineData(130, "0x4200000000000000000000000000000000000006", "WETH")]
    [InlineData(10143, "0x760AfE86e5de5fa0Ee542fc7B7B713e1c5425701", "WMON")]
    [InlineData(1868, "0x4200000000000000000000000000000000000006", "WETH")]
    [InlineData(143, "0x3bd359C1119dA7Da1D913D1C4D2B7c461115433A", "WMON")]
    [InlineData(59144, "0xe5D7C2a44FfDDf6b295A15c148167daaAf5Cf34f", "WETH")]
    [InlineData(4326, "0x4200000000000000000000000000000000000006", "WETH")]
    [InlineData(4663, "0x0Bd7D308f8E1639FAb988df18A8011f41EAcAD73", "WETH")]
    [InlineData(57073, "0x4200000000000000000000000000000000000006", "WETH")]
    public void Weth9_ContainsChain(int chainId, string address, string symbol)
    {
        Assert.True(Weth9.Tokens.ContainsKey(chainId));
        var token = Weth9.Tokens[chainId];
        Assert.Equal(address, token.Address);
        Assert.Equal(symbol, token.Symbol);
        Assert.Equal(18, token.Decimals);
    }

    [Fact]
    public void Ether_Wrapped_ResolvesForSepolia()
    {
        var weth = Ether.OnChain(11155111).Wrapped();
        Assert.Equal("0xfFf9976782d46CC05630D1f6eBAb18b2324d6B14", weth.Address);
    }
}
