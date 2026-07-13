using System.Numerics;
using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.V3;
using UniswapSharp.V3.Entities;
using UniswapSharp.V3.Utils;
using static UniswapSharp.V3.Constants;

namespace UniswapSharp.Testing.V3;

// Regression: SwapOptions.InputTokenPermit must be able to carry a real permit
// (upstream `inputTokenPermit?: PermitOptions = StandardPermitArguments | AllowedPermitArguments`)
// and SwapRouter must prepend the selfPermit calldata. Upstream ships no test for this path.
public class SwapRouterPermitTests
{
    private static readonly Token token0 = new(1, "0x0000000000000000000000000000000000000001", 18, "t0", "token0");
    private static readonly Token token1 = new(1, "0x0000000000000000000000000000000000000002", 18, "t1", "token1");

    private static Pool MakePool(Token a, Token b) => new(
        a, b, FeeAmount.MEDIUM, EncodeSqrtRatioX96.Encode(1, 1), BigInteger.Parse("1000000000000000000"),
        0, new List<Tick>
        {
            new(NearestUsableTick.Find(TickMath.MIN_TICK, TICK_SPACINGS[FeeAmount.MEDIUM]), BigInteger.Parse("1000000000000000000"), BigInteger.Parse("1000000000000000000")),
            new(NearestUsableTick.Find(TickMath.MAX_TICK, TICK_SPACINGS[FeeAmount.MEDIUM]), BigInteger.Parse("-1000000000000000000"), BigInteger.Parse("1000000000000000000")),
        });

    [Fact]
    public async Task SwapCallParameters_WithStandardInputTokenPermit_PrependsSelfPermitCalldata()
    {
        var pool = MakePool(token0, token1);
        var trade = await Trade<Token, Token>.FromRoute(
            new Route<Token, Token>(new List<Pool> { pool }, token0, token1),
            CurrencyAmount<Token>.FromRawAmount(token0, 100),
            TradeType.EXACT_INPUT);

        var permit = new SelfPermit.StandardPermitArguments
        {
            V = 1,
            R = "0x0000000000000000000000000000000000000000000000000000000000000001",
            S = "0x0000000000000000000000000000000000000000000000000000000000000002",
            Amount = 100,
            Deadline = 123,
        };

        var result = SwapRouter.SwapCallParameters(trade, new Staker.SwapOptions
        {
            SlippageTolerance = new Percent(1, 100),
            Recipient = "0x0000000000000000000000000000000000000003",
            Deadline = 123,
            InputTokenPermit = permit,
        });

        // The permit must be encoded and included, matching SelfPermit's (vector-tested) encoding.
        string expectedPermit = SelfPermit.EncodePermit(token0, permit);
        Assert.Contains(expectedPermit.Substring(2), result.Calldata);
    }
}
