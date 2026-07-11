using UniswapSharp.Core.Entities;
using UniswapSharp.V4;
using UniswapSharp.V4.Utils;

namespace UniswapSharp.Testing.V4.Utils;

// Ported from v4-sdk/src/utils/currencyMap.ts behavior.
public class CurrencyMapTests
{
    private static readonly Token token = new(1, "0x0000000000000000000000000000000000000001", 18, "t0", "token0");

    [Fact]
    public void ToAddress_NativeIsZeroAddress()
    {
        Assert.Equal(Constants.ADDRESS_ZERO, CurrencyMap.ToAddress(Ether.OnChain(1)));
    }

    [Fact]
    public void ToAddress_TokenIsWrappedAddress()
    {
        Assert.Equal(token.Address, CurrencyMap.ToAddress(token));
    }
}
