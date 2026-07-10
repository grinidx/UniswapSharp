using UniswapSharp.Core.Entities;
// ReSharper disable InconsistentNaming

namespace UniswapSharp.Testing.Core.Entities;

public class CurrencyTests
{
    private const string ADDRESS_ZERO = "0x0000000000000000000000000000000000000000";
    private const string ADDRESS_ONE = "0x0000000000000000000000000000000000000001";
    private readonly Token t0 = new(1, ADDRESS_ZERO, 18);
    private readonly Token t1 = new(1, ADDRESS_ONE, 18);

    [Fact]
    public void Equals_Test()
    {
        Assert.True(Ether.OnChain(1).Equals(Ether.OnChain(1)));
        Assert.False(Ether.OnChain(1).Equals(t0));
        Assert.False(t1.Equals(t0));
        Assert.True(t0.Equals(t0));
        Assert.True(t0.Equals(new Token(1, ADDRESS_ZERO, 18, "symbol", "name")));
    }
}
