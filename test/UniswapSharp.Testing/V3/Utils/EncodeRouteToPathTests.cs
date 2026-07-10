using UniswapSharp.Core.Entities;
using UniswapSharp.V3.Entities;
using UniswapSharp.V3.Utils;
using static UniswapSharp.V3.Constants;

namespace UniswapSharp.Testing.V3.Utils;

// Ported from sdks/v3-sdk/src/utils/encodeRouteToPath.test.ts
public class EncodeRouteToPathTests
{
    private static readonly Ether ETHER = Ether.OnChain(1);
    private static readonly Token token0 = new(1, "0x0000000000000000000000000000000000000001", 18, "t0", "token0");
    private static readonly Token token1 = new(1, "0x0000000000000000000000000000000000000002", 18, "t1", "token1");
    private static readonly Token token2 = new(1, "0x0000000000000000000000000000000000000003", 18, "t2", "token2");
    private static readonly Token weth = Weth9.Tokens[1];

    private static Pool P(Token a, Token b, FeeAmount fee) =>
        new(a, b, fee, EncodeSqrtRatioX96.Encode(1, 1), 0, 0, new List<Tick>());

    private static readonly Pool pool_0_1_medium = P(token0, token1, FeeAmount.MEDIUM);
    private static readonly Pool pool_1_2_low = P(token1, token2, FeeAmount.LOW);
    private static readonly Pool pool_0_weth = P(token0, weth, FeeAmount.MEDIUM);
    private static readonly Pool pool_1_weth = P(token1, weth, FeeAmount.MEDIUM);

    private static Route<BaseCurrency, BaseCurrency> R(List<Pool> pools, BaseCurrency input, BaseCurrency output) =>
        new(pools, input, output);

    private static readonly Route<BaseCurrency, BaseCurrency> route_0_1 = R([pool_0_1_medium], token0, token1);
    private static readonly Route<BaseCurrency, BaseCurrency> route_0_1_2 = R([pool_0_1_medium, pool_1_2_low], token0, token2);
    private static readonly Route<BaseCurrency, BaseCurrency> route_0_weth = R([pool_0_weth], token0, ETHER);
    private static readonly Route<BaseCurrency, BaseCurrency> route_0_1_weth = R([pool_0_1_medium, pool_1_weth], token0, ETHER);
    private static readonly Route<BaseCurrency, BaseCurrency> route_weth_0 = R([pool_0_weth], ETHER, token0);
    private static readonly Route<BaseCurrency, BaseCurrency> route_weth_0_1 = R([pool_0_weth, pool_0_1_medium], ETHER, token1);

    [Fact]
    public void PacksExactInputSingleHop() =>
        Assert.Equal("0x0000000000000000000000000000000000000001000bb80000000000000000000000000000000000000002",
            EncodeRouteToPath.Encode(route_0_1, false));

    [Fact]
    public void PacksExactOutputSingleHop() =>
        Assert.Equal("0x0000000000000000000000000000000000000002000bb80000000000000000000000000000000000000001",
            EncodeRouteToPath.Encode(route_0_1, true));

    [Fact]
    public void PacksMultihopExactInput() =>
        Assert.Equal("0x0000000000000000000000000000000000000001000bb800000000000000000000000000000000000000020001f40000000000000000000000000000000000000003",
            EncodeRouteToPath.Encode(route_0_1_2, false));

    [Fact]
    public void PacksMultihopExactOutput() =>
        Assert.Equal("0x00000000000000000000000000000000000000030001f40000000000000000000000000000000000000002000bb80000000000000000000000000000000000000001",
            EncodeRouteToPath.Encode(route_0_1_2, true));

    [Fact]
    public void WrapsEtherInputExactInputSingleHop() =>
        Assert.Equal("0xc02aaa39b223fe8d0a0e5c4f27ead9083c756cc2000bb80000000000000000000000000000000000000001",
            EncodeRouteToPath.Encode(route_weth_0, false));

    [Fact]
    public void WrapsEtherInputExactOutputSingleHop() =>
        Assert.Equal("0x0000000000000000000000000000000000000001000bb8c02aaa39b223fe8d0a0e5c4f27ead9083c756cc2",
            EncodeRouteToPath.Encode(route_weth_0, true));

    [Fact]
    public void WrapsEtherInputExactInputMultihop() =>
        Assert.Equal("0xc02aaa39b223fe8d0a0e5c4f27ead9083c756cc2000bb80000000000000000000000000000000000000001000bb80000000000000000000000000000000000000002",
            EncodeRouteToPath.Encode(route_weth_0_1, false));

    [Fact]
    public void WrapsEtherInputExactOutputMultihop() =>
        Assert.Equal("0x0000000000000000000000000000000000000002000bb80000000000000000000000000000000000000001000bb8c02aaa39b223fe8d0a0e5c4f27ead9083c756cc2",
            EncodeRouteToPath.Encode(route_weth_0_1, true));

    [Fact]
    public void WrapsEtherOutputExactInputSingleHop() =>
        Assert.Equal("0x0000000000000000000000000000000000000001000bb8c02aaa39b223fe8d0a0e5c4f27ead9083c756cc2",
            EncodeRouteToPath.Encode(route_0_weth, false));

    [Fact]
    public void WrapsEtherOutputExactOutputSingleHop() =>
        Assert.Equal("0xc02aaa39b223fe8d0a0e5c4f27ead9083c756cc2000bb80000000000000000000000000000000000000001",
            EncodeRouteToPath.Encode(route_0_weth, true));

    [Fact]
    public void WrapsEtherOutputExactInputMultihop() =>
        Assert.Equal("0x0000000000000000000000000000000000000001000bb80000000000000000000000000000000000000002000bb8c02aaa39b223fe8d0a0e5c4f27ead9083c756cc2",
            EncodeRouteToPath.Encode(route_0_1_weth, false));

    [Fact]
    public void WrapsEtherOutputExactOutputMultihop() =>
        Assert.Equal("0xc02aaa39b223fe8d0a0e5c4f27ead9083c756cc2000bb80000000000000000000000000000000000000002000bb80000000000000000000000000000000000000001",
            EncodeRouteToPath.Encode(route_0_1_weth, true));
}
