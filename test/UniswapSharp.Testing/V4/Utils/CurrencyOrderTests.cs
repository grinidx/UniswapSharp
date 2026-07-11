using UniswapSharp.Core.Entities;
using UniswapSharp.V4.Utils;

namespace UniswapSharp.Testing.V4.Utils;

// Ported from v4-sdk/src/utils/sortsBefore.ts behavior.
public class CurrencyOrderTests
{
    private static readonly Token token0 = new(1, "0x0000000000000000000000000000000000000001", 18, "t0", "token0");
    private static readonly Token token1 = new(1, "0x0000000000000000000000000000000000000002", 18, "t1", "token1");

    [Fact]
    public void NativeSortsFirst()
    {
        Assert.True(CurrencyOrder.SortsBefore(Ether.OnChain(1), token0));
        Assert.False(CurrencyOrder.SortsBefore(token0, Ether.OnChain(1)));
    }

    [Fact]
    public void TokensCompareByAddress()
    {
        Assert.True(CurrencyOrder.SortsBefore(token0, token1));
        Assert.False(CurrencyOrder.SortsBefore(token1, token0));
        // matches the underlying Token.SortsBefore
        Assert.Equal(token0.SortsBefore(token1), CurrencyOrder.SortsBefore(token0, token1));
    }
}
