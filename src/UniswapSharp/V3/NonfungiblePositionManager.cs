using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.V3.Entities;
using UniswapSharp.V3.Utils;

namespace UniswapSharp.V3;

public class NonfungiblePositionManager
{
    public class MethodParameters
    {
        public string Calldata { get; set; }
        public string Value { get; set; }
    }

   // public static readonly Interface INTERFACE = new Interface(INonfungiblePositionManager.abi);

    private NonfungiblePositionManager() { }

    private static string EncodeCreate(Pool pool)
    {
        //return INTERFACE.EncodeFunctionData("createAndInitializePoolIfNecessary", new object[]
        //{
        //    pool.Token0.Address,
        //    pool.Token1.Address,
        //    pool.Fee,
        //    toHex(pool.SqrtRatioX96)
        //});
        throw new NotImplementedException();
    }

    //public static MethodParameters CreateCallParameters(Pool pool)
    //{
    //    return new MethodParameters
    //    {
    //        Calldata = EncodeCreate(pool),
    //        Value = toHex(0)
    //    };
    //}

    //public static MethodParameters AddCallParameters(Position position, AddLiquidityOptions options)
    //{
    //    invariant(JSBI.GreaterThan(position.Liquidity, ZERO), "ZERO_LIQUIDITY");

    //    var calldatas = new List<string>();

    //    var (amount0Desired, amount1Desired) = position.MintAmounts;

    //    var minimumAmounts = position.MintAmountsWithSlippage(options.SlippageTolerance);
    //    var amount0Min = toHex(minimumAmounts.Amount0);
    //    var amount1Min = toHex(minimumAmounts.Amount1);

    //    var deadline = toHex(options.Deadline);

    //    if (IsMint(options) && options.CreatePool)
    //    {
    //        calldatas.Add(EncodeCreate(position.Pool));
    //    }

    //    if (options.Token0Permit != null)
    //    {
    //        calldatas.Add(SelfPermit.EncodePermit(position.Pool.Token0, options.Token0Permit));
    //    }
    //    if (options.Token1Permit != null)
    //    {
    //        calldatas.Add(SelfPermit.EncodePermit(position.Pool.Token1, options.Token1Permit));
    //    }

    //    if (IsMint(options))
    //    {
    //        var recipient = ValidateAndParseAddress(options.Recipient);

    //        calldatas.Add(INTERFACE.EncodeFunctionData("mint", new object[]
    //        {
    //            new
    //            {
    //                Token0 = position.Pool.Token0.Address,
    //                Token1 = position.Pool.Token1.Address,
    //                Fee = position.Pool.Fee,
    //                TickLower = position.TickLower,
    //                TickUpper = position.TickUpper,
    //                Amount0Desired = toHex(amount0Desired),
    //                Amount1Desired = toHex(amount1Desired),
    //                Amount0Min,
    //                Amount1Min,
    //                Recipient = recipient,
    //                Deadline = deadline
    //            }
    //        }));
    //    }
    //    else
    //    {
    //        calldatas.Add(INTERFACE.EncodeFunctionData("increaseLiquidity", new object[]
    //        {
    //            new
    //            {
    //                TokenId = toHex(options.TokenId),
    //                Amount0Desired = toHex(amount0Desired),
    //                Amount1Desired = toHex(amount1Desired),
    //                Amount0Min,
    //                Amount1Min,
    //                Deadline = deadline
    //            }
    //        }));
    //    }

    //    string value = toHex(0);

    //    if (options.UseNative != null)
    //    {
    //        var wrapped = options.UseNative.Wrapped;
    //        invariant(position.Pool.Token0.Equals(wrapped) || position.Pool.Token1.Equals(wrapped), "NO_WETH");

    //        var wrappedValue = position.Pool.Token0.Equals(wrapped) ? amount0Desired : amount1Desired;

    //        if (JSBI.GreaterThan(wrappedValue, ZERO))
    //        {
    //            calldatas.Add(Payments.EncodeRefundETH());
    //        }

    //        value = toHex(wrappedValue);
    //    }

    //    return new MethodParameters
    //    {
    //        Calldata = Multicall.EncodeMulticall(calldatas),
    //        Value = value
    //    };
    //}

    //private static List<string> EncodeCollect(CollectOptions options)
    //{
    //    var calldatas = new List<string>();

    //    var tokenId = toHex(options.TokenId);

    //    var involvesETH = options.ExpectedCurrencyOwed0.Currency.IsNative || options.ExpectedCurrencyOwed1.Currency.IsNative;

    //    var recipient = ValidateAndParseAddress(options.Recipient);

    //    calldatas.Add(INTERFACE.EncodeFunctionData("collect", new object[]
    //    {
    //        new
    //        {
    //            TokenId = tokenId,
    //            Recipient = involvesETH ? ADDRESS_ZERO : recipient,
    //            Amount0Max = MaxUint128,
    //            Amount1Max = MaxUint128
    //        }
    //    }));

