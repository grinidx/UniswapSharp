using UniswapSharp.Core.Entities;
using UniswapSharp.V3.Utils;
using UniswapSharp.V4;
using UniswapSharp.V4.Entities;
using UniswapSharp.V4.Utils;
using EncodeRouteToPath = UniswapSharp.V4.Utils.EncodeRouteToPath;
using Pool = UniswapSharp.V4.Entities.Pool;
using Tick = UniswapSharp.V3.Entities.Tick;

namespace UniswapSharp.Testing.V4.Utils;

// Ported 1:1 from sdks/v4-sdk/src/utils/encodeRouteToPath.test.ts
public class EncodeRouteToPathTests
{
    private static readonly Ether eth = Ether.OnChain(1);
    private static readonly Token weth = Weth9.Tokens[1];
    private static readonly Token currency1 = new(1, "0x1111111111111111111111111111111111111111", 18, "t1");
    private static readonly Token currency2 = new(1, "0x2222222222222222222222222222222222222222", 18, "t2");
    private static readonly Token currency3 = new(1, "0x3333333333333333333333333333333333333333", 18, "t3");

    private const int MEDIUM = Constants.FEE_AMOUNT_MEDIUM;
    private const int SPACING = Constants.TICK_SPACING_TEN;
    private const string ZERO = Constants.ADDRESS_ZERO;

    private static Pool MakePool(BaseCurrency a, BaseCurrency b) =>
        new(a, b, MEDIUM, SPACING, ZERO, EncodeSqrtRatioX96.Encode(1, 1), 0, 0, new List<Tick>());

    private static readonly Pool pool_eth_1 = MakePool(eth, currency1);
    private static readonly Pool pool_1_2 = MakePool(currency1, currency2);
    private static readonly Pool pool_2_3 = MakePool(currency2, currency3);

    private static readonly Route<Ether, Token> route = new(new List<Pool> { pool_eth_1, pool_1_2, pool_2_3 }, eth, currency3);

    private static PathKey Key(string currency) => new(currency, 3000, 10, ZERO, "0x");

    [Fact]
    public void EncodesExactIn()
    {
        var expected = new List<PathKey>
        {
            Key("0x1111111111111111111111111111111111111111"),
            Key("0x2222222222222222222222222222222222222222"),
            Key("0x3333333333333333333333333333333333333333"),
        };
        Assert.Equal(expected, EncodeRouteToPath.Encode(route));
    }

    [Fact]
    public void EncodesExactOut()
    {
        var expected = new List<PathKey>
        {
            Key("0x0000000000000000000000000000000000000000"),
            Key("0x1111111111111111111111111111111111111111"),
            Key("0x2222222222222222222222222222222222222222"),
        };
        Assert.Equal(expected, EncodeRouteToPath.Encode(route, true));
    }

    [Fact]
    public void EncodesWhenOutputDiffersFromPathOutput()
    {
        var newRoute = new Route<Token, Token>(new List<Pool> { pool_1_2, pool_eth_1 }, currency2, weth);
        var expected = new List<PathKey>
        {
            Key("0x2222222222222222222222222222222222222222"),
            Key("0x1111111111111111111111111111111111111111"),
        };
        Assert.Equal(expected, EncodeRouteToPath.Encode(newRoute, true));
    }

    [Fact]
    public void EncodesWhenInputDiffersFromPathInput()
    {
        var newRoute = new Route<Token, Token>(new List<Pool> { pool_eth_1, pool_1_2 }, weth, currency2);
        var expected = new List<PathKey>
        {
            Key("0x1111111111111111111111111111111111111111"),
            Key("0x2222222222222222222222222222222222222222"),
        };
        Assert.Equal(expected, EncodeRouteToPath.Encode(newRoute, false));
    }
}
