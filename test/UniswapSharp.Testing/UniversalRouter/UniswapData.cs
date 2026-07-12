using System.Numerics;
using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.Router.Entities;
using UniswapSharp.Router.Entities.MixedRoute;
using UniswapSharp.UniversalRouter.Entities.Actions;
using UniswapSharp.UniversalRouter.Utils;
using UniswapSharp.V3.Utils;
using Constants = UniswapSharp.UniversalRouter.Utils.Constants;
using FeeAmount = UniswapSharp.V3.Constants.FeeAmount;
using MixedTrade = UniswapSharp.Router.Entities.MixedRoute.MixedRouteTrade<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;
using RouterTrade = UniswapSharp.Router.Entities.Trade<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;
using Tick = UniswapSharp.V3.Entities.Tick;
using V2TradeT = UniswapSharp.V2.Entities.Trade<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;
using V3Pool = UniswapSharp.V3.Entities.Pool;
using V3TradeT = UniswapSharp.V3.Entities.Trade<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;
using V4Pool = UniswapSharp.V4.Entities.Pool;
using V4TradeT = UniswapSharp.V4.Entities.Trade<UniswapSharp.Core.Entities.BaseCurrency, UniswapSharp.Core.Entities.BaseCurrency>;

namespace UniswapSharp.Testing.UniversalRouter;

// Ported test helpers from sdks/universal-router-sdk/test/utils/uniswapData.ts (the parts referenced by the
// static-pool tests; the RPC fork helpers getUniswapPools/getPair/getPool are not portable offline).
internal static class UniswapData
{
    public const string TEST_RECIPIENT_ADDRESS = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    public const string TEST_FEE_RECIPIENT_ADDRESS = "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    public static readonly Ether ETHER = Ether.OnChain(1);
    public static readonly Token WETH = new(1, "0xc02aaa39b223fe8d0a0e5c4f27ead9083c756cc2", 18, "WETH", "Wrapped Ether");
    public static readonly Token DAI = new(1, "0x6B175474E89094C44Da98b954EedeAC495271d0F", 18, "DAI", "dai");
    public static readonly Token USDC = new(1, "0xa0b86991c6218b36c1d19d4a2e9eb0ce3606eb48", 6, "USDC", "USD Coin");
    public const FeeAmount FEE_AMOUNT = FeeAmount.MEDIUM;

    private static readonly BigInteger LIQUIDITY = BigInteger.Pow(10, 18) * 1_000_000;

    private static List<Tick> Ticks(int tickSpacing) => new()
    {
        new Tick(NearestUsableTick.Find(TickMath.MIN_TICK, tickSpacing), LIQUIDITY, LIQUIDITY),
        new Tick(NearestUsableTick.Find(TickMath.MAX_TICK, tickSpacing), -LIQUIDITY, LIQUIDITY),
    };

    public static V3Pool MakeV3Pool(Token tokenA, Token tokenB, FeeAmount fee = FeeAmount.MEDIUM)
    {
        int tickSpacing = UniswapSharp.V3.Constants.TICK_SPACINGS[fee];
        return new V3Pool(tokenA, tokenB, fee, EncodeSqrtRatioX96.Encode(1, 1), LIQUIDITY, 0, Ticks(tickSpacing));
    }

    public static V4Pool MakeV4Pool(BaseCurrency tokenA, BaseCurrency tokenB, FeeAmount fee = FeeAmount.MEDIUM)
    {
        const int tickSpacing = 60;
        return new V4Pool(tokenA, tokenB, (int)fee, tickSpacing, Constants.ZERO_ADDRESS,
            EncodeSqrtRatioX96.Encode(1, 1), LIQUIDITY, 0, Ticks(tickSpacing));
    }

    // use some sane defaults
    public static SwapOptions SwapOptions(
        UniswapSharp.V3.Payments.IFeeOptions? fee = null,
        FlatFeeOptions? flatFee = null,
        UniversalRouterVersion? urVersion = null,
        bool? safeMode = null,
        bool? useRouterBalance = null,
        Permit2Permit? inputTokenPermit = null,
        TokenTransferMode? tokenTransferMode = null,
        int? chainId = null,
        bool? nativeErc20Input = null,
        object? deadlineOrPreviousBlockhash = null,
        string? recipient = TEST_RECIPIENT_ADDRESS)
    {
        Percent slippage = new(5, 100);
        if (fee is not null)
        {
            slippage = slippage.Add(fee.Fee);
        }
        return new SwapOptions
        {
            SlippageTolerance = slippage,
            Recipient = recipient,
            Fee = fee,
            FlatFee = flatFee,
            UrVersion = urVersion,
            SafeMode = safeMode,
            UseRouterBalance = useRouterBalance,
            InputTokenPermit = inputTokenPermit,
            TokenTransferMode = tokenTransferMode,
            ChainId = chainId,
            NativeErc20Input = nativeErc20Input,
            DeadlineOrPreviousBlockhash = deadlineOrPreviousBlockhash,
        };
    }

    // alternative constructor to create a RouterTrade from protocol-specific sdk trades
    public static RouterTrade BuildTrade(IEnumerable<object> trades)
    {
        var list = trades.ToList();
        var v2 = new List<V2RouteAmounts<BaseCurrency, BaseCurrency>>();
        var v3 = new List<V3RouteAmounts<BaseCurrency, BaseCurrency>>();
        var v4 = new List<V4RouteAmounts<BaseCurrency, BaseCurrency>>();
        var mixed = new List<MixedRouteAmounts<BaseCurrency, BaseCurrency>>();
        TradeType? tradeType = null;

        foreach (var t in list)
        {
            switch (t)
            {
                case V2TradeT v2t:
                    v2.Add(new V2RouteAmounts<BaseCurrency, BaseCurrency>(v2t.Route, v2t.InputAmount, v2t.OutputAmount));
                    tradeType ??= v2t.TradeType;
                    break;
                case V3TradeT v3t:
                    v3.Add(new V3RouteAmounts<BaseCurrency, BaseCurrency>(v3t.Route, v3t.InputAmount, v3t.OutputAmount));
                    tradeType ??= v3t.TradeType;
                    break;
                case V4TradeT v4t:
                    v4.Add(new V4RouteAmounts<BaseCurrency, BaseCurrency>(v4t.Route, v4t.InputAmount, v4t.OutputAmount));
                    tradeType ??= v4t.TradeType;
                    break;
                case MixedTrade mt:
                    mixed.Add(new MixedRouteAmounts<BaseCurrency, BaseCurrency>(mt.Route, mt.InputAmount, mt.OutputAmount));
                    tradeType ??= mt.TradeType;
                    break;
                default:
                    throw new ArgumentException($"Unsupported trade type {t.GetType().Name}");
            }
        }

        return new RouterTrade(tradeType!.Value, v2, v3, v4, mixed);
    }
}
