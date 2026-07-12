using System.Numerics;
using System.Text;
using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.UniversalRouter;
using UniswapSharp.UniversalRouter.Entities.Actions;
using UniswapSharp.UniversalRouter.Types;
using UniswapSharp.UniversalRouter.Utils;
using UniswapSharp.V3.Utils;
using UniswapSharp.V4.Utils;
using Constants = UniswapSharp.UniversalRouter.Utils.Constants;
using PoolKey = UniswapSharp.V4.Entities.PoolKey;

namespace UniswapSharp.Testing.UniversalRouter;

// Ported from sdks/universal-router-sdk/test/unit/encodeSwaps.test.ts (validate + encodeSwapStep + encodeSwaps).
public class EncodeSwapsTests
{
    private static readonly Ether ETH = Ether.OnChain(1);
    private static readonly Token WETH = Weth9.Tokens[1];
    private static readonly Token USDC = new(1, "0xa0b86991c6218b36c1d19d4a2e9eb0ce3606eb48", 6, "USDC", "USD Coin");
    private static readonly Token DAI = new(1, "0x6B175474E89094C44Da98b954EedeAC495271d0F", 18, "DAI", "dai");

    private const string TEST_RECIPIENT = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string FEE_RECIPIENT = "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private static readonly Permit2Permit TEST_PERMIT = new(
        new UniswapSharp.Permit2.PermitDetails("0xa0b86991c6218b36c1d19d4a2e9eb0ce3606eb48", 1000000, 1000000000, 0),
        "0x0000000000000000000000000000000000000001",
        1000000000,
        "0x" + string.Concat(Enumerable.Repeat("00", 65)));

    private static CurrencyAmount<BaseCurrency> CA(BaseCurrency c, string raw) =>
        CurrencyAmount<BaseCurrency>.FromRawAmount(c, BigInteger.Parse(raw));

    // ---- packV3Path helpers ----
    private static string PackV3Path(string[] tokens, int[] fees)
    {
        var sb = new StringBuilder("0x").Append(tokens[0][2..]);
        for (int i = 0; i < fees.Length; i++)
        {
            sb.Append(fees[i].ToString("x6")).Append(tokens[i + 1][2..]);
        }
        return sb.ToString().ToLowerInvariant();
    }

    private static string PackV3ExactOutPath(string[] tokens, int[] fees)
    {
        var sb = new StringBuilder("0x").Append(tokens[^1][2..]);
        for (int i = fees.Length - 1; i >= 0; i--)
        {
            sb.Append(fees[i].ToString("x6")).Append(tokens[i][2..]);
        }
        return sb.ToString().ToLowerInvariant();
    }

    private static string Addr(BaseCurrency c) => c.Wrapped().Address;

    private static SwapSpecification BuildSpec(
        TradeType tradeType = TradeType.EXACT_INPUT,
        BaseCurrency? inputToken = null,
        BaseCurrency? outputToken = null,
        CurrencyAmount<BaseCurrency>? amount = null,
        CurrencyAmount<BaseCurrency>? quote = null,
        string? recipient = TEST_RECIPIENT,
        Percent? slippage = null,
        TokenTransferMode? mode = TokenTransferMode.Permit2,
        UniversalRouterVersion? urVersion = UniversalRouterVersion.V2_0,
        bool safeMode = false,
        int? chainId = null,
        Permit2Permit? permit = null,
        Fee? fee = null,
        object? deadline = null,
        bool? nativeErc20Input = null)
    {
        inputToken ??= USDC;
        outputToken ??= WETH;
        amount ??= CA(USDC, "1000000");
        quote ??= CA(WETH, "500000000000000000");
        return new SwapSpecification
        {
            TradeType = tradeType,
            Routing = new SwapRouting(inputToken, outputToken, amount, quote),
            SlippageTolerance = slippage ?? new Percent(5, 100),
            Recipient = recipient,
            TokenTransferMode = mode,
            UrVersion = urVersion,
            SafeMode = safeMode,
            ChainId = chainId,
            Permit = permit,
            Fee = fee,
            Deadline = deadline,
            NativeErc20Input = nativeErc20Input,
        };
    }

    private static NormalizedSwapSpecification Norm(SwapSpecification s) => NormalizeEncodeSwapsSpec.Normalize(s);

    private static V3SwapExactIn BuildV3ExactInStep(
        string recipient = Constants.ROUTER_AS_RECIPIENT,
        object? amountIn = null,
        object? amountOutMin = null,
        IReadOnlyList<object>? minHop = null,
        BaseCurrency[]? tokens = null,
        int[]? fees = null)
    {
        tokens ??= new BaseCurrency[] { USDC, WETH };
        fees ??= new[] { 500 };
        return new V3SwapExactIn(
            recipient, amountIn ?? "1000000", amountOutMin ?? "0",
            PackV3Path(tokens.Select(Addr).ToArray(), fees), minHop);
    }

    private static V3SwapExactOut BuildV3ExactOutStep(
        object? amountOut = null,
        object? amountInMax = null,
        BaseCurrency[]? tokens = null,
        int[]? fees = null)
    {
        tokens ??= new BaseCurrency[] { USDC, WETH };
        fees ??= new[] { 500 };
        return new V3SwapExactOut(
            Constants.ROUTER_AS_RECIPIENT, amountOut ?? "500000000000000000", amountInMax ?? "1050000",
            PackV3ExactOutPath(tokens.Select(Addr).ToArray(), fees));
    }

    private static BigInteger ExactInputGrossMin(BigInteger quote, Percent s) =>
        quote * (s.Denominator - s.Numerator) / s.Denominator;

    private static BigInteger ExactOutputMaxIn(BigInteger quote, Percent s) =>
        quote * (s.Denominator + s.Numerator) / s.Denominator;

    // ---- decode helpers ----
    private static (string commands, List<string> inputs, BigInteger? deadline) DecodeExecute(string calldata)
    {
        string selector = calldata[..10];
        string body = "0x" + calldata[10..];
        if (selector == SwapRouter.GetSighash("execute(bytes,bytes[],uint256)"))
        {
            var d = AbiParamDecoder.Decode(new[] { "bytes", "bytes[]", "uint256" }, body);
            return ((string)d[0]!, ((List<object?>)d[1]!).Select(x => (string)x!).ToList(), (BigInteger)d[2]!);
        }
        var d2 = AbiParamDecoder.Decode(new[] { "bytes", "bytes[]" }, body);
        return ((string)d2[0]!, ((List<object?>)d2[1]!).Select(x => (string)x!).ToList(), null);
    }

    private static List<int> CommandTypes(string commands)
    {
        var list = new List<int>();
        for (int i = 2; i < commands.Length; i += 2)
        {
            list.Add(Convert.ToInt32(commands.Substring(i, 2), 16) & 0x3f);
        }
        return list;
    }

    private static List<object?> Decode(string[] types, string hex) => AbiParamDecoder.Decode(types, hex);

    private static void ExpectThrows(string message, Action action)
    {
        var ex = Assert.ThrowsAny<Exception>(action);
        Assert.Contains(message, ex.Message);
    }

    // ================= validateEncodeSwaps =================

    [Fact]
    public void Validate_RejectsEmptySwapSteps() =>
        ExpectThrows("EMPTY_SWAP_STEPS", () => ValidateEncodeSwaps.Validate(Norm(BuildSpec()), new List<SwapStep>()));

    [Fact]
    public void Validate_RejectsApproveProxyWithoutChainId() =>
        ExpectThrows("PROXY_MISSING_CHAIN_ID", () =>
            ValidateEncodeSwaps.Validate(Norm(BuildSpec(mode: TokenTransferMode.ApproveProxy)), new SwapStep[] { BuildV3ExactInStep() }));

    [Fact]
    public void Validate_RejectsApproveProxyWithNativeInput() =>
        ExpectThrows("PROXY_NATIVE_INPUT", () =>
            ValidateEncodeSwaps.Validate(Norm(BuildSpec(
                mode: TokenTransferMode.ApproveProxy, chainId: 1,
                inputToken: ETH, amount: CA(ETH, "1000000000000000000"), quote: CA(WETH, "500000000000000000"))),
                new SwapStep[] { BuildV3ExactInStep(amountIn: "1000000000000000000", tokens: new BaseCurrency[] { WETH, WETH }) }));

    [Fact]
    public void Validate_RejectsApproveProxyWithPermit() =>
        ExpectThrows("PROXY_PERMIT_CONFLICT", () =>
            ValidateEncodeSwaps.Validate(Norm(BuildSpec(mode: TokenTransferMode.ApproveProxy, chainId: 1, permit: TEST_PERMIT)),
                new SwapStep[] { BuildV3ExactInStep() }));

    [Fact]
    public void Validate_RejectsApproveProxyWithoutExplicitRecipient() =>
        ExpectThrows("PROXY_EXPLICIT_RECIPIENT_REQUIRED", () =>
            ValidateEncodeSwaps.Validate(Norm(BuildSpec(mode: TokenTransferMode.ApproveProxy, chainId: 1, recipient: Constants.SENDER_AS_RECIPIENT)),
                new SwapStep[] { BuildV3ExactInStep() }));

    [Fact]
    public void Validate_RejectsNativeInputWithPermit() =>
        ExpectThrows("NATIVE_INPUT_PERMIT", () =>
            ValidateEncodeSwaps.Validate(Norm(BuildSpec(
                permit: TEST_PERMIT, inputToken: ETH, outputToken: USDC,
                amount: CA(ETH, "1000000000000000000"), quote: CA(USDC, "2000000000"))),
                new SwapStep[] { BuildV3ExactInStep(amountIn: "1000000000000000000", tokens: new BaseCurrency[] { WETH, USDC }) }));

    [Fact]
    public void Validate_RejectsZeroAddressRecipient() =>
        ExpectThrows("RECIPIENT_CANNOT_BE_ZERO", () =>
            ValidateEncodeSwaps.Validate(Norm(BuildSpec(recipient: Constants.ZERO_ADDRESS)), new SwapStep[] { BuildV3ExactInStep() }));

    [Fact]
    public void Validate_RejectsRouterRecipientInPermit2Mode() =>
        ExpectThrows("RECIPIENT_CANNOT_BE_ROUTER", () =>
            ValidateEncodeSwaps.Validate(Norm(BuildSpec(recipient: Constants.ROUTER_AS_RECIPIENT)), new SwapStep[] { BuildV3ExactInStep() }));

    [Fact]
    public void Validate_RejectsNegativeSlippage() =>
        ExpectThrows("SLIPPAGE_TOLERANCE", () =>
            ValidateEncodeSwaps.Validate(Norm(BuildSpec(slippage: new Percent(-1, 100))), new SwapStep[] { BuildV3ExactInStep() }));

    [Fact]
    public void Validate_RejectsExactInputWrongAmountCurrency() =>
        ExpectThrows("INVALID_ROUTING_AMOUNT_CURRENCY", () =>
            ValidateEncodeSwaps.Validate(Norm(BuildSpec(amount: CA(WETH, "500000000000000000"))), new SwapStep[] { BuildV3ExactInStep() }));

    [Fact]
    public void Validate_RejectsExactInputWrongQuoteCurrency() =>
        ExpectThrows("INVALID_ROUTING_QUOTE_CURRENCY", () =>
            ValidateEncodeSwaps.Validate(Norm(BuildSpec(quote: CA(USDC, "1000000"))), new SwapStep[] { BuildV3ExactInStep() }));

    [Fact]
    public void Validate_RejectsExactOutputWrongAmountCurrency() =>
        ExpectThrows("INVALID_ROUTING_AMOUNT_CURRENCY", () =>
            ValidateEncodeSwaps.Validate(Norm(BuildSpec(
                tradeType: TradeType.EXACT_OUTPUT, amount: CA(USDC, "1000000"), quote: CA(USDC, "1000000"))),
                new SwapStep[] { BuildV3ExactOutStep() }));

    [Fact]
    public void Validate_RejectsV3RecipientNotRouter() =>
        ExpectThrows("STEP_RECIPIENT_MUST_BE_ROUTER", () =>
            ValidateEncodeSwaps.Validate(Norm(BuildSpec()), new SwapStep[] { BuildV3ExactInStep(recipient: TEST_RECIPIENT) }));

    [Fact]
    public void Validate_RejectsV2SenderRecipient() =>
        ExpectThrows("STEP_RECIPIENT_MUST_BE_ROUTER", () =>
            ValidateEncodeSwaps.Validate(Norm(BuildSpec()),
                new SwapStep[] { new V2SwapExactIn(Constants.SENDER_AS_RECIPIENT, "1000", "0", new[] { USDC.Address, WETH.Address }) }));

    [Fact]
    public void Validate_RejectsV4TakeRecipientNotRouter() =>
        ExpectThrows("V4_ACTION_RECIPIENT_MUST_BE_ROUTER", () =>
            ValidateEncodeSwaps.Validate(Norm(BuildSpec()),
                new SwapStep[] { new V4Swap(new V4Action[] { new V4Take(USDC.Address, TEST_RECIPIENT, "1000") }) }));

    [Fact]
    public void Validate_AllowsRouterCustodyRecipients()
    {
        ValidateEncodeSwaps.Validate(Norm(BuildSpec()), new SwapStep[] { BuildV3ExactInStep() });
    }

    [Fact]
    public void Validate_RejectsPortionFeeOnNonExactInput() =>
        ExpectThrows("INVALID_PORTION_FEE_TRADE_TYPE", () =>
            ValidateEncodeSwaps.Validate(Norm(BuildSpec(
                tradeType: TradeType.EXACT_OUTPUT, fee: new PortionFee(FEE_RECIPIENT, new Percent(5, 100)),
                amount: CA(WETH, "500000000000000000"), quote: CA(USDC, "1000000"))),
                new SwapStep[] { BuildV3ExactOutStep() }));

    [Fact]
    public void Validate_RejectsFlatFeeOnNonExactOutput() =>
        ExpectThrows("INVALID_FLAT_FEE_TRADE_TYPE", () =>
            ValidateEncodeSwaps.Validate(Norm(BuildSpec(fee: new FlatFee(FEE_RECIPIENT, "1000"))),
                new SwapStep[] { BuildV3ExactInStep() }));

    [Fact]
    public void Validate_RejectsFlatFeeExceedingGross() =>
        ExpectThrows("FLAT_FEE_GT_AMOUNT", () =>
            ValidateEncodeSwaps.Validate(Norm(BuildSpec(
                tradeType: TradeType.EXACT_OUTPUT, fee: new FlatFee(FEE_RECIPIENT, "600000000000000000"),
                amount: CA(WETH, "500000000000000000"), quote: CA(USDC, "1000000"))),
                new SwapStep[] { BuildV3ExactOutStep() }));

    [Fact]
    public void Validate_RejectsMinHopOnV2_0Steps() =>
        ExpectThrows("MIN_HOP_PRICE_X36_UNSUPPORTED_ON_V2_0", () =>
            ValidateEncodeSwaps.Validate(Norm(BuildSpec(urVersion: UniversalRouterVersion.V2_0)),
                new SwapStep[] { BuildV3ExactInStep(minHop: new object[] { "100" }) }));

    [Fact]
    public void Validate_RejectsFractionalBpsPortionFeeOnV2_0() =>
        ExpectThrows("FRACTIONAL_BPS_PORTION_FEE_UNSUPPORTED_ON_V2_0", () =>
            ValidateEncodeSwaps.Validate(Norm(BuildSpec(urVersion: UniversalRouterVersion.V2_0, fee: new PortionFee(FEE_RECIPIENT, new Percent(1, 3)))),
                new SwapStep[] { BuildV3ExactInStep() }));

    [Fact]
    public void Validate_RejectsV2HopCountMismatch() =>
        ExpectThrows("V2_MIN_HOP_PRICE_X36_LENGTH_MISMATCH", () =>
            ValidateEncodeSwaps.Validate(Norm(BuildSpec(urVersion: UniversalRouterVersion.V2_1_1)),
                new SwapStep[] { new V2SwapExactIn(Constants.ROUTER_AS_RECIPIENT, "1000", "0", new[] { USDC.Address, WETH.Address }, new object[] { "10", "20" }) }));

    [Fact]
    public void Validate_RejectsV4PathHopCountMismatch()
    {
        var step = new V4Swap(new V4Action[]
        {
            new V4SwapExactIn(USDC.Address,
                new[] { new PathKey(WETH.Address, 500, 10, Constants.ETH_ADDRESS, "0x") },
                "1000", "0", new object[] { "10", "20" }),
        });
        ExpectThrows("V4_MIN_HOP_PRICE_X36_LENGTH_MISMATCH", () =>
            ValidateEncodeSwaps.Validate(Norm(BuildSpec(urVersion: UniversalRouterVersion.V2_1_1)), new SwapStep[] { step }));
    }

    // ================= encodeSwapStep =================

    [Fact]
    public void EncodeSwapStep_HardcodesV4SettlePayerIsUserFalse()
    {
        var planner = new RoutePlanner();
        var step = new V4Swap(new V4Action[] { new V4Settle(USDC.Address, "1000") });
        EncodeSwapStep.Encode(planner, step, UniversalRouterVersion.V2_1_1);
        var parsed = V4BaseActionsParser.ParseCalldata(planner.Inputs[0], URVersion.V2_1_1);
        Assert.Equal(false, parsed.Actions[0].Params[2].Value);
    }

    [Fact]
    public void EncodeSwapStep_EncodesV4SingleMinHopOnV2_1_1()
    {
        var planner = new RoutePlanner();
        var step = new V4Swap(new V4Action[]
        {
            new V4SwapExactOutSingle(
                new PoolKey(USDC.Address, WETH.Address, 500, 10, Constants.ETH_ADDRESS),
                true, "1000", "2000", "0x", "123456"),
        });
        EncodeSwapStep.Encode(planner, step, UniversalRouterVersion.V2_1_1);
        var parsed = V4BaseActionsParser.ParseCalldata(planner.Inputs[0], URVersion.V2_1_1);
        var swap = (SwapExactOutSingle)parsed.Actions[0].Params[0].Value!;
        Assert.Equal(new BigInteger(123456), swap.MinHopPriceX36);
    }

    // ================= SwapRouter.encodeSwaps =================

    [Fact]
    public void EncodeSwaps_ExactInputErc20ToErc20WithIngressAndFinalSweep()
    {
        var spec = BuildSpec(slippage: new Percent(25, 1000));
        var result = SwapRouter.EncodeSwaps(spec, new SwapStep[] { BuildV3ExactInStep() });
        var (commands, inputs, _) = DecodeExecute(result.Calldata);

        Assert.Equal("0x00", result.Value);
        Assert.Equal("0x020004", commands);

        var transfer = Decode(new[] { "address", "address", "uint160" }, inputs[0]);
        Assert.Equal(USDC.Address.ToLowerInvariant(), ((string)transfer[0]!).ToLowerInvariant());
        Assert.Equal(Constants.ROUTER_AS_RECIPIENT, transfer[1]);
        Assert.Equal("1000000", ((BigInteger)transfer[2]!).ToString());

        var sweep = Decode(new[] { "address", "address", "uint256" }, inputs[2]);
        BigInteger expectedGrossMin = ExactInputGrossMin(new BigInteger(500000000000000000), spec.SlippageTolerance);
        Assert.Equal(WETH.Address.ToLowerInvariant(), ((string)sweep[0]!).ToLowerInvariant());
        Assert.Equal(TEST_RECIPIENT.ToLowerInvariant(), ((string)sweep[1]!).ToLowerInvariant());
        Assert.Equal(expectedGrossMin.ToString(), ((BigInteger)sweep[2]!).ToString());
    }

    [Fact]
    public void EncodeSwaps_AppendsSafeModeSweepOnExactInput()
    {
        var spec = BuildSpec(slippage: new Percent(25, 1000), safeMode: true);
        var result = SwapRouter.EncodeSwaps(spec, new SwapStep[] { BuildV3ExactInStep() });
        var (commands, inputs, _) = DecodeExecute(result.Calldata);

        Assert.Equal(new List<int>
        {
            (int)CommandType.PERMIT2_TRANSFER_FROM, (int)CommandType.V3_SWAP_EXACT_IN,
            (int)CommandType.SWEEP, (int)CommandType.SWEEP,
        }, CommandTypes(commands));

        var safeSweep = Decode(new[] { "address", "address", "uint256" }, inputs[^1]);
        Assert.Equal(Constants.ETH_ADDRESS.ToLowerInvariant(), ((string)safeSweep[0]!).ToLowerInvariant());
        Assert.Equal(TEST_RECIPIENT.ToLowerInvariant(), ((string)safeSweep[1]!).ToLowerInvariant());
        Assert.Equal("0", ((BigInteger)safeSweep[2]!).ToString());
    }

    [Fact]
    public void EncodeSwaps_TreatsPrimitiveZeroDeadlineAsAbsent()
    {
        var result = SwapRouter.EncodeSwaps(BuildSpec(deadline: 0), new SwapStep[] { BuildV3ExactInStep() });
        Assert.Null(DecodeExecute(result.Calldata).deadline);
    }

    [Fact]
    public void EncodeSwaps_TreatsBigIntegerZeroDeadlineAsExplicit()
    {
        var result = SwapRouter.EncodeSwaps(BuildSpec(deadline: BigInteger.Zero), new SwapStep[] { BuildV3ExactInStep() });
        Assert.Equal("0", DecodeExecute(result.Calldata).deadline!.Value.ToString());
    }

    [Fact]
    public void EncodeSwaps_PassesThroughNonzeroDeadline()
    {
        var result = SwapRouter.EncodeSwaps(BuildSpec(deadline: 1700000000), new SwapStep[] { BuildV3ExactInStep() });
        Assert.Equal("1700000000", DecodeExecute(result.Calldata).deadline!.Value.ToString());
    }

    [Fact]
    public void EncodeSwaps_PermitIngressBeforeTransferFrom()
    {
        var result = SwapRouter.EncodeSwaps(BuildSpec(permit: TEST_PERMIT), new SwapStep[] { BuildV3ExactInStep() });
        var (commands, inputs, _) = DecodeExecute(result.Calldata);

        Assert.Equal(new List<int>
        {
            (int)CommandType.PERMIT2_PERMIT, (int)CommandType.PERMIT2_TRANSFER_FROM,
            (int)CommandType.V3_SWAP_EXACT_IN, (int)CommandType.SWEEP,
        }, CommandTypes(commands));
        Assert.Equal("0x00", result.Value);

        var permitInput = Decode(new[]
        {
            "((address token,uint160 amount,uint48 expiration,uint48 nonce) details,address spender,uint256 sigDeadline)",
            "bytes",
        }, inputs[0]);
        var details = (List<object?>)((List<object?>)permitInput[0]!)[0]!;
        Assert.Equal(TEST_PERMIT.Details.Token.ToLowerInvariant(), ((string)details[0]!).ToLowerInvariant());
    }

    [Fact]
    public void EncodeSwaps_Eip2098PermitIngress()
    {
        var permit = TEST_PERMIT with { Signature = "0x" + string.Concat(Enumerable.Repeat("11", 32)) + string.Concat(Enumerable.Repeat("22", 32)) };
        var result = SwapRouter.EncodeSwaps(BuildSpec(permit: permit), new SwapStep[] { BuildV3ExactInStep() });
        var (commands, _, _) = DecodeExecute(result.Calldata);
        Assert.Equal(new List<int>
        {
            (int)CommandType.PERMIT2_PERMIT, (int)CommandType.PERMIT2_TRANSFER_FROM,
            (int)CommandType.V3_SWAP_EXACT_IN, (int)CommandType.SWEEP,
        }, CommandTypes(commands));
    }

    [Fact]
    public void EncodeSwaps_ExactInputNativeToErc20WithWrapAndValue()
    {
        var spec = BuildSpec(
            slippage: new Percent(25, 1000), inputToken: ETH, outputToken: USDC,
            amount: CA(ETH, "1000000000000000000"), quote: CA(USDC, "2056807919"));
        var steps = new SwapStep[]
        {
            new WrapEth(Constants.ROUTER_AS_RECIPIENT, "1000000000000000000"),
            BuildV3ExactInStep(amountIn: "1000000000000000000", tokens: new BaseCurrency[] { WETH, USDC }),
        };
        var result = SwapRouter.EncodeSwaps(spec, steps);
        var (commands, inputs, _) = DecodeExecute(result.Calldata);

        Assert.Equal("0x0b0004", commands);
        Assert.Equal(Utilities.ToHex(new BigInteger(1000000000000000000)), result.Value);

        var wrap = Decode(new[] { "address", "uint256" }, inputs[0]);
        Assert.Equal(Constants.ROUTER_AS_RECIPIENT, wrap[0]);
        Assert.Equal("1000000000000000000", ((BigInteger)wrap[1]!).ToString());
    }

    [Fact]
    public void EncodeSwaps_ExactInputErc20ToNative()
    {
        var spec = BuildSpec(slippage: new Percent(25, 1000), outputToken: ETH, quote: CA(ETH, "500000000000000000"));
        var steps = new SwapStep[]
        {
            BuildV3ExactInStep(),
            new UnwrapWeth(Constants.ROUTER_AS_RECIPIENT, "0"),
        };
        var result = SwapRouter.EncodeSwaps(spec, steps);
        var (commands, inputs, _) = DecodeExecute(result.Calldata);

        Assert.Equal("0x02000c04", commands);
        var sweep = Decode(new[] { "address", "address", "uint256" }, inputs[3]);
        Assert.Equal(Constants.ETH_ADDRESS.ToLowerInvariant(), ((string)sweep[0]!).ToLowerInvariant());
        Assert.Equal(TEST_RECIPIENT.ToLowerInvariant(), ((string)sweep[1]!).ToLowerInvariant());
    }

    [Fact]
    public void EncodeSwaps_ExactOutputErc20ToErc20WithRefundSweep()
    {
        var spec = BuildSpec(
            tradeType: TradeType.EXACT_OUTPUT, slippage: new Percent(5, 100),
            outputToken: DAI, amount: CA(DAI, "500000000000000000"), quote: CA(USDC, "1000000"));
        var maxIn = ExactOutputMaxIn(new BigInteger(1000000), spec.SlippageTolerance);
        var result = SwapRouter.EncodeSwaps(spec, new SwapStep[]
        {
            BuildV3ExactOutStep(amountInMax: maxIn.ToString(), tokens: new BaseCurrency[] { USDC, WETH, DAI }, fees: new[] { 500, 3000 }),
        });
        var (commands, inputs, _) = DecodeExecute(result.Calldata);

        Assert.Equal("0x02010404", commands);

        var transfer = Decode(new[] { "address", "address", "uint160" }, inputs[0]);
        Assert.Equal(maxIn.ToString(), ((BigInteger)transfer[2]!).ToString());

        var settlement = Decode(new[] { "address", "address", "uint256" }, inputs[2]);
        Assert.Equal(DAI.Address.ToLowerInvariant(), ((string)settlement[0]!).ToLowerInvariant());
        Assert.Equal("500000000000000000", ((BigInteger)settlement[2]!).ToString());

        var refund = Decode(new[] { "address", "address", "uint256" }, inputs[3]);
        Assert.Equal(USDC.Address.ToLowerInvariant(), ((string)refund[0]!).ToLowerInvariant());
        Assert.Equal("0", ((BigInteger)refund[2]!).ToString());
    }

    [Fact]
    public void EncodeSwaps_ExactOutputNativeInputRefundValue()
    {
        var spec = BuildSpec(
            tradeType: TradeType.EXACT_OUTPUT, slippage: new Percent(5, 100),
            inputToken: ETH, outputToken: USDC, amount: CA(USDC, "2000000000"), quote: CA(ETH, "1000000000000000000"));
        var maxIn = ExactOutputMaxIn(new BigInteger(1000000000000000000), spec.SlippageTolerance);
        var steps = new SwapStep[]
        {
            new WrapEth(Constants.ROUTER_AS_RECIPIENT, maxIn.ToString()),
            BuildV3ExactOutStep(amountOut: "2000000000", amountInMax: maxIn.ToString(), tokens: new BaseCurrency[] { WETH, USDC }),
            new UnwrapWeth(Constants.ROUTER_AS_RECIPIENT, "0"),
        };
        var result = SwapRouter.EncodeSwaps(spec, steps);
        var (commands, _, _) = DecodeExecute(result.Calldata);

        Assert.Equal(Utilities.ToHex(maxIn), result.Value);
        Assert.Equal(new List<int>
        {
            (int)CommandType.WRAP_ETH, (int)CommandType.V3_SWAP_EXACT_OUT, (int)CommandType.UNWRAP_WETH,
            (int)CommandType.SWEEP, (int)CommandType.SWEEP,
        }, CommandTypes(commands));
    }
}
