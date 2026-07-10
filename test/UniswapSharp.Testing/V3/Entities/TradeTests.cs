using System.Numerics;
using System.Text.Json;
using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.V3.Entities;
using UniswapSharp.V3.Utils;
using Constants = UniswapSharp.V3.Constants;


#pragma warning disable xUnit1048

// ReSharper disable UnusedMember.Local

namespace UniswapSharp.Testing.V3.Entities;

public class TradeTests
{
    private static readonly Ether ETHER = Ether.OnChain(1);
    private static readonly Token token0 = new(1, "0x0000000000000000000000000000000000000001", 18, "t0", "token0");
    private static readonly Token token1 = new(1, "0x0000000000000000000000000000000000000002", 18, "t1", "token1");
    private static readonly Token token2 = new(1, "0x0000000000000000000000000000000000000003", 18, "t2", "token2");
    private static readonly Token token3 = new(1, "0x0000000000000000000000000000000000000004", 18, "t3", "token3");

    private readonly Pool pool_0_1 = V2StylePool(
        CurrencyAmount<Token>.FromRawAmount(token0, 100000),
        CurrencyAmount<Token>.FromRawAmount(token1, 100000)
    );

    private readonly Pool pool_0_2 = V2StylePool(
        CurrencyAmount<Token>.FromRawAmount(token0, 100000),
        CurrencyAmount<Token>.FromRawAmount(token2, 110000)
    );

    private readonly Pool pool_0_3 = V2StylePool(
        CurrencyAmount<Token>.FromRawAmount(token0, 100000),
        CurrencyAmount<Token>.FromRawAmount(token3, 90000)
    );

    private readonly Pool pool_1_2 = V2StylePool(
        CurrencyAmount<Token>.FromRawAmount(token1, 120000),
        CurrencyAmount<Token>.FromRawAmount(token2, 100000)
    );

    private readonly Pool pool_1_3 = V2StylePool(
        CurrencyAmount<Token>.FromRawAmount(token1, 120000),
        CurrencyAmount<Token>.FromRawAmount(token3, 130000)
    );

    private readonly Pool pool_weth_0 = V2StylePool(
        CurrencyAmount<Token>.FromRawAmount(Weth9.Tokens[1], BigInteger.Parse("100000")),
        CurrencyAmount<Token>.FromRawAmount(token0, BigInteger.Parse("100000"))
    );

    private readonly Pool pool_weth_1 = V2StylePool(
        CurrencyAmount<Token>.FromRawAmount(Weth9.Tokens[1], BigInteger.Parse("100000")),
        CurrencyAmount<Token>.FromRawAmount(token1, BigInteger.Parse("100000"))
    );

    private readonly Pool pool_weth_2 = V2StylePool(
        CurrencyAmount<Token>.FromRawAmount(Weth9.Tokens[1], BigInteger.Parse("100000")),
        CurrencyAmount<Token>.FromRawAmount(token2, BigInteger.Parse("100000"))
    );

    private readonly ITestOutputHelper testOutputHelper;

    public TradeTests(ITestOutputHelper testOutputHelper)
    {
        this.testOutputHelper = testOutputHelper;
    }

    private static Pool V2StylePool(CurrencyAmount<Token> reserve0, CurrencyAmount<Token> reserve1, Constants.FeeAmount feeAmount = Constants.FeeAmount.MEDIUM)
    {
        var sqrtRatioX96 = EncodeSqrtRatioX96.Encode(reserve1.Quotient, reserve0.Quotient);
        var liquidity = BigIntegerSquareRoot.NewtonPlusSqrt(reserve0.Quotient * reserve1.Quotient);
        return new Pool(
            reserve0.Currency,
            reserve1.Currency,
            feeAmount,
            sqrtRatioX96,
            liquidity,
            TickMath.GetTickAtSqrtRatio(sqrtRatioX96),
            new List<Tick>
            {
                new(
                    NearestUsableTick.Find(TickMath.MIN_TICK, Constants.TICK_SPACINGS[feeAmount]),
                    liquidity,
                    liquidity
                ),
                new(
                    NearestUsableTick.Find(TickMath.MAX_TICK, Constants.TICK_SPACINGS[feeAmount]),
                    -liquidity,
                    liquidity
                )
            }
        );
    }

    [Fact]
    public async void FromRoute_CanBeConstructedWithEtherAsInput()
    {
        var trade = await Trade<BaseCurrency, Token>.FromRoute(
            new Route<BaseCurrency, Token>([pool_weth_0], ETHER, token0),
            CurrencyAmount<BaseCurrency>.FromRawAmount(Ether.OnChain(1), BigInteger.Parse("10000")),
            TradeType.EXACT_INPUT
        );
        Assert.Equal(ETHER, trade.InputAmount.Currency);
        Assert.Equal(token0, trade.OutputAmount.Currency);
    }

    [Fact]
    public async void FromRoute_CanBeConstructedWithEtherAsInputForExactOutput()
    {
        var trade = await Trade<BaseCurrency, Token>.FromRoute(
            new Route<BaseCurrency, Token>([pool_weth_0], ETHER, token0),
            CurrencyAmount<Token>.FromRawAmount(token0, BigInteger.Parse("10000")),
            TradeType.EXACT_OUTPUT
        );
        Assert.Equal(ETHER, trade.InputAmount.Currency);
        Assert.Equal(token0, trade.OutputAmount.Currency);
    }

    [Fact]
    public async void FromRoute_CanBeConstructedWithEtherAsOutput()
    {
        var trade = await Trade<Token, BaseCurrency>.FromRoute(
            new Route<Token, BaseCurrency>([pool_weth_0], token0, ETHER),
            CurrencyAmount<BaseCurrency>.FromRawAmount(Ether.OnChain(1), BigInteger.Parse("10000")),
            TradeType.EXACT_OUTPUT
        );
        Assert.Equal(token0, trade.InputAmount.Currency);
        Assert.Equal(ETHER, trade.OutputAmount.Currency);
    }

