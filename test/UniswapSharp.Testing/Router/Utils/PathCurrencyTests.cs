using UniswapSharp.Core.Entities;
using UniswapSharp.Router;
using UniswapSharp.Router.Utils;
using UniswapSharp.V3.Utils;
using V4Pool = UniswapSharp.V4.Entities.Pool;

namespace UniswapSharp.Testing.Router.Utils;

// Ported 1:1 from sdks/router-sdk/src/utils/pathCurrency.test.ts
public class PathCurrencyTests
{
    private static readonly Ether ETHER = Ether.OnChain(1);
    private static readonly Token token1 = new(1, "0x0000000000000000000000000000000000000002", 18, "t1");

    private static readonly V4Pool pool_v4_eth_token1 =
        new(token1, ETHER, 0, 0, Constants.ADDRESS_ZERO, EncodeSqrtRatioX96.Encode(1, 1), 0, 0);

    [Fact]
    public void GetPathCurrency_ReturnsEthInput()
    {
        Assert.True(PathCurrency.GetPathCurrency(ETHER, pool_v4_eth_token1).Equals(ETHER));
    }
}
