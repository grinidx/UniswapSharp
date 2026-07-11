using System.Numerics;
using UniswapSharp.V3.Utils;

namespace UniswapSharp.Testing.V3.Utils;

// Ported 1:1 from sdks/v3-sdk/src/utils/maxLiquidityForAmounts.test.ts
public class MaxLiquidityTests
{
    private const string MAX = "115792089237316195423570985008687907853269984665640564039457584007913129639935";

    [Theory]
    // ---- imprecise (useFullPrecision = false) ----
    // price inside
    [InlineData(1, 1, "100", "200", false, "2148")]
    [InlineData(1, 1, "100", MAX, false, "2148")]
    [InlineData(1, 1, MAX, "200", false, "4297")]
    // price below
    [InlineData(99, 110, "100", "200", false, "1048")]
    [InlineData(99, 110, "100", MAX, false, "1048")]
    [InlineData(99, 110, MAX, "200", false, "1214437677402050006470401421068302637228917309992228326090730924516431320489727")]
    // price above
    [InlineData(111, 100, "100", "200", false, "2097")]
    [InlineData(111, 100, "100", MAX, false, "1214437677402050006470401421098959354205873606971497132040612572422243086574654")]
    [InlineData(111, 100, MAX, "200", false, "2097")]
    // ---- precise (useFullPrecision = true) ----
    // price inside
    [InlineData(1, 1, "100", "200", true, "2148")]
    [InlineData(1, 1, "100", MAX, true, "2148")]
    [InlineData(1, 1, MAX, "200", true, "4297")]
    // price below
    [InlineData(99, 110, "100", "200", true, "1048")]
    [InlineData(99, 110, "100", MAX, true, "1048")]
    [InlineData(99, 110, MAX, "200", true, "1214437677402050006470401421082903520362793114274352355276488318240158678126184")]
    // price above
    [InlineData(111, 100, "100", "200", true, "2097")]
    [InlineData(111, 100, "100", MAX, true, "1214437677402050006470401421098959354205873606971497132040612572422243086574654")]
    [InlineData(111, 100, MAX, "200", true, "2097")]
    public void MaxLiquidityForAmounts_Matches(int curA1, int curA0, string amount0, string amount1, bool useFullPrecision, string expected)
    {
        BigInteger current = EncodeSqrtRatioX96.Encode(curA1, curA0);
        BigInteger lower = EncodeSqrtRatioX96.Encode(100, 110);
        BigInteger upper = EncodeSqrtRatioX96.Encode(110, 100);

        BigInteger result = MaxLiquidity.MaxLiquidityForAmounts(
            current, lower, upper, BigInteger.Parse(amount0), BigInteger.Parse(amount1), useFullPrecision);

        Assert.Equal(BigInteger.Parse(expected), result);
    }
}