    [Fact]
    public async void FromRoute_CanBeConstructedWithEtherAsOutputForExactInput()
    {
        var trade = await Trade<Token, BaseCurrency>.FromRoute(
            new Route<Token, BaseCurrency>([pool_weth_0], token0, ETHER),
            CurrencyAmount<BaseCurrency>.FromRawAmount(token0, BigInteger.Parse("10000")),
            TradeType.EXACT_INPUT
        );
        Assert.Equal(token0, trade.InputAmount.Currency);
        Assert.Equal(ETHER, trade.OutputAmount.Currency);
    }

    [Fact]
    public async void FromRoutes_CanBeConstructedWithEtherAsInputWithMultipleRoutes()
    {
        var trade = await Trade<Ether, Token>.FromRoutes<Ether>(
            [
                new ValueTuple<CurrencyAmount<Ether>, Route<Ether, Token>>(CurrencyAmount<Ether>.FromRawAmount(Ether.OnChain(1), BigInteger.Parse("10000")),
                    new Route<Ether, Token>([pool_weth_0], ETHER, token0))
            ]
            , TradeType.EXACT_INPUT);

        Assert.Equal(ETHER, trade.InputAmount.Currency);
        Assert.Equal(token0, trade.OutputAmount.Currency);
    }

    [Fact]
    public async void FromRoutes_CanBeConstructedWithEtherAsInputForExactOutputWithMultipleRoutes()
    {
        var trade = await Trade<Ether, Token>.FromRoutes<Token>(
            [
                new ValueTuple<CurrencyAmount<Token>, Route<Ether, Token>>(CurrencyAmount<Token>.FromRawAmount(token0, BigInteger.Parse("3000")),
                    new Route<Ether, Token>([pool_weth_0], ETHER, token0)),
                new ValueTuple<CurrencyAmount<Token>, Route<Ether, Token>>(CurrencyAmount<Token>.FromRawAmount(token0, BigInteger.Parse("7000")),
                    new Route<Ether, Token>([pool_weth_1, pool_0_1], ETHER, token0))
            ]
            , TradeType.EXACT_OUTPUT);


        Assert.Equal(ETHER, trade.InputAmount.Currency);
        Assert.Equal(token0, trade.OutputAmount.Currency);
    }

    [Fact]
    public async void FromRoutes_CanBeConstructedWithEtherAsOutputWithMultipleRoutes()
    {
        var trade = await Trade<BaseCurrency, BaseCurrency>.FromRoutes<Ether>(
            [
                new ValueTuple<CurrencyAmount<Ether>, Route<BaseCurrency, BaseCurrency>>(
                    CurrencyAmount<BaseCurrency>.FromRawAmount(Ether.OnChain(1), BigInteger.Parse("4000")),
                    new Route<BaseCurrency, BaseCurrency>([pool_weth_0], token0, ETHER)),
                new ValueTuple<CurrencyAmount<Ether>, Route<BaseCurrency, BaseCurrency>>(
                    CurrencyAmount<BaseCurrency>.FromRawAmount(Ether.OnChain(1), BigInteger.Parse("6000")),
                    new Route<BaseCurrency, BaseCurrency>([pool_0_1, pool_weth_1], token0, ETHER))
            ]
            , TradeType.EXACT_OUTPUT);


        Assert.Equal(token0, trade.InputAmount.Currency);
        Assert.Equal(ETHER, trade.OutputAmount.Currency);
    }

    [Fact]
    public async void FromRoutes_CanBeConstructedWithEtherAsOutputForExactInputWithMultipleRoutes()
    {
        var trade = await Trade<Token, Ether>.FromRoutes(
            [
                new ValueTuple<CurrencyAmount<Token>, Route<Token, Ether>>(
                    CurrencyAmount<Token>.FromRawAmount(token0, BigInteger.Parse("3000")),
                    new Route<Token, Ether>([pool_weth_0], token0, ETHER)),
                new ValueTuple<CurrencyAmount<Token>, Route<Token, Ether>>(
                    CurrencyAmount<BaseCurrency>.FromRawAmount(token0, BigInteger.Parse("7000")),
                    new Route<Token, Ether>([pool_0_1, pool_weth_1], token0, ETHER))
            ]
            , TradeType.EXACT_INPUT);


        Assert.Equal(token0, trade.InputAmount.Currency);
        Assert.Equal(ETHER, trade.OutputAmount.Currency);
    }

