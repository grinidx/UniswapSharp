using Nethereum.ABI;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Utils;

namespace UniswapSharp.V3.Utils;

public static class ComputePoolAddress
{
    public static string Compute(
        string factoryAddress,
        Token tokenA,
        Token tokenB,
        Constants.FeeAmount fee,
        string? initCodeHashManualOverride = null,
        ChainId? chainId = null)
    {
        //[Fact]
        //public async void FromRoutes_CanBeConstructedWithEtherAsInputForExactOutputWithMultipleRoutes()
        //{
        //    var trade = await Trade.FromRoutes<Ether, Token, TradeType.EXACT_OUTPUT>(
        //        new List<RouteInput>
        //        {
        //            new RouteInput
        //            {
        //                Amount = CurrencyAmount.FromRawAmount(token0, BigInteger.Parse("3000")),
        //                Route = new Route(new List<Pool> { pool_weth_0 }, ETHER, token0),
        //            },
        //            new RouteInput
        //            {
        //                Amount = CurrencyAmount.FromRawAmount(token0, BigInteger.Parse("7000")),
        //                Route = new Route(new List<Pool> { pool_weth_1, pool_0_1 }, ETHER, token0),
        //            }
        //        },
        //        TradeType.EXACT_OUTPUT
        //    );
        //    Assert.Equal(ETHER, trade.InputAmount.Currency);
        //    Assert.Equal(token0, trade.OutputAmount.Currency);
        //}

        //[Fact]
        //public async void FromRoutes_CanBeConstructedWithEtherAsOutputWithMultipleRoutes()
        //{
        //    var trade = await Trade.FromRoutes<Token, Ether, TradeType.EXACT_OUTPUT>(
        //        new List<RouteInput>
        //        {
        //            new RouteInput
        //            {
        //                Amount = CurrencyAmount.FromRawAmount(Ether.OnChain(1), BigInteger.Parse("4000")),
        //                Route = new Route(new List<Pool> { pool_weth_0 }, token0, ETHER),
        //            },
        //            new RouteInput
        //            {
        //                Amount = CurrencyAmount.FromRawAmount(Ether.OnChain(1), BigInteger.Parse("6000")),
        //                Route = new Route(new List<Pool> { pool_0_1, pool_weth_1 }, token0, ETHER),
        //            }
        //        },
        //        TradeType.EXACT_OUTPUT
        //    );
        //    Assert.Equal(token0, trade.InputAmount.Currency);
        //    Assert.Equal(ETHER, trade.OutputAmount.Currency);
        //}

        //[Fact]
        //public async void FromRoutes_CanBeConstructedWithEtherAsOutputForExactInputWithMultipleRoutes()
        //{
        //    var trade = await Trade.FromRoutes<Token, Ether, TradeType.EXACT_INPUT>(
        //        new List<RouteInput>
        //        {
        //            new RouteInput
        //            {
        //                Amount = CurrencyAmount.FromRawAmount(token0, BigInteger.Parse("3000")),
        //                Route = new Route(new List<Pool> { pool_weth_0 }, token0, ETHER),
        //            },
        //            new RouteInput
        //            {
        //                Amount = CurrencyAmount.FromRawAmount(token0, BigInteger.Parse("7000")),
        //                Route = new Route(new List<Pool> { pool_0_1, pool_weth_1 }, token0, ETHER),
        //            }
        //        },
        //        TradeType.EXACT_INPUT
        //    );
        //    Assert.Equal(token0, trade.InputAmount.Currency);
        //    Assert.Equal(ETHER, trade.OutputAmount.Currency);
        //}

        //[Fact]
        //public async void FromRoutes_ThrowsIfPoolsAreReUsedBetweenRoutes()
        //{
        //    await Assert.ThrowsAsync<Exception>(async () =>
        //    {
        //        await Trade.FromRoutes<Token, Ether, TradeType.EXACT_INPUT>(
        //            new List<RouteInput>
        //            {
        //                new RouteInput
        //                {
        //                    Amount = CurrencyAmount.FromRawAmount(token0, BigInteger.Parse("4500")),
        //                    Route = new Route(new List<Pool> { pool_0_1, pool_weth_1 }, token0, ETHER),
        //                },
        //                new RouteInput
        //                {
        //                    Amount = CurrencyAmount.FromRawAmount(token0, BigInteger.Parse("5500")),
        //                    Route = new Route(new List<Pool> { pool_0_1, pool_1_2, pool_weth_2 }, token0, ETHER),
        //                }
        //            },
        //            TradeType.EXACT_INPUT
        //        );
        //    });
        //}

        //[Fact]
        //public void CreateUncheckedTrade_ThrowsIfInputCurrencyDoesNotMatchRoute()
        //{
        //    Assert.Throws<Exception>(() =>
        //        Trade.CreateUncheckedTrade(new TradeInput
        //        {
        //            Route = new Route(new List<Pool> { pool_0_1 }, token0, token1),
        //            InputAmount = CurrencyAmount.FromRawAmount(token2, 10000),
        //            OutputAmount = CurrencyAmount.FromRawAmount(token1, 10000),
        //            TradeType = TradeType.EXACT_INPUT,
        //        })
        //    );
        //}

        //[Fact]
        //public void CreateUncheckedTrade_ThrowsIfOutputCurrencyDoesNotMatchRoute()
        //{
        //    Assert.Throws<Exception>(() =>
        //        Trade.CreateUncheckedTrade(new TradeInput
        //        {
        //            Route = new Route(new List<Pool> { pool_0_1 }, token0, token1),
        //            InputAmount = CurrencyAmount.FromRawAmount(token0, 10000),
        //            OutputAmount = CurrencyAmount.FromRawAmount(token2, 10000),
        //            TradeType = TradeType.EXACT_INPUT,
        //        })
        //    );
        //}

        //[Fact]
        //public void CreateUncheckedTrade_CanCreateAnExactInputTradeWithoutSimulating()
        //{
        //    Trade.CreateUncheckedTrade(new TradeInput
        //    {
        //        Route = new Route(new List<Pool> { pool_0_1 }, token0, token1),
        //        InputAmount = CurrencyAmount.FromRawAmount(token0, 10000),
        //        OutputAmount = CurrencyAmount.FromRawAmount(token1, 100000),
        //        TradeType = TradeType.EXACT_INPUT,
        //    });
        //}

        //[Fact]
        //public void CreateUncheckedTrade_CanCreateAnExactOutputTradeWithoutSimulating()
        //{
        //    Trade.CreateUncheckedTrade(new TradeInput
        //    {
        //        Route = new Route(new List<Pool> { pool_0_1 }, token0, token1),
        //        InputAmount = CurrencyAmount.FromRawAmount(token0, 10000),
        //        OutputAmount = CurrencyAmount.FromRawAmount(token1, 100000),
        //        TradeType = TradeType.EXACT_OUTPUT,
        //    });
        //}

        //[Fact]
        //public void CreateUncheckedTradeWithMultipleRoutes_ThrowsIfInputCurrencyDoesNotMatchRouteWithMultipleRoutes()
        //{
        //    Assert.Throws<Exception>(() =>
        //        Trade.CreateUncheckedTradeWithMultipleRoutes(new MultipleRoutesInput
        //        {
        //            Routes = new List<RouteInput>
        //            {
        //                new RouteInput
        //                {
        //                    Route = new Route(new List<Pool> { pool_1_2 }, token2, token1),
        //                    InputAmount = CurrencyAmount.FromRawAmount(token2, 2000),
        //                    OutputAmount = CurrencyAmount.FromRawAmount(token1, 2000),
        //                },
        //                new RouteInput
        //                {
        //                    Route = new Route(new List<Pool> { pool_0_1 }, token0, token1),
        //                    InputAmount = CurrencyAmount.FromRawAmount(token2, 8000),
        //                    OutputAmount = CurrencyAmount.FromRawAmount(token1, 8000),
        //                }
        //            },
        //            TradeType = TradeType.EXACT_INPUT,
        //        })
        //    );
        //}

        //[Fact]
        //public void CreateUncheckedTradeWithMultipleRoutes_ThrowsIfOutputCurrencyDoesNotMatchRouteWithMultipleRoutes()
        //{
        //    Assert.Throws<Exception>(() =>
        //        Trade.CreateUncheckedTradeWithMultipleRoutes(new MultipleRoutesInput
        //        {
        //            Routes = new List<RouteInput>
        //            {
        //                new RouteInput
        //                {
        //                    Route = new Route(new List<Pool> { pool_0_2 }, token0, token2),
        //                    InputAmount = CurrencyAmount.FromRawAmount(token0, 10000),
        //                    OutputAmount = CurrencyAmount.FromRawAmount(token2, 10000),
        //                },
        //                new RouteInput
        //                {
        //                    Route = new Route(new List<Pool> { pool_0_1 }, token0, token1),
        //                    InputAmount = CurrencyAmount.FromRawAmount(token0, 10000),
        //                    OutputAmount = CurrencyAmount.FromRawAmount(token2, 10000),
        //                }
        //            },
        //            TradeType = TradeType.EXACT_INPUT,
        //        })
        //    );
        //}

        //[Fact]
        //public void CreateUncheckedTradeWithMultipleRoutes_CanCreateAnExactInputTradeWithoutSimulatingWithMultipleRoutes()
        //{
        //    Trade.CreateUncheckedTradeWithMultipleRoutes(new MultipleRoutesInput
        //    {
        //        Routes = new List<RouteInput>
        //        {
        //            new RouteInput
        //            {
        //                Route = new Route(new List<Pool> { pool_0_1 }, token0, token1),
        //                InputAmount = CurrencyAmount.FromRawAmount(token0, 5000),
        //                OutputAmount = CurrencyAmount.FromRawAmount(token1, 50000),
        //            },
        //            new RouteInput
        //            {
        //                Route = new Route(new List<Pool> { pool_0_2, pool_1_2 }, token0, token1),
        //                InputAmount = CurrencyAmount.FromRawAmount(token0, 5000),
        //                OutputAmount = CurrencyAmount.FromRawAmount(token1, 50000),
        //            }
        //        },
        //        TradeType = TradeType.EXACT_INPUT,
        //    });
        //}

        //[Fact]
        //public void CreateUncheckedTradeWithMultipleRoutes_CanCreateAnExactOutputTradeWithoutSimulatingWithMultipleRoutes()
        //{
        //    Trade.CreateUncheckedTradeWithMultipleRoutes(new MultipleRoutesInput
        //    {
        //        Routes = new List<RouteInput>
        //        {
        //            new RouteInput
        //            {
        //                Route = new Route(new List<Pool> { pool_0_1 }, token0, token1),
        //                InputAmount = CurrencyAmount.FromRawAmount(token0, 5001),
        //                OutputAmount = CurrencyAmount.FromRawAmount(token1, 50000),
        //            },
        //            new RouteInput
        //            {
        //                Route = new Route(new List<Pool> { pool_0_2, pool_1_2 }, token0, token1),
        //                InputAmount = CurrencyAmount.FromRawAmount(token0, 4999),
        //                OutputAmount = CurrencyAmount.FromRawAmount(token1, 50000),
        //            }
        //        },
        //        TradeType = TradeType.EXACT_OUTPUT,
        //    });
        //}
        var (token0, token1) = tokenA.SortsBefore(tokenB) ? (tokenA, tokenB) : (tokenB, tokenA);

        var abiEncoder = new ABIEncode();
        var encodedData = abiEncoder.GetABIEncoded(
            new ABIValue("address", token0.Address),
            new ABIValue("address", token1.Address),
            new ABIValue("uint24", (int)fee)
        );

        var salt = Sha3Keccack.Current.CalculateHash(encodedData);
        var initCodeHash = initCodeHashManualOverride ?? Constants.PoolInitCodeHash(chainId);

        switch (chainId)
        {
            //case ChainId.ZKSYNC:
            //    return ComputeZksyncCreate2Address(factoryAddress, initCodeHash, salt);
            default:
                return AddressValidator.GetCreate2Address(factoryAddress, salt, initCodeHash.HexToByteArray());
        }
    }



}