    //    if (involvesETH)
    //    {
    //        var ethAmount = options.ExpectedCurrencyOwed0.Currency.IsNative
    //            ? options.ExpectedCurrencyOwed0.Quotient
    //            : options.ExpectedCurrencyOwed1.Quotient;
    //        var token = options.ExpectedCurrencyOwed0.Currency.IsNative
    //            ? (Token)options.ExpectedCurrencyOwed1.Currency
    //            : (Token)options.ExpectedCurrencyOwed0.Currency;
    //        var tokenAmount = options.ExpectedCurrencyOwed0.Currency.IsNative
    //            ? options.ExpectedCurrencyOwed1.Quotient
    //            : options.ExpectedCurrencyOwed0.Quotient;

    //        calldatas.Add(Payments.EncodeUnwrapWETH9(ethAmount, recipient));
    //        calldatas.Add(Payments.EncodeSweepToken(token, tokenAmount, recipient));
    //    }

    //    return calldatas;
    //}

    //public static MethodParameters CollectCallParameters(CollectOptions options)
    //{
    //    var calldatas = EncodeCollect(options);

    //    return new MethodParameters
    //    {
    //        Calldata = Multicall.EncodeMulticall(calldatas),
    //        Value = toHex(0)
    //    };
    //}

    //public static MethodParameters RemoveCallParameters(Position position, RemoveLiquidityOptions options)
    //{
    //    var calldatas = new List<string>();

    //    var deadline = toHex(options.Deadline);
    //    var tokenId = toHex(options.TokenId);

    //    var partialPosition = new Position(new PositionParameters
    //    {
    //        Pool = position.Pool,
    //        Liquidity = options.LiquidityPercentage.Multiply(position.Liquidity).Quotient,
    //        TickLower = position.TickLower,
    //        TickUpper = position.TickUpper
    //    });
    //    invariant(JSBI.GreaterThan(partialPosition.Liquidity, ZERO), "ZERO_LIQUIDITY");

    //    var (amount0Min, amount1Min) = partialPosition.BurnAmountsWithSlippage(options.SlippageTolerance);

    //    if (options.Permit != null)
    //    {
    //        calldatas.Add(INTERFACE.EncodeFunctionData("permit", new object[]
    //        {
    //            ValidateAndParseAddress(options.Permit.Spender),
    //            tokenId,
    //            toHex(options.Permit.Deadline),
    //            options.Permit.V,
    //            options.Permit.R,
    //            options.Permit.S
    //        }));
    //    }

    //    calldatas.Add(INTERFACE.EncodeFunctionData("decreaseLiquidity", new object[]
    //    {
    //        new
    //        {
    //            TokenId = tokenId,
    //            Liquidity = toHex(partialPosition.Liquidity),
    //            Amount0Min = toHex(amount0Min),
    //            Amount1Min = toHex(amount1Min),
    //            Deadline = deadline
    //        }
    //    }));

    //    var (expectedCurrencyOwed0, expectedCurrencyOwed1, rest) = options.CollectOptions;
    //    calldatas.AddRange(EncodeCollect(new CollectOptions
    //    {
    //        TokenId = toHex(options.TokenId),
    //        ExpectedCurrencyOwed0 = expectedCurrencyOwed0.Add(CurrencyAmount.FromRawAmount(expectedCurrencyOwed0.Currency, amount0Min)),
    //        ExpectedCurrencyOwed1 = expectedCurrencyOwed1.Add(CurrencyAmount.FromRawAmount(expectedCurrencyOwed1.Currency, amount1Min)),
    //        // Add other properties from rest
    //    }));

    //    if (options.LiquidityPercentage.EqualTo(ONE))
    //    {
    //        if (options.BurnToken)
    //        {
    //            calldatas.Add(INTERFACE.EncodeFunctionData("burn", new object[] { tokenId }));
    //        }
    //    }
    //    else
    //    {
    //        invariant(options.BurnToken != true, "CANNOT_BURN");
    //    }

    //    return new MethodParameters
    //    {
    //        Calldata = Multicall.EncodeMulticall(calldatas),
    //        Value = toHex(0)
    //    };
    //}

    //public static MethodParameters SafeTransferFromParameters(SafeTransferOptions options)
    //{
    //    var recipient = ValidateAndParseAddress(options.Recipient);
    //    var sender = ValidateAndParseAddress(options.Sender);

    //    string calldata;
    //    if (options.Data != null)
    //    {
    //        calldata = INTERFACE.EncodeFunctionData("safeTransferFrom(address,address,uint256,bytes)", new object[]
    //        {
    //            sender,
    //            recipient,
    //            toHex(options.TokenId),
    //            options.Data
    //        });
    //    }
    //    else
    //    {
    //        calldata = INTERFACE.EncodeFunctionData("safeTransferFrom(address,address,uint256)", new object[]
    //        {
    //            sender,
    //            recipient,
    //            toHex(options.TokenId)
    //        });
    //    }
    //    return new MethodParameters
    //    {
    //        Calldata = calldata,
    //        Value = toHex(0)
    //    };
    //}

    //private static bool IsMint(AddLiquidityOptions options)
    //{
    //    return options.GetType().GetProperty("Recipient") != null;
    //}

    //private static void invariant(bool condition, string message)
    //{
    //    if (!condition)
    //    {
    //        throw new Exception(message);
    //    }
    //}

    //private static string toHex(object value)
    //{
    //    // Implement conversion to hex string
    //    return value.ToString();
    //}

    //private static string ValidateAndParseAddress(string address)
    //{
    //    // Implement address validation and parsing
    //    return address;
    //}
}