    [Fact]
    public async void FromRoutes_ThrowsIfPoolsAreReUsedBetweenRoutes()
    {
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await Trade<Token, Ether>.FromRoutes(
                [
                    new ValueTuple<CurrencyAmount<Token>, Route<Token, Ether>>(
                        CurrencyAmount<Token>.FromRawAmount(token0, BigInteger.Parse("4500")),
                        new Route<Token, Ether>([pool_0_1, pool_weth_1], token0, ETHER)),
                    new ValueTuple<CurrencyAmount<Token>, Route<Token, Ether>>(
                        CurrencyAmount<BaseCurrency>.FromRawAmount(token0, BigInteger.Parse("7000")),
                        new Route<Token, Ether>([pool_0_1, pool_1_2, pool_weth_2], token0, ETHER))
                ]
                , TradeType.EXACT_INPUT);
        });
    }

    [Fact]
    public void CreateUncheckedTrade_ThrowsIfInputCurrencyDoesNotMatchRoute()
    {
        Assert.Throws<ArgumentException>(() =>
            Trade<Token, Token>.CreateUncheckedTrade( new RouteInput<Token, Token>(){
                Route = new Route<Token, Token>([pool_0_1], token0, token1),
               InputAmount = CurrencyAmount<Token>.FromRawAmount(token2, 10000),
               OutputAmount = CurrencyAmount<Token>.FromRawAmount(token1, 10000)
                },
                TradeType.EXACT_INPUT
            )
        );
    }

    [Fact]
    public void CreateUncheckedTrade_ThrowsIfOutputCurrencyDoesNotMatchRoute()
    {
        Assert.Throws<ArgumentException>(() =>
            Trade<Token, Token>.CreateUncheckedTrade
            (new RouteInput<Token, Token>(){
                    Route = new Route<Token, Token>([pool_0_1], token0, token1),
                    InputAmount = CurrencyAmount<Token>.FromRawAmount(token0, 10000),
                    OutputAmount = CurrencyAmount<Token>.FromRawAmount(token2, 10000)
                },
                TradeType.EXACT_INPUT
            )
        );
    }

    [Fact]
    public void CreateUncheckedTrade_CanCreateAnExactInputTradeWithoutSimulating()
    {
        Trade<Token, Token>.CreateUncheckedTrade(
            new RouteInput<Token, Token>(){
                Route = new Route<Token, Token>([pool_0_1], token0, token1),
                InputAmount = CurrencyAmount<Token>.FromRawAmount(token0, 10000),
                OutputAmount = CurrencyAmount<Token>.FromRawAmount(token1, 100000)
            },
            TradeType.EXACT_INPUT
        );
    }

    [Fact]
    public void CreateUncheckedTrade_CanCreateAnExactOutputTradeWithoutSimulating()
    {
        Trade<Token, Token>.CreateUncheckedTrade(
            new RouteInput<Token, Token>(){
                Route = new Route<Token, Token>([pool_0_1], token0, token1),
                InputAmount = CurrencyAmount<Token>.FromRawAmount(token0, 10000),
                OutputAmount = CurrencyAmount<Token>.FromRawAmount(token1, 100000)}
            ,
            TradeType.EXACT_OUTPUT
        );
    }

    [Fact]
    public void CreateUncheckedTradeWithMultipleRoutes_ThrowsIfInputCurrencyDoesNotMatchRouteWithMultipleRoutes()
    {
        Assert.Throws<ArgumentException>(() =>
            Trade<Token, Token>.CreateUncheckedTradeWithMultipleRoutes(
                [
                    new RouteInput<Token, Token>
                    {
                        Route = new Route<Token, Token>([pool_1_2], token2, token1),
                        InputAmount = CurrencyAmount<Token>.FromRawAmount(token2, 2000),
                        OutputAmount = CurrencyAmount<Token>.FromRawAmount(token1, 2000)
                    },
                    new RouteInput<Token, Token>
                    {
                        Route = new Route<Token, Token>([pool_0_1], token0, token1),
                        InputAmount = CurrencyAmount<Token>.FromRawAmount(token2, 8000),
                        OutputAmount = CurrencyAmount<Token>.FromRawAmount(token1, 8000)
                    }
                ],
                TradeType.EXACT_INPUT
            )
        );
    }


    [Fact]
    public void CreateUncheckedTradeWithMultipleRoutes_ThrowsIfOutputCurrencyDoesNotMatchRouteWithMultipleRoutes()
    {
        Assert.Throws<ArgumentException>(() =>
            Trade<Token, Token>.CreateUncheckedTradeWithMultipleRoutes(
                [
                    new RouteInput<Token, Token>
                    {
                        Route = new Route<Token, Token>([pool_0_2], token0, token2),
                        InputAmount = CurrencyAmount<Token>.FromRawAmount(token0, 10000),
                        OutputAmount = CurrencyAmount<Token>.FromRawAmount(token2, 10000)
                    },
                    new RouteInput<Token, Token>
                    {
                        Route = new Route<Token, Token>([pool_0_1], token0, token1),
                        InputAmount = CurrencyAmount<Token>.FromRawAmount(token0, 10000),
                        OutputAmount = CurrencyAmount<Token>.FromRawAmount(token2, 10000)
                    }
                ],
                TradeType.EXACT_INPUT
            )
        );
    }

    [Fact]
    public void CreateUncheckedTradeWithMultipleRoutes_CanCreateAnExactInputTradeWithoutSimulatingWithMultipleRoutes()
    {
        Trade<Token,Token>.CreateUncheckedTradeWithMultipleRoutes([
                new RouteInput<Token,Token>
                {
                    Route = new Route<Token,Token>([pool_0_1], token0, token1),
                    InputAmount = CurrencyAmount<Token>.FromRawAmount(token0, 5000),
                    OutputAmount = CurrencyAmount<Token>.FromRawAmount(token1, 50000),
                },
                new RouteInput<Token,Token>
                {
                    Route = new Route<Token,Token>([pool_0_2, pool_1_2], token0, token1),
                    InputAmount = CurrencyAmount<Token>.FromRawAmount(token0, 5000),
                    OutputAmount = CurrencyAmount<Token>.FromRawAmount(token1, 50000),
                }
                ],
             TradeType.EXACT_INPUT);
    }

    [Fact]
    public void CreateUncheckedTradeWithMultipleRoutes_CanCreateAnExactOutputTradeWithoutSimulatingWithMultipleRoutes()
    {
        Trade<Token, Token>.CreateUncheckedTradeWithMultipleRoutes([
                new RouteInput<Token,Token>
                {
                    Route = new Route<Token,Token>([pool_0_1], token0, token1),
                    InputAmount = CurrencyAmount<Token>.FromRawAmount(token0, 5001),
                    OutputAmount = CurrencyAmount<Token>.FromRawAmount(token1, 50000),
                },
                new RouteInput<Token,Token>
                {
                    Route = new Route<Token,Token>([pool_0_2, pool_1_2], token0, token1),
                    InputAmount = CurrencyAmount<Token>.FromRawAmount(token0, 4999),
                    OutputAmount = CurrencyAmount<Token>.FromRawAmount(token1, 50000),
                }
            ],
       TradeType.EXACT_OUTPUT
        );
    }


    [Fact]
    public void RouteAndSwaps()
    {
        var singleRoute = Trade<Token, Token>.CreateUncheckedTrade(
            new RouteInput<Token, Token>(){
            Route=new Route<Token, Token>([pool_0_1, pool_1_2], token0, token2),
            InputAmount = CurrencyAmount<Token>.FromRawAmount(token0, 100),
            OutputAmount = CurrencyAmount<Token>.FromRawAmount(token2, 69)},
            TradeType.EXACT_INPUT
        );

        var multiRoute = Trade<Token, Token>.CreateUncheckedTradeWithMultipleRoutes(
            [
                new RouteInput<Token, Token>
                {
                    Route = new Route<Token, Token>([pool_0_1, pool_1_2], token0, token2),
                    InputAmount = CurrencyAmount<Token>.FromRawAmount(token0, 50),
                    OutputAmount = CurrencyAmount<Token>.FromRawAmount(token2, 35)
                },
                new RouteInput<Token, Token>
                {
                    Route = new Route<Token, Token>([pool_0_2], token0, token2),
                    InputAmount = CurrencyAmount<Token>.FromRawAmount(token0, 50),
                    OutputAmount = CurrencyAmount<Token>.FromRawAmount(token2, 34)
                }
            ],
            TradeType.EXACT_INPUT
        );

        Assert.NotNull(singleRoute.Route);
        Assert.NotNull(singleRoute.Swaps);
        Assert.Single(singleRoute.Swaps);
        Assert.NotNull(multiRoute.Swaps);
        Assert.Equal(2, multiRoute.Swaps.Count);
        Assert.Throws<InvalidOperationException>(() =>
        {
            var _ = multiRoute.Route;
        });
    }

    [Fact]
    public void WorstExecutionPrice()
    {
        // Test for EXACT_INPUT
        var exactIn = Trade<Token,Token>.CreateUncheckedTrade(new RouteInput<Token, Token>()
        {
            Route = new Route<Token, Token>(new List<Pool> { pool_0_1, pool_1_2 }, token0, token2),
            InputAmount = CurrencyAmount<Token>.FromRawAmount(token0, 100),
            OutputAmount = CurrencyAmount<Token>.FromRawAmount(token2, 69),
     
        }, TradeType.EXACT_INPUT);

        var exactInMultiRoute = Trade<Token, Token>.CreateUncheckedTradeWithMultipleRoutes(
            [
               new RouteInput<Token, Token>
               {
                   Route = new Route<Token,Token>(new List<Pool> { pool_0_1, pool_1_2 }, token0, token2),
                   InputAmount = CurrencyAmount<Token>.FromRawAmount(token0, 50),
                   OutputAmount = CurrencyAmount<Token>.FromRawAmount(token2, 35),
               },
               new RouteInput<Token, Token>
               {
                   Route = new Route<Token,Token>(new List<Pool> { pool_0_2 }, token0, token2),
                   InputAmount = CurrencyAmount<Token>.FromRawAmount(token0, 50),
                   OutputAmount = CurrencyAmount<Token>.FromRawAmount(token2, 34),
               }
           ],
         TradeType.EXACT_INPUT);

        Assert.Throws<ArgumentException>(() => exactIn.MinimumAmountOut(new Percent(-1, 100)));
        Assert.Equal(exactIn.ExecutionPrice, exactIn.WorstExecutionPrice(new Percent(0, 100)));
        Assert.Equal(new Price<Token, Token>(token0, token2, 100, 69), exactIn.WorstExecutionPrice(new Percent(0, 100)));
        Assert.Equal(new Price<Token, Token>(token0, token2, 100, 65), exactIn.WorstExecutionPrice(new Percent(5, 100)));
        Assert.Equal(new Price<Token, Token>(token0, token2, 100, 23), exactIn.WorstExecutionPrice(new Percent(200, 100)));

        Assert.Equal(new Price<Token, Token>(token0, token2, 100, 69), exactInMultiRoute.WorstExecutionPrice(new Percent(0, 100)));
        Assert.Equal(new Price<Token, Token>(token0, token2, 100, 65), exactInMultiRoute.WorstExecutionPrice(new Percent(5, 100)));
        Assert.Equal(new Price<Token, Token>(token0, token2, 100, 23), exactInMultiRoute.WorstExecutionPrice(new Percent(200, 100)));

        // Test for EXACT_OUTPUT
        var exactOut = Trade<Token, Token>.CreateUncheckedTrade(new RouteInput<Token, Token>()
        {
            Route = new Route<Token, Token>(new List<Pool> { pool_0_1, pool_1_2 }, token0, token2),
            InputAmount = CurrencyAmount<Token>.FromRawAmount(token0, 156),
            OutputAmount = CurrencyAmount<Token>.FromRawAmount(token2, 100),
      
        },  TradeType.EXACT_OUTPUT);

        var exactOutMultiRoute = Trade<Token, Token>.CreateUncheckedTradeWithMultipleRoutes(

   [             new RouteInput<Token, Token>
                {
                    Route = new Route<Token,Token>(new List<Pool> { pool_0_1, pool_1_2 }, token0, token2),
                    InputAmount = CurrencyAmount<Token>.FromRawAmount(token0, 78),
                    OutputAmount = CurrencyAmount<Token>.FromRawAmount(token2, 50),
                },
                new RouteInput<Token, Token>
                {
                    Route = new Route<Token,Token>(new List<Pool> { pool_0_2 }, token0, token2),
                    InputAmount = CurrencyAmount<Token>.FromRawAmount(token0, 78),
                    OutputAmount = CurrencyAmount<Token>.FromRawAmount(token2, 50),
                }]
            ,
          TradeType.EXACT_OUTPUT
        );

        Assert.Throws<ArgumentException>(() => exactOut.WorstExecutionPrice(new Percent(-1, 100)));
        Assert.Equal(exactOut.ExecutionPrice, exactOut.WorstExecutionPrice(new Percent(0, 100)));
        Assert.True(exactOut.WorstExecutionPrice(new Percent(0, 100)).Equals(new Price<Token, Token>(token0, token2, 156, 100)));
        Assert.True(exactOut.WorstExecutionPrice(new Percent(5, 100)).Equals(new Price<Token, Token>(token0, token2, 163, 100)));
        Assert.True(exactOut.WorstExecutionPrice(new Percent(200, 100)).Equals(new Price<Token, Token>(token0, token2, 468, 100)));

        Assert.True(exactOutMultiRoute.WorstExecutionPrice(new Percent(0, 100)).Equals(new Price<Token, Token>(token0, token2, 156, 100)));
        Assert.True(exactOutMultiRoute.WorstExecutionPrice(new Percent(5, 100)).Equals(new Price<Token, Token>(token0, token2, 163, 100)));
        Assert.True(exactOutMultiRoute.WorstExecutionPrice(new Percent(200, 100)).Equals(new Price<Token, Token>(token0, token2, 468, 100)));
    }

    [Fact]
    public void PriceImpact()
    {
        // Test for EXACT_INPUT
        var exactIn = Trade<Token, Token>.CreateUncheckedTradeWithMultipleRoutes(
  [            
      new RouteInput<Token, Token>
                {
                    Route = new Route<Token,Token>(new List<Pool> { pool_0_1, pool_1_2 }, token0, token2),
                    InputAmount = CurrencyAmount<Token>.FromRawAmount(token0, 100),
                    OutputAmount = CurrencyAmount<Token>.FromRawAmount(token2, 69)
            }
],
             TradeType.EXACT_INPUT
        );

        var exactInMultipleRoutes = Trade<Token, Token>.CreateUncheckedTradeWithMultipleRoutes(
   [             new  RouteInput<Token, Token>
                {
                    Route = new Route<Token,Token>(new List<Pool> { pool_0_1, pool_1_2 }, token0, token2),
                    InputAmount = CurrencyAmount<Token>.FromRawAmount(token0, 90),
                    OutputAmount = CurrencyAmount<Token>.FromRawAmount(token2, 62),
                },
                new  RouteInput<Token, Token>
                {
                    Route = new Route<Token,Token>(new List<Pool> { pool_0_2 }, token0, token2),
                    InputAmount = CurrencyAmount<Token>.FromRawAmount(token0, 10),
                    OutputAmount = CurrencyAmount<Token>.FromRawAmount(token2, 7),
                }]
            ,
        TradeType.EXACT_INPUT
        );

        Assert.Equal("17.2", exactIn.PriceImpact.ToSignificant(3));
        Assert.Equal("19.8", exactInMultipleRoutes.PriceImpact.ToSignificant(3));

        // Test for EXACT_OUTPUT
        var exactOut = Trade<Token, Token>.CreateUncheckedTradeWithMultipleRoutes(

    [            new  RouteInput<Token, Token>
                {
                    Route = new Route<Token,Token>(new List<Pool> { pool_0_1, pool_1_2 }, token0, token2),
                    InputAmount = CurrencyAmount<Token>.FromRawAmount(token0, 156),
                    OutputAmount = CurrencyAmount<Token>.FromRawAmount(token2, 100),
                }]
           ,
     TradeType.EXACT_OUTPUT
        );

        var exactOutMultipleRoutes = Trade<Token, Token>.CreateUncheckedTradeWithMultipleRoutes(
[                new  RouteInput<Token, Token>
                {
                    Route = new Route<Token,Token>(new List<Pool> { pool_0_1, pool_1_2 }, token0, token2),
                    InputAmount = CurrencyAmount<Token>.FromRawAmount(token0, 140),
                    OutputAmount = CurrencyAmount<Token>.FromRawAmount(token2, 90),
                },
                new  RouteInput<Token, Token>
                {
                    Route = new Route<Token,Token>(new List<Pool> { pool_0_2 }, token0, token2),
                    InputAmount = CurrencyAmount<Token>.FromRawAmount(token0, 16),
                    OutputAmount = CurrencyAmount<Token>.FromRawAmount(token2, 10),
                }]
           ,
       TradeType.EXACT_OUTPUT
        );

        Assert.Equal("23.1", exactOut.PriceImpact.ToSignificant(3));
        Assert.Equal("25.5", exactOutMultipleRoutes.PriceImpact.ToSignificant(3));
    }

    [Fact]
    public async Task BestTradeExactIn_ThrowsWithEmptyPools()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Trade<Token, Token>.BestTradeExactIn([], CurrencyAmount<Token>.FromRawAmount(token0, 10000), token2)
        );
    }

    [Fact]
    public async Task BestTradeExactIn_ThrowsWithMaxHopsOf0()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Trade<Token, Token>.BestTradeExactIn([pool_0_2], CurrencyAmount<Token>.FromRawAmount(token0, 10000), token2,
                new Trade<Token, Token>.BestTradeOptions { MaxHops = 0 })
        );
    }

    public static string JsonPrettify(object obj)
    {
        return JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true });
    }

    [Fact]
    public async Task BestTradeExactIn_ProvidesBestRoute()
    {
        var result = await Trade<Token, Token>.BestTradeExactIn([pool_0_1, pool_0_2, pool_1_2],
            CurrencyAmount<Token>.FromRawAmount(token0, 10000), token2);

        Assert.Equal(2, result.Count);
        Assert.Single(result[0].Swaps[0].Route.Pools);
        Assert.Equal([token0, token2], result[0].Swaps[0].Route.TokenPath);
        Assert.True(result[0].InputAmount.Equals(CurrencyAmount<Token>.FromRawAmount(token0, 10000)));
        Assert.True(result[0].OutputAmount.Equals(CurrencyAmount<Token>.FromRawAmount(token2, 9971)));
        Assert.Equal(2, result[1].Swaps[0].Route.Pools.Count);
        Assert.Equal([token0, token1, token2], result[1].Swaps[0].Route.TokenPath);
        Assert.True(result[1].InputAmount.Equals(CurrencyAmount<Token>.FromRawAmount(token0, 10000)));
        Assert.True(result[1].OutputAmount.Equals(CurrencyAmount<Token>.FromRawAmount(token2, 7004)));
    }

    [Fact]
    public async Task BestTradeExactIn_RespectsMaxHops()
    {
        var result = await Trade<Token, Token>.BestTradeExactIn([pool_0_1, pool_0_2, pool_1_2], CurrencyAmount<Token>.FromRawAmount(token0, 10), token2, new Trade<Token, Token>.BestTradeOptions() { MaxHops = 1 });
        Assert.Single(result);
        Assert.Single( result[0].Swaps[0].Route.Pools);
        Assert.Equal([token0, token2], result[0].Swaps[0].Route.TokenPath);
    }

    [Fact]
    public async Task BestTradeExactIn_InsufficientInputForOnePool()
    {
        var result = await Trade<Token, Token>.BestTradeExactIn([pool_0_1, pool_0_2, pool_1_2], CurrencyAmount<Token>.FromRawAmount(token0, 1), token2);

        Assert.Equal(2, result.Count);
        Assert.Single(result[0].Swaps[0].Route.Pools);
        Assert.Equal([token0, token2], result[0].Swaps[0].Route.TokenPath);
        Assert.Equal(CurrencyAmount<Token>.FromRawAmount(token2, 0), result[0].OutputAmount);
    }

    [Fact]
    public async Task BestTradeExactIn_RespectsN()
    {
        var result = await Trade<Token, Token>.BestTradeExactIn([pool_0_1, pool_0_2, pool_1_2], CurrencyAmount<Token>.FromRawAmount(token0, 10), token2, new Trade<Token, Token>.BestTradeOptions() { MaxHops = 1 });
        Assert.Single(result);
    }

    [Fact]
    public async Task BestTradeExactIn_NoPath()
    {
        var result = await Trade<Token, Token>.BestTradeExactIn([pool_0_1, pool_0_3, pool_1_3],
            CurrencyAmount<Token>.FromRawAmount(token0, 10), token2);
        Assert.Empty(result);
    }

    [Fact]
    public async Task BestTradeExactIn_WorksForEtherCurrencyInput()
    {
        var result = await Trade<Ether, Token>.BestTradeExactIn([pool_weth_0, pool_0_1, pool_0_3, pool_1_3],
            CurrencyAmount<Token>.FromRawAmount(Ether.OnChain(1), 100), token3);
        Assert.Equal(2, result.Count);
        Assert.Equal(ETHER, result[0].InputAmount.Currency);
        Assert.Equal([Weth9.Tokens[1], token0, token1, token3], result[0].Swaps[0].Route.TokenPath);
        Assert.Equal(token3, result[0].OutputAmount.Currency);
        Assert.Equal(ETHER, result[1].InputAmount.Currency);
        Assert.Equal([Weth9.Tokens[1], token0, token3], result[1].Swaps[0].Route.TokenPath);
        Assert.Equal(token3, result[1].OutputAmount.Currency);
    }

    [Fact]
    public async Task BestTradeExactIn_WorksForEtherCurrencyOutput()
    {
        var result = await Trade<Token, Ether>.BestTradeExactIn([pool_weth_0, pool_0_1, pool_0_3, pool_1_3],
            CurrencyAmount<Token>.FromRawAmount(token3, 100), ETHER);
        Assert.Equal(2, result.Count);
        Assert.Equal(token3, result[0].InputAmount.Currency);
        Assert.Equal([token3, token0, Weth9.Tokens[1]], result[0].Swaps[0].Route.TokenPath);
        Assert.Equal(ETHER, result[0].OutputAmount.Currency);
        Assert.Equal(token3, result[1].InputAmount.Currency);
        Assert.Equal([token3, token1, token0, Weth9.Tokens[1]], result[1].Swaps[0].Route.TokenPath);
        Assert.Equal(ETHER, result[1].OutputAmount.Currency);
    }

    [Fact]
    public async Task MaximumAmountIn_ThrowsIfLessThan0()
    {
        var exactIn = await Trade<Token, Token>.FromRoute(new Route<Token, Token>([pool_0_1, pool_1_2], token0, token2),
            CurrencyAmount<Token>.FromRawAmount(token0, 100), TradeType.EXACT_INPUT);
        Assert.Throws<ArgumentException>(() => exactIn.MaximumAmountIn(new Percent(-1, 100)));
    }

    [Fact]
    public async Task MaximumAmountIn_ReturnsExactIf0()
    {
        var exactIn = await Trade<Token, Token>.FromRoute(new Route<Token, Token>([pool_0_1, pool_1_2], token0, token2),
            CurrencyAmount<Token>.FromRawAmount(token0, 100), TradeType.EXACT_INPUT);
        Assert.Equal(exactIn.InputAmount, exactIn.MaximumAmountIn(new Percent(0, 100)));
    }

    [Fact]
    public async Task MaximumAmountIn_ReturnsExactIfNonzero()
    {
        var exactIn = await Trade<Token, Token>.FromRoute(new Route<Token, Token>([pool_0_1, pool_1_2], token0, token2),
            CurrencyAmount<Token>.FromRawAmount(token0, 100), TradeType.EXACT_INPUT);
        Assert.True(exactIn.MaximumAmountIn(new Percent(0, 100)).Equals(CurrencyAmount<Token>.FromRawAmount(token0, 100)));
        Assert.True(exactIn.MaximumAmountIn(new Percent(5, 100)).Equals(CurrencyAmount<Token>.FromRawAmount(token0, 100)));
        Assert.True(exactIn.MaximumAmountIn(new Percent(200, 100)).Equals(CurrencyAmount<Token>.FromRawAmount(token0, 100)));
    }

    [Fact]
    public async Task MaximumAmountIn_ExactOutput_ThrowsIfLessThan0()
    {
        var exactOut = await Trade<Token, Token>.FromRoute(new Route<Token, Token>([pool_0_1, pool_1_2], token0, token2),
            CurrencyAmount<Token>.FromRawAmount(token2, 10000), TradeType.EXACT_OUTPUT);
        Assert.Throws<ArgumentException>(() => exactOut.MaximumAmountIn(new Percent(-1, 10000)));
    }

    [Fact]
    public async Task MaximumAmountIn_ExactOutput_ReturnsExactIf0()
    {
        var exactOut = await Trade<Token, Token>.FromRoute(new Route<Token, Token>([pool_0_1, pool_1_2], token0, token2),
            CurrencyAmount<Token>.FromRawAmount(token2, 10000), TradeType.EXACT_OUTPUT);
        Assert.Equal(exactOut.InputAmount, exactOut.MaximumAmountIn(new Percent(0, 10000)));
    }

    [Fact]
    public async Task MaximumAmountIn_ExactOutput_ReturnsSlippageAmountIfNonzero()
    {
        var exactOut = await Trade<Token, Token>.FromRoute(new Route<Token, Token>([pool_0_1, pool_1_2], token0, token2),
            CurrencyAmount<Token>.FromRawAmount(token2, 10000), TradeType.EXACT_OUTPUT);
        Assert.True(exactOut.MaximumAmountIn(new Percent(0, 100)).Equals(CurrencyAmount<Token>.FromRawAmount(token0, 15488)));
        Assert.True(exactOut.MaximumAmountIn(new Percent(5, 100)).Equals(CurrencyAmount<Token>.FromRawAmount(token0, 16262)));
        Assert.True(exactOut.MaximumAmountIn(new Percent(200, 100)).Equals(CurrencyAmount<Token>.FromRawAmount(token0, 46464)));
    }

    [Fact]
    public async Task MinimumAmountOut_ThrowsIfLessThan0()
    {
        var exactIn = await Trade<Token, Token>.FromRoute(new Route<Token, Token>([pool_0_1, pool_1_2], token0, token2),
            CurrencyAmount<Token>.FromRawAmount(token0, 10000), TradeType.EXACT_INPUT);
        Assert.Throws<ArgumentException>(() => exactIn.MinimumAmountOut(new Percent(-1, 100)));
    }

    [Fact]
    public async Task MinimumAmountOut_ReturnsExactIf0()
    {
        var exactIn = await Trade<Token, Token>.FromRoute(new Route<Token, Token>([pool_0_1, pool_1_2], token0, token2),
            CurrencyAmount<Token>.FromRawAmount(token0, 10000), TradeType.EXACT_INPUT);
        Assert.Equal(exactIn.OutputAmount, exactIn.MinimumAmountOut(new Percent(0, 10000)));
    }

    [Fact]
    public async Task MinimumAmountOut_ReturnsExactIfNonzero()
    {
        var exactIn = await Trade<Token, Token>.FromRoute(new Route<Token, Token>([pool_0_1, pool_1_2], token0, token2),
            CurrencyAmount<Token>.FromRawAmount(token0, 10000), TradeType.EXACT_INPUT);
        Assert.Equal(CurrencyAmount<Token>.FromRawAmount(token2, 7004), exactIn.MinimumAmountOut(new Percent(0, 100)));
        Assert.Equal(CurrencyAmount<Token>.FromRawAmount(token2, 6670), exactIn.MinimumAmountOut(new Percent(5, 100)));
        Assert.Equal(CurrencyAmount<Token>.FromRawAmount(token2, 2334), exactIn.MinimumAmountOut(new Percent(200, 100)));
    }

    [Fact]
    public async Task MinimumAmountOut_ExactOutput_ThrowsIfLessThan0()
    {
        var exactOut = await Trade<Token, Token>.FromRoute(new Route<Token, Token>([pool_0_1, pool_1_2], token0, token2),
            CurrencyAmount<Token>.FromRawAmount(token2, 100), TradeType.EXACT_OUTPUT);
        Assert.Throws<ArgumentException>(() => exactOut.MinimumAmountOut(new Percent(-1, 100)));
    }

    [Fact]
    public async Task MinimumAmountOut_ExactOutput_ReturnsExactIf0()
    {
        var exactOut = await Trade<Token, Token>.FromRoute(new Route<Token, Token>([pool_0_1, pool_1_2], token0, token2),
            CurrencyAmount<Token>.FromRawAmount(token2, 100), TradeType.EXACT_OUTPUT);
        Assert.Equal(exactOut.OutputAmount, exactOut.MinimumAmountOut(new Percent(0, 100)));
    }

    [Fact]
    public async Task MinimumAmountOut_ExactOutput_ReturnsSlippageAmountIfNonzero()
    {
        var exactOut = await Trade<Token, Token>.FromRoute(new Route<Token, Token>([pool_0_1, pool_1_2], token0, token2),
            CurrencyAmount<Token>.FromRawAmount(token2, 100), TradeType.EXACT_OUTPUT);
        Assert.True(exactOut.MinimumAmountOut(new Percent(0, 100)).Equals(CurrencyAmount<Token>.FromRawAmount(token2, 100)));
        Assert.True(exactOut.MinimumAmountOut(new Percent(5, 100)).Equals(CurrencyAmount<Token>.FromRawAmount(token2, 100)));
        Assert.True(exactOut.MinimumAmountOut(new Percent(200, 100)).Equals(CurrencyAmount<Token>.FromRawAmount(token2, 100)));
    }

    [Fact]
    public async Task BestTradeExactOut_ThrowsWithEmptyPools()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Trade<Token, Token>.BestTradeExactOut([], token0, CurrencyAmount<Token>.FromRawAmount(token2, 100))
        );
    }

    [Fact]
    public async Task BestTradeExactOut_ThrowsWithMaxHopsOf0()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Trade<Token, Token>.BestTradeExactOut([pool_0_2], token0, CurrencyAmount<Token>.FromRawAmount(token2, 100),
                new Trade<Token, Token>.BestTradeOptions { MaxHops = 0 })
        );
    }

    [Fact]
    public async Task BestTradeExactOut_ProvidesBestRoute()
    {
        var result = await Trade<Token, Token>.BestTradeExactOut([pool_0_1, pool_0_2, pool_1_2], token0,
            CurrencyAmount<Token>.FromRawAmount(token2, 10000));
        Assert.Equal(2, result.Count);
        Assert.Equal(1, result[0].Swaps[0].Route.Pools.Count);
        Assert.Equal([token0, token2], result[0].Swaps[0].Route.TokenPath);
        Assert.True(result[0].InputAmount.Equals(CurrencyAmount<Token>.FromRawAmount(token0, 10032)));
        Assert.True(result[0].OutputAmount.Equals(CurrencyAmount<Token>.FromRawAmount(token2, 10000)));
        Assert.Equal(2, result[1].Swaps[0].Route.Pools.Count);
        Assert.Equal([token0, token1, token2], result[1].Swaps[0].Route.TokenPath);
        Assert.True(result[1].InputAmount.Equals(CurrencyAmount<Token>.FromRawAmount(token0, 15488)));
        Assert.True(result[1].OutputAmount.Equals(CurrencyAmount<Token>.FromRawAmount(token2, 10000)));
    }

    [Fact]
    public async Task BestTradeExactOut_RespectsMaxHops()
    {
        var result = await Trade<Token, Token>.BestTradeExactOut([pool_0_1, pool_0_2, pool_1_2], token0, CurrencyAmount<Token>.FromRawAmount(token2, 10), new Trade<Token, Token>.BestTradeOptions() {MaxHops= 1});
        Assert.Single(result);
        Assert.Single(result[0].Swaps[0].Route.Pools);
        Assert.Equal([token0, token2], result[0].Swaps[0].Route.TokenPath);
    }

    [Fact]
    public async Task BestTradeExactOut_RespectsN()
    {
        var result = await Trade<Token, Token>.BestTradeExactOut([pool_0_1, pool_0_2, pool_1_2], token0, CurrencyAmount<Token>.FromRawAmount(token2, 10), new Trade<Token, Token>.BestTradeOptions() { MaxHops = 1 });
        Assert.Single(result);
    }

    [Fact]
    public async Task BestTradeExactOut_NoPath()
    {
        var result = await Trade<Token, Token>.BestTradeExactOut([pool_0_1, pool_0_3, pool_1_3], token0,
            CurrencyAmount<Token>.FromRawAmount(token2, 10));
        Assert.Empty(result);
    }

    [Fact]
    public async Task BestTradeExactOut_WorksForEtherCurrencyInput()
    {
        var result = await Trade<Ether, Token>.BestTradeExactOut([pool_weth_0, pool_0_1, pool_0_3, pool_1_3], ETHER,
            CurrencyAmount<Token>.FromRawAmount(token3, 10000));
        Assert.Equal(2, result.Count);
        Assert.Equal(ETHER, result[0].InputAmount.Currency);
        Assert.Equal([Weth9.Tokens[1], token0, token1, token3], result[0].Swaps[0].Route.TokenPath);
        Assert.Equal(token3, result[0].OutputAmount.Currency);
        Assert.Equal(ETHER, result[1].InputAmount.Currency);
        Assert.Equal([Weth9.Tokens[1], token0, token3], result[1].Swaps[0].Route.TokenPath);
        Assert.Equal(token3, result[1].OutputAmount.Currency);
    }

    [Fact]
    public async Task BestTradeExactOut_WorksForEtherCurrencyOutput()
    {
        var result = await Trade<Token, Ether>.BestTradeExactOut([pool_weth_0, pool_0_1, pool_0_3, pool_1_3], token3,
            CurrencyAmount<Token>.FromRawAmount(Ether.OnChain(1), 100));
        Assert.Equal(2, result.Count);
        Assert.Equal(token3, result[0].InputAmount.Currency);
        Assert.Equal([token3, token0, Weth9.Tokens[1]], result[0].Swaps[0].Route.TokenPath);
        Assert.Equal(ETHER, result[0].OutputAmount.Currency);
        Assert.Equal(token3, result[1].InputAmount.Currency);
        Assert.Equal([token3, token1, token0, Weth9.Tokens[1]], result[1].Swaps[0].Route.TokenPath);
        Assert.Equal(ETHER, result[1].OutputAmount.Currency);
    }
}