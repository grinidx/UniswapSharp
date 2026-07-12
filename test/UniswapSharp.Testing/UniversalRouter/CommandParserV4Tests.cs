using System.Numerics;
using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Router.Entities;
using UniswapSharp.UniversalRouter;
using UniswapSharp.UniversalRouter.Utils;
using UniswapSharp.V4.Utils;
using CurrencyAmountBase = UniswapSharp.Core.Entities.Fractions.CurrencyAmount<UniswapSharp.Core.Entities.BaseCurrency>;
using Param = UniswapSharp.UniversalRouter.Utils.Param;
using RouterTrade = UniswapSharp.Router.Entities.Trade<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;
using V4Pool = UniswapSharp.V4.Entities.Pool;
using V4Route = UniswapSharp.V4.Entities.Route<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;
using V4Trade = UniswapSharp.V4.Entities.Trade<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;

namespace UniswapSharp.Testing.UniversalRouter;

// Ported from the "V4 per-hop slippage (V2.1.1)" block of test/utils/commandParser.test.ts
public class CommandParserV4Tests
{
    private static readonly Token WETH = UniswapData.WETH;
    private static readonly Token USDC = UniswapData.USDC;
    private static readonly Token DAI = UniswapData.DAI;

    private static readonly V4Pool WETH_USDC_V4 = UniswapData.MakeV4Pool(WETH, USDC);
    private static readonly V4Pool USDC_DAI_V4 = UniswapData.MakeV4Pool(USDC, DAI);

    private static RouterTrade Trade(BigInteger[]? minHop)
    {
        var v4Route = new V4Route(new List<V4Pool> { WETH_USDC_V4, USDC_DAI_V4 }, WETH, DAI);
        var v4Trade = V4Trade.CreateUncheckedTrade(new UniswapSharp.V4.Entities.RouteInput<BaseCurrency, BaseCurrency>
        {
            Route = v4Route,
            InputAmount = CurrencyAmountBase.FromRawAmount((BaseCurrency)WETH, BigInteger.Parse("1000000000000000000")),
            OutputAmount = CurrencyAmountBase.FromRawAmount((BaseCurrency)DAI, BigInteger.Parse("1000000000000000000")),
        }, TradeType.EXACT_INPUT);

        return new RouterTrade(TradeType.EXACT_INPUT, v4Routes: new[]
        {
            new V4RouteAmounts<BaseCurrency, BaseCurrency>(v4Trade.Route, v4Trade.InputAmount, v4Trade.OutputAmount, minHop),
        });
    }

    private static SwapExactIn FindSwapExactIn(UniversalRouterCall call)
    {
        var v4Command = call.Commands.First(c => c.CommandType == CommandType.V4_SWAP);
        var swapAction = v4Command.Params.First(p => p.Name == "SWAP_EXACT_IN");
        var swapParam = ((List<Param>)swapAction.Value!).First(p => p.Name == "swap");
        return (SwapExactIn)swapParam.Value!;
    }

    [Fact]
    public void SurfacesMinHopPriceX36InDecodedV4Swap()
    {
        var trade = Trade(new BigInteger[] { 1000, 2000 });
        var mp = SwapRouter.SwapCallParameters(trade, UniswapData.SwapOptions(urVersion: UniversalRouterVersion.V2_1_1));

        var result = CommandParser.ParseCalldata(mp.Calldata, UniversalRouterVersion.V2_1_1);
        var swap = FindSwapExactIn(result);
        Assert.Equal(new List<string> { "1000", "2000" }, swap.MinHopPriceX36!.Select(v => v.ToString()).ToList());
    }

    [Fact]
    public void OmitsMinHopPriceX36WhenDecodingAsV2_0()
    {
        var trade = Trade(null);
        var mp = SwapRouter.SwapCallParameters(trade, UniswapData.SwapOptions(urVersion: UniversalRouterVersion.V2_0));

        var result = CommandParser.ParseCalldata(mp.Calldata, UniversalRouterVersion.V2_0);
        var swap = FindSwapExactIn(result);
        Assert.Null(swap.MinHopPriceX36);
    }
}
