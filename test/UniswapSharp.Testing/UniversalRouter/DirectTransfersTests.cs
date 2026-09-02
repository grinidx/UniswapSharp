using System.Numerics;
using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.UniversalRouter;
using UniswapSharp.UniversalRouter.Entities.Actions;
using UniswapSharp.UniversalRouter.Types;
using UniswapSharp.UniversalRouter.Utils;
using UniswapSharp.V4;
using UniswapSharp.V4.Entities;
using UniswapSharp.V4.Utils;
using Constants = UniswapSharp.UniversalRouter.Utils.Constants;
using TradeType = UniswapSharp.Core.TradeType;

namespace UniswapSharp.Testing.UniversalRouter;

// Ported from sdks/universal-router-sdk/test/unit/directTransfers.test.ts (upstream #638)
public class DirectTransfersTests
{
    private const string ROUTER = Constants.ROUTER_AS_RECIPIENT;
    private const string SENDER = Constants.SENDER_AS_RECIPIENT;
    private static readonly string TEST_RECIPIENT = UniswapData.TEST_RECIPIENT_ADDRESS;
    private static readonly string FEE_RECIPIENT = UniswapData.TEST_FEE_RECIPIENT_ADDRESS;
    private static readonly Token USDC = UniswapData.USDC;
    private static readonly Token WETH = UniswapData.WETH;
    private static readonly Token DAI = UniswapData.DAI;

    // ---- fixtures ----

    private static string PackV3Path(string[] tokens, int[] fees)
    {
        string path = tokens[0].ToLowerInvariant().Replace("0x", "");
        for (int i = 0; i < fees.Length; i++)
        {
            path += fees[i].ToString("x6") + tokens[i + 1].ToLowerInvariant().Replace("0x", "");
        }
        return "0x" + path;
    }

    // exact-out paths are encoded output-first
    private static string PackV3ExactOutPath(string[] tokens, int[] fees)
    {
        string path = tokens[^1].ToLowerInvariant().Replace("0x", "");
        for (int i = fees.Length - 1; i >= 0; i--)
        {
            path += fees[i].ToString("x6") + tokens[i].ToLowerInvariant().Replace("0x", "");
        }
        return "0x" + path;
    }

    private static NormalizedSwapSpecification BuildSpec(
        bool allowDirectTransfers = false,
        string? recipient = null,
        Fee? fee = null,
        TokenTransferMode? tokenTransferMode = null,
        Permit2Permit? permit = null,
        bool? nativeErc20Input = null,
        Percent? slippage = null,
        BaseCurrency? inputToken = null,
        int? chainId = null,
        UniversalRouterVersion? urVersion = null) => new()
        {
            TradeType = TradeType.EXACT_INPUT,
            Routing = new SwapRouting(
                inputToken ?? USDC,
                WETH,
                CurrencyAmount<BaseCurrency>.FromRawAmount((BaseCurrency)(inputToken ?? USDC), BigInteger.Parse("1000000")),
                CurrencyAmount<BaseCurrency>.FromRawAmount((BaseCurrency)WETH, BigInteger.Parse("500000000000000000"))),
            Recipient = recipient ?? TEST_RECIPIENT,
            SlippageTolerance = slippage ?? new Percent(5, 100),
            TokenTransferMode = tokenTransferMode ?? UniswapSharp.UniversalRouter.Entities.Actions.TokenTransferMode.Permit2,
            UrVersion = urVersion ?? UniversalRouterVersion.V2_0,
            SafeMode = false,
            AllowDirectTransfers = allowDirectTransfers,
            Fee = fee,
            Permit = permit,
            NativeErc20Input = nativeErc20Input,
            ChainId = chainId,
        };

    // exact-output mirror: 0.5 WETH out exact, 1 USDC quote, 5% slippage -> maxIn 1_050_000
    private static NormalizedSwapSpecification BuildExactOutSpec(bool allowDirectTransfers = false) => new()
    {
        TradeType = TradeType.EXACT_OUTPUT,
        Routing = new SwapRouting(
            USDC, WETH,
            CurrencyAmount<BaseCurrency>.FromRawAmount((BaseCurrency)WETH, BigInteger.Parse("500000000000000000")),
            CurrencyAmount<BaseCurrency>.FromRawAmount((BaseCurrency)USDC, BigInteger.Parse("1000000"))),
        Recipient = TEST_RECIPIENT,
        SlippageTolerance = new Percent(5, 100),
        TokenTransferMode = UniswapSharp.UniversalRouter.Entities.Actions.TokenTransferMode.Permit2,
        UrVersion = UniversalRouterVersion.V2_0,
        SafeMode = false,
        AllowDirectTransfers = allowDirectTransfers,
    };

    private static V3SwapExactIn V3ExactIn(string? recipient = null, bool? payerIsUser = null, object? amountIn = null, string? path = null) =>
        new(recipient ?? ROUTER, amountIn ?? "1000000", "0",
            path ?? PackV3Path(new[] { USDC.Address, WETH.Address }, new[] { 500 }), null, payerIsUser);

    private static V3SwapExactOut V3ExactOut(string? recipient = null, bool? payerIsUser = null) =>
        new(recipient ?? ROUTER, "500000000000000000", "1050000",
            PackV3ExactOutPath(new[] { USDC.Address, WETH.Address }, new[] { 500 }), null, payerIsUser);

    private static V2SwapExactIn V2ExactIn(string? recipient = null, bool? payerIsUser = null, object? amountIn = null) =>
        new(recipient ?? ROUTER, amountIn ?? "1000000", "0", new[] { USDC.Address, WETH.Address }, null, payerIsUser);

    private static V2SwapExactOut V2ExactOut(string? recipient = null, bool? payerIsUser = null) =>
        new(recipient ?? ROUTER, "500000000000000000", "1050000", new[] { USDC.Address, WETH.Address }, null, payerIsUser);

    private static V4Swap V4SettleSwap(bool? payerIsUser = null, object? settleAmount = null) => new(new List<V4Action>
    {
        new V4SwapExactInSingle(
            new PoolKey(USDC.Address, WETH.Address, 500, 10, Constants.ETH_ADDRESS),
            true, "1000000", "0", "0x"),
        new V4Settle(USDC.Address, settleAmount ?? "1000000", payerIsUser),
        new V4Take(WETH.Address, ROUTER, "0"),
    });

    private static void AssertInvariant(string code, Action action) =>
        Assert.Contains(code, Assert.Throws<InvalidOperationException>(action).Message);

    // ---- normalizeEncodeSwapsSpec ----

    [Fact]
    public void Normalize_DefaultsAllowDirectTransfersToFalse()
    {
        var normalized = NormalizeEncodeSwapsSpec.Normalize(new SwapSpecification
        {
            TradeType = TradeType.EXACT_INPUT,
            Routing = BuildSpec().Routing,
            SlippageTolerance = new Percent(5, 100),
        });
        Assert.False(normalized.AllowDirectTransfers);
    }

    [Fact]
    public void Normalize_PreservesAllowDirectTransfersWhenSet()
    {
        var normalized = NormalizeEncodeSwapsSpec.Normalize(new SwapSpecification
        {
            TradeType = TradeType.EXACT_INPUT,
            Routing = BuildSpec().Routing,
            SlippageTolerance = new Percent(5, 100),
            AllowDirectTransfers = true,
        });
        Assert.True(normalized.AllowDirectTransfers);
    }

    // ---- encodeSwapStep payerIsUser threading ----

    private static bool DecodePayerIsUser(SwapStep step, CommandType expected)
    {
        var planner = new RoutePlanner();
        EncodeSwapStep.Encode(planner, step, UniversalRouterVersion.V2_0);
        Assert.Contains(((int)expected).ToString("x2"), planner.Commands);

        string[] types = expected is CommandType.V2_SWAP_EXACT_IN or CommandType.V2_SWAP_EXACT_OUT
            ? new[] { "address", "uint256", "uint256", "address[]", "bool" }
            : new[] { "address", "uint256", "uint256", "bytes", "bool" };
        return (bool)AbiParamDecoder.Decode(types, planner.Inputs[0])[4]!;
    }

    [Fact]
    public void EncodeSwapStep_ThreadsPayerIsUser()
    {
        Assert.True(DecodePayerIsUser(V3ExactIn(payerIsUser: true), CommandType.V3_SWAP_EXACT_IN));
        Assert.False(DecodePayerIsUser(V3ExactIn(), CommandType.V3_SWAP_EXACT_IN));
        Assert.True(DecodePayerIsUser(V3ExactOut(payerIsUser: true), CommandType.V3_SWAP_EXACT_OUT));
        Assert.True(DecodePayerIsUser(V2ExactIn(payerIsUser: true), CommandType.V2_SWAP_EXACT_IN));
        Assert.True(DecodePayerIsUser(V2ExactOut(payerIsUser: true), CommandType.V2_SWAP_EXACT_OUT));
        Assert.False(DecodePayerIsUser(V2ExactOut(), CommandType.V2_SWAP_EXACT_OUT));
    }

    [Fact]
    public void EncodeV4Action_ThreadsSettlePayerIsUser()
    {
        var (_, withFlag) = EncodeV4Action.Encode(new V4Settle(USDC.Address, "1000000", true), UniversalRouterVersion.V2_0);
        Assert.Equal(true, withFlag[2]);

        var (_, withoutFlag) = EncodeV4Action.Encode(new V4Settle(USDC.Address, "1000000"), UniversalRouterVersion.V2_0);
        Assert.Equal(false, withoutFlag[2]);
    }

    // ---- safe mode (allowDirectTransfers = false) gating ----

    [Fact]
    public void SafeMode_RejectsPayerIsUserOnEveryStepShape()
    {
        var spec = BuildSpec();
        foreach (SwapStep step in new SwapStep[]
        {
            V3ExactIn(payerIsUser: true), V3ExactOut(payerIsUser: true),
            V2ExactIn(payerIsUser: true), V2ExactOut(payerIsUser: true),
            V4SettleSwap(payerIsUser: true),
        })
        {
            AssertInvariant("PAYER_IS_USER_REQUIRES_DIRECT_TRANSFERS",
                () => ValidateEncodeSwaps.Validate(spec, new[] { step }));
        }
    }

    [Fact]
    public void SafeMode_RejectsV4SettleAllAndTakeAll()
    {
        var spec = BuildSpec();

        AssertInvariant("SETTLE_ALL_REQUIRES_DIRECT_TRANSFERS", () => ValidateEncodeSwaps.Validate(spec,
            new SwapStep[] { new V4Swap(new List<V4Action> { new V4SettleAll(USDC.Address, "1000000") }) }));

        AssertInvariant("TAKE_ALL_REQUIRES_DIRECT_TRANSFERS", () => ValidateEncodeSwaps.Validate(spec,
            new SwapStep[] { new V4Swap(new List<V4Action> { new V4TakeAll(WETH.Address, "0") }) }));
    }

    [Fact]
    public void SafeMode_StillAcceptsPlainCustodyPlans()
    {
        ValidateEncodeSwaps.Validate(BuildSpec(), new SwapStep[] { V3ExactIn() });
        ValidateEncodeSwaps.Validate(BuildSpec(), new SwapStep[] { V4SettleSwap() });
    }

    [Fact]
    public void DirectTransfers_AcceptsTheSameShapesWhenFlagIsSet()
    {
        var spec = BuildSpec(allowDirectTransfers: true);
        ValidateEncodeSwaps.Validate(spec, new SwapStep[] { V3ExactIn(payerIsUser: true) });
        ValidateEncodeSwaps.Validate(spec, new SwapStep[] { V4SettleSwap(payerIsUser: true) });
    }

    // ---- v3 path token extraction ----

    [Fact]
    public void V3PathTokens_ReturnNullForMalformedPaths()
    {
        Assert.Null(DirectTransfers.V3PathFirstToken("0x" + new string('a', 40)));   // single address
        Assert.Null(DirectTransfers.V3PathLastToken("0x" + new string('a', 90)));    // hop-misaligned
        Assert.Null(DirectTransfers.GetV3HopCount("nothex"));
    }

    [Fact]
    public void V3PathTokens_PreserveAddressCasingFromChecksummedPaths()
    {
        string checksummed = "0x" + "C02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2" + "0001f4" +
            "A0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";
        Assert.Equal("0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2", DirectTransfers.V3PathFirstToken(checksummed));
        Assert.Equal("0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48", DirectTransfers.V3PathLastToken(checksummed));
    }

    [Fact]
    public void StepUserPaidPulls_WrapUnwrapAndSettleFreeStepsProduceNoPulls()
    {
        Assert.Empty(DirectTransfers.StepUserPaidPulls(new WrapEth(ROUTER, "1000")));
        Assert.Empty(DirectTransfers.StepUserPaidPulls(new UnwrapWeth(ROUTER, "1000")));
        Assert.Empty(DirectTransfers.StepUserPaidPulls(V4SettleSwap()));
        Assert.Empty(DirectTransfers.StepUserPaidPulls(V3ExactIn()));
    }

    [Fact]
    public void StepUserPaidPulls_BindsV3ExactOutToThePathTail()
    {
        var pull = Assert.Single(DirectTransfers.StepUserPaidPulls(V3ExactOut(payerIsUser: true)));
        Assert.Equal(USDC.Address.ToLowerInvariant(), pull.Token!.ToLowerInvariant());
        Assert.Equal(BigInteger.Parse("1050000"), pull.MaxAmount);
    }

    // ---- budgeted mode: inbound ----

    [Fact]
    public void Inbound_AcceptsAUserPaidStepConsumingTheWholeBudget()
    {
        ValidateEncodeSwaps.Validate(BuildSpec(allowDirectTransfers: true),
            new SwapStep[] { V3ExactIn(payerIsUser: true, amountIn: "1000000") });
    }

    [Fact]
    public void Inbound_RejectsPullsAboveTheBudget()
    {
        AssertInvariant("USER_PAID_EXCEEDS_MAX_INPUT", () => ValidateEncodeSwaps.Validate(
            BuildSpec(allowDirectTransfers: true),
            new SwapStep[] { V3ExactIn(payerIsUser: true, amountIn: "1000001") }));
    }

    [Fact]
    public void Inbound_SumsPullsAcrossStepsAgainstTheBudget()
    {
        var spec = BuildSpec(allowDirectTransfers: true);
        ValidateEncodeSwaps.Validate(spec, new SwapStep[]
        {
            V3ExactIn(payerIsUser: true, amountIn: "600000"),
            V2ExactIn(payerIsUser: true, amountIn: "400000"),
        });

        AssertInvariant("USER_PAID_EXCEEDS_MAX_INPUT", () => ValidateEncodeSwaps.Validate(spec, new SwapStep[]
        {
            V3ExactIn(payerIsUser: true, amountIn: "600000"),
            V2ExactIn(payerIsUser: true, amountIn: "400001"),
        }));
    }

    [Fact]
    public void Inbound_CountsSettleAllMaxAmountTowardTheBudget()
    {
        AssertInvariant("USER_PAID_EXCEEDS_MAX_INPUT", () => ValidateEncodeSwaps.Validate(
            BuildSpec(allowDirectTransfers: true),
            new SwapStep[] { new V4Swap(new List<V4Action> { new V4SettleAll(USDC.Address, "1000001") }) }));
    }

    [Fact]
    public void Inbound_ExactOutputBudgetUsesTheSlippagePaddedMaxInput()
    {
        // maxIn = 1_000_000 * 1.05 = 1_050_000
        ValidateEncodeSwaps.Validate(BuildExactOutSpec(allowDirectTransfers: true),
            new SwapStep[] { V3ExactOut(payerIsUser: true) });
    }

    [Fact]
    public void Inbound_RejectsZeroAndSentinelAmounts()
    {
        var spec = BuildSpec(allowDirectTransfers: true);

        AssertInvariant("USER_PAID_AMOUNT_OUT_OF_RANGE", () => ValidateEncodeSwaps.Validate(spec,
            new SwapStep[] { V3ExactIn(payerIsUser: true, amountIn: "0") }));

        AssertInvariant("USER_PAID_AMOUNT_OUT_OF_RANGE", () => ValidateEncodeSwaps.Validate(spec,
            new SwapStep[] { V3ExactIn(payerIsUser: true, amountIn: Constants.CONTRACT_BALANCE) }));
    }

    [Fact]
    public void Inbound_AcceptsExactlyMaxUint160AndRejectsOneAbove()
    {
        // budget must clear the pull, so give the spec a huge input amount
        NormalizedSwapSpecification HugeSpec() => new()
        {
            TradeType = TradeType.EXACT_INPUT,
            Routing = new SwapRouting(USDC, WETH,
                CurrencyAmount<BaseCurrency>.FromRawAmount((BaseCurrency)USDC, Constants.MAX_UINT160),
                CurrencyAmount<BaseCurrency>.FromRawAmount((BaseCurrency)WETH, BigInteger.One)),
            Recipient = TEST_RECIPIENT,
            SlippageTolerance = new Percent(5, 100),
            TokenTransferMode = UniswapSharp.UniversalRouter.Entities.Actions.TokenTransferMode.Permit2,
            UrVersion = UniversalRouterVersion.V2_0,
            SafeMode = false,
            AllowDirectTransfers = true,
        };

        ValidateEncodeSwaps.Validate(HugeSpec(),
            new SwapStep[] { V3ExactIn(payerIsUser: true, amountIn: Constants.MAX_UINT160) });

        AssertInvariant("USER_PAID_AMOUNT_OUT_OF_RANGE", () => ValidateEncodeSwaps.Validate(HugeSpec(),
            new SwapStep[] { V3ExactIn(payerIsUser: true, amountIn: Constants.MAX_UINT160 + 1) }));
    }

    [Fact]
    public void Inbound_RejectsOffTokenPulls()
    {
        AssertInvariant("USER_PAID_INPUT_TOKEN_MISMATCH", () => ValidateEncodeSwaps.Validate(
            BuildSpec(allowDirectTransfers: true),
            new SwapStep[]
            {
                V3ExactIn(payerIsUser: true, path: PackV3Path(new[] { DAI.Address, WETH.Address }, new[] { 500 })),
            }));
    }

    [Fact]
    public void Inbound_RejectsMalformedPathsOnUserPaidSteps()
    {
        AssertInvariant("USER_PAID_MALFORMED_PATH", () => ValidateEncodeSwaps.Validate(
            BuildSpec(allowDirectTransfers: true),
            new SwapStep[] { V3ExactIn(payerIsUser: true, path: "0x" + new string('a', 40)) }));
    }

    [Fact]
    public void Inbound_NamesTheOffendingStepInTheError()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => ValidateEncodeSwaps.Validate(
            BuildSpec(allowDirectTransfers: true),
            new SwapStep[] { V3ExactIn(), V3ExactIn(payerIsUser: true, amountIn: "0") }));
        Assert.Contains("step 1", ex.Message);
    }

    [Fact]
    public void Inbound_RejectsNativeAndNativeErc20AndApproveProxy()
    {
        AssertInvariant("DIRECT_TRANSFERS_NATIVE_INPUT", () => ValidateEncodeSwaps.Validate(
            BuildSpec(allowDirectTransfers: true, inputToken: Ether.OnChain(1)),
            new SwapStep[] { V3ExactIn(payerIsUser: true) }));

        AssertInvariant("DIRECT_TRANSFERS_NATIVE_ERC20_INPUT", () => ValidateEncodeSwaps.Validate(
            BuildSpec(allowDirectTransfers: true, nativeErc20Input: true),
            new SwapStep[] { V3ExactIn(payerIsUser: true) }));

        AssertInvariant("DIRECT_TRANSFERS_REQUIRES_PERMIT2", () => ValidateEncodeSwaps.Validate(
            BuildSpec(allowDirectTransfers: true, chainId: 1,
                tokenTransferMode: UniswapSharp.UniversalRouter.Entities.Actions.TokenTransferMode.ApproveProxy),
            new SwapStep[] { V3ExactIn(payerIsUser: true) }));
    }

    [Fact]
    public void Inbound_ValidatesThePermitAgainstTheBudget()
    {
        Permit2Permit Permit(string token, string amount) => new(
            new UniswapSharp.Permit2.PermitDetails(token, BigInteger.Parse(amount), BigInteger.Parse("2000000000"), 0),
            "0x0000000000000000000000000000000000000001",
            BigInteger.Parse("2000000000"),
            "0x" + new string('0', 130));

        AssertInvariant("PERMIT_AMOUNT_INSUFFICIENT", () => ValidateEncodeSwaps.Validate(
            BuildSpec(allowDirectTransfers: true, permit: Permit(USDC.Address, "999999")),
            new SwapStep[] { V3ExactIn(payerIsUser: true) }));

        AssertInvariant("PERMIT_TOKEN_MISMATCH", () => ValidateEncodeSwaps.Validate(
            BuildSpec(allowDirectTransfers: true, permit: Permit(DAI.Address, "1000000")),
            new SwapStep[] { V3ExactIn(payerIsUser: true) }));

        // covering the budget is fine
        ValidateEncodeSwaps.Validate(
            BuildSpec(allowDirectTransfers: true, permit: Permit(USDC.Address, "1000000")),
            new SwapStep[] { V3ExactIn(payerIsUser: true) });
    }

    [Fact]
    public void Inbound_SkipsGatingWhenNoUserPaidPullsExist()
    {
        // flag on, custody steps, native input: the inbound block must not fire
        ValidateEncodeSwaps.Validate(
            BuildSpec(allowDirectTransfers: true, inputToken: Ether.OnChain(1)),
            new SwapStep[] { V3ExactIn() });
    }

    [Fact]
    public void Inbound_RejectsInvalidV4HookData()
    {
        AssertInvariant("V4_HOOK_DATA_INVALID", () => ValidateEncodeSwaps.Validate(
            BuildSpec(allowDirectTransfers: true),
            new SwapStep[]
            {
                new V4Swap(new List<V4Action>
                {
                    new V4SwapExactInSingle(
                        new PoolKey(USDC.Address, WETH.Address, 500, 10, Constants.ETH_ADDRESS),
                        true, "1000000", "0", ""),
                }),
            }));
    }

    // ---- budgeted mode: ingress remainder ----

    private static (List<int> types, List<string> inputs) ParseCommands(string calldata)
    {
        string body = "0x" + calldata[10..];
        var d = calldata[..10] == SwapRouter.GetSighash("execute(bytes,bytes[],uint256)")
            ? AbiParamDecoder.Decode(new[] { "bytes", "bytes[]", "uint256" }, body)
            : AbiParamDecoder.Decode(new[] { "bytes", "bytes[]" }, body);

        var commands = (string)d[0]!;
        var inputs = ((List<object?>)d[1]!).Select(x => (string)x!).ToList();
        var types = new List<int>();
        for (int i = 2; i < commands.Length; i += 2)
        {
            types.Add(Convert.ToInt32(commands.Substring(i, 2), 16) & 0x3f);
        }
        return (types, inputs);
    }

    private static SwapSpecification ToSpec(NormalizedSwapSpecification n) => new()
    {
        TradeType = n.TradeType,
        Routing = n.Routing,
        SlippageTolerance = n.SlippageTolerance,
        Recipient = n.Recipient,
        Fee = n.Fee,
        TokenTransferMode = n.TokenTransferMode,
        Permit = n.Permit,
        UrVersion = n.UrVersion,
        SafeMode = n.SafeMode,
        NativeErc20Input = n.NativeErc20Input,
        AllowDirectTransfers = n.AllowDirectTransfers,
    };

    [Fact]
    public void Ingress_OmitsPermit2TransferFromWhenPullsConsumeTheWholeBudget()
    {
        var mp = SwapRouter.EncodeSwaps(ToSpec(BuildSpec(allowDirectTransfers: true)),
            new SwapStep[] { V3ExactIn(payerIsUser: true, amountIn: "1000000") });

        var (types, _) = ParseCommands(mp.Calldata);
        Assert.DoesNotContain((int)CommandType.PERMIT2_TRANSFER_FROM, types);
    }

    [Fact]
    public void Ingress_PullsOnlyTheRemainderWhenPullsCoverPartOfTheBudget()
    {
        var mp = SwapRouter.EncodeSwaps(ToSpec(BuildSpec(allowDirectTransfers: true)),
            new SwapStep[] { V3ExactIn(payerIsUser: true, amountIn: "600000"), V3ExactIn(amountIn: "400000") });

        var (types, inputs) = ParseCommands(mp.Calldata);
        int idx = types.IndexOf((int)CommandType.PERMIT2_TRANSFER_FROM);
        Assert.NotEqual(-1, idx);

        var decoded = AbiParamDecoder.Decode(new[] { "address", "address", "uint160" }, inputs[idx]);
        Assert.Equal(BigInteger.Parse("400000"), (BigInteger)decoded[2]!);
    }

    [Fact]
    public void Ingress_EncodesByteIdenticallyToSafeModeWhenNoDirectStepsAreUsed()
    {
        var steps = new SwapStep[] { V3ExactIn() };
        var safe = SwapRouter.EncodeSwaps(ToSpec(BuildSpec()), steps);
        var direct = SwapRouter.EncodeSwaps(ToSpec(BuildSpec(allowDirectTransfers: true)), steps);

        Assert.Equal(safe.Calldata, direct.Calldata);
    }

    // ---- budgeted mode: outbound recipients ----

    [Fact]
    public void Outbound_AcceptsStepsPayingTheSpecRecipientDirectly()
    {
        ValidateEncodeSwaps.Validate(BuildSpec(allowDirectTransfers: true),
            new SwapStep[] { V3ExactIn(recipient: TEST_RECIPIENT) });
    }

    [Fact]
    public void Outbound_RejectsRecipientsThatAreNeitherRouterNorSpecRecipient()
    {
        AssertInvariant("STEP_RECIPIENT_NOT_ALLOWED", () => ValidateEncodeSwaps.Validate(
            BuildSpec(allowDirectTransfers: true), new SwapStep[] { V3ExactIn(recipient: DAI.Address) }));
    }

    [Fact]
    public void Outbound_StillRejectsNonRouterRecipientsInSafeModeWithTheLegacyError()
    {
        AssertInvariant("STEP_RECIPIENT_MUST_BE_ROUTER", () => ValidateEncodeSwaps.Validate(
            BuildSpec(), new SwapStep[] { V3ExactIn(recipient: TEST_RECIPIENT) }));
    }

    [Fact]
    public void Outbound_RejectsDirectOutputStepsWhenAPortionFeeIsSet()
    {
        AssertInvariant("PORTION_FEE_REQUIRES_ROUTER_CUSTODY", () => ValidateEncodeSwaps.Validate(
            BuildSpec(allowDirectTransfers: true, fee: new PortionFee(FEE_RECIPIENT, new Percent(5, 100))),
            new SwapStep[] { V3ExactIn(recipient: TEST_RECIPIENT) }));
    }

    [Fact]
    public void Outbound_AllowsUserPaidInputAlongsideAPortionFeeWithCustodyOutput()
    {
        ValidateEncodeSwaps.Validate(
            BuildSpec(allowDirectTransfers: true, fee: new PortionFee(FEE_RECIPIENT, new Percent(5, 100))),
            new SwapStep[] { V3ExactIn(payerIsUser: true) });
    }

    [Fact]
    public void Outbound_AcceptsV4TakeAndTakePortionToTheSpecRecipient()
    {
        ValidateEncodeSwaps.Validate(BuildSpec(allowDirectTransfers: true), new SwapStep[]
        {
            new V4Swap(new List<V4Action>
            {
                new V4Take(WETH.Address, TEST_RECIPIENT, "1000"),
                new V4TakePortion(WETH.Address, TEST_RECIPIENT, "100"),
            }),
        });
    }

    [Fact]
    public void Outbound_RejectsV4TakeToAnUnrelatedAddress()
    {
        AssertInvariant("STEP_RECIPIENT_NOT_ALLOWED", () => ValidateEncodeSwaps.Validate(
            BuildSpec(allowDirectTransfers: true),
            new SwapStep[] { new V4Swap(new List<V4Action> { new V4Take(WETH.Address, DAI.Address, "1000") }) }));
    }

    [Fact]
    public void Outbound_AcceptsUnwrapWethToTheRecipientButKeepsWrapEthRouterOnly()
    {
        var spec = BuildSpec(allowDirectTransfers: true);
        ValidateEncodeSwaps.Validate(spec, new SwapStep[] { new UnwrapWeth(TEST_RECIPIENT, "0") });

        AssertInvariant("STEP_RECIPIENT_MUST_BE_ROUTER", () => ValidateEncodeSwaps.Validate(
            spec, new SwapStep[] { new WrapEth(TEST_RECIPIENT, "0") }));
    }

    [Fact]
    public void Outbound_TakeAllRequiresTheSenderSentinelRecipient()
    {
        AssertInvariant("TAKE_ALL_REQUIRES_SENDER_RECIPIENT", () => ValidateEncodeSwaps.Validate(
            BuildSpec(allowDirectTransfers: true),
            new SwapStep[] { new V4Swap(new List<V4Action> { new V4TakeAll(WETH.Address, "0") }) }));

        ValidateEncodeSwaps.Validate(BuildSpec(allowDirectTransfers: true, recipient: SENDER),
            new SwapStep[] { new V4Swap(new List<V4Action> { new V4TakeAll(WETH.Address, "0") }) });
    }

    [Fact]
    public void Outbound_BansTakeAllUnderAPortionFee()
    {
        AssertInvariant("PORTION_FEE_REQUIRES_ROUTER_CUSTODY", () => ValidateEncodeSwaps.Validate(
            BuildSpec(allowDirectTransfers: true, recipient: SENDER,
                fee: new PortionFee(FEE_RECIPIENT, new Percent(5, 100))),
            new SwapStep[] { new V4Swap(new List<V4Action> { new V4TakeAll(WETH.Address, "0") }) }));
    }

    [Fact]
    public void Outbound_MatchesStepRecipientsCaseInsensitively()
    {
        ValidateEncodeSwaps.Validate(BuildSpec(allowDirectTransfers: true),
            new SwapStep[] { V3ExactIn(recipient: TEST_RECIPIENT.ToUpperInvariant().Replace("0X", "0x")) });
    }

    [Fact]
    public void Outbound_RejectsTheSenderSentinelWhenTheSpecRecipientIsExplicit()
    {
        AssertInvariant("STEP_RECIPIENT_NOT_ALLOWED", () => ValidateEncodeSwaps.Validate(
            BuildSpec(allowDirectTransfers: true), new SwapStep[] { V3ExactIn(recipient: SENDER) }));
    }

    // ---- output coverage helpers ----

    [Fact]
    public void Coverage_DoesNotCreditV2ExactOutAmounts()
    {
        Assert.Equal(BigInteger.Zero, DirectTransfers.SumDirectOutputMin(
            new SwapStep[] { V2ExactOut(recipient: TEST_RECIPIENT) }, TEST_RECIPIENT, WETH.Address));
    }

    [Fact]
    public void Coverage_CountsUnwrapWethToTheRecipientAsNative()
    {
        Assert.Equal(BigInteger.Parse("1000"), DirectTransfers.SumDirectOutputMin(
            new SwapStep[] { new UnwrapWeth(TEST_RECIPIENT, "1000") }, TEST_RECIPIENT, Constants.ETH_ADDRESS));
    }

    [Fact]
    public void Coverage_CountsConcreteV4TakesButNotOpenDeltaOrContractBalance()
    {
        BigInteger Sum(object amount) => DirectTransfers.SumDirectOutputMin(
            new SwapStep[] { new V4Swap(new List<V4Action> { new V4Take(WETH.Address, TEST_RECIPIENT, amount) }) },
            TEST_RECIPIENT, WETH.Address);

        Assert.Equal(BigInteger.Parse("1000"), Sum("1000"));
        Assert.Equal(BigInteger.Zero, Sum("0"));                          // OPEN_DELTA
        Assert.Equal(BigInteger.Zero, Sum(Constants.CONTRACT_BALANCE));   // runtime-sized
    }

    [Fact]
    public void Coverage_CountsTakeAllMinAmountOnlyForTheSenderSentinelRecipient()
    {
        var steps = new SwapStep[] { new V4Swap(new List<V4Action> { new V4TakeAll(WETH.Address, "1000") }) };
        Assert.Equal(BigInteger.Parse("1000"), DirectTransfers.SumDirectOutputMin(steps, SENDER, WETH.Address));
        Assert.Equal(BigInteger.Zero, DirectTransfers.SumDirectOutputMin(steps, TEST_RECIPIENT, WETH.Address));
    }

    [Fact]
    public void Coverage_CountsTakePortionToTheRecipientAsZero()
    {
        Assert.Equal(BigInteger.Zero, DirectTransfers.SumDirectOutputMin(
            new SwapStep[] { new V4Swap(new List<V4Action> { new V4TakePortion(WETH.Address, TEST_RECIPIENT, "100") }) },
            TEST_RECIPIENT, WETH.Address));
    }

    [Fact]
    public void Coverage_AccumulatesDuplicateDirectLegs()
    {
        Assert.Equal(BigInteger.Parse("2000"), DirectTransfers.SumDirectOutputMin(new SwapStep[]
        {
            new V3SwapExactIn(TEST_RECIPIENT, "1", "1000", PackV3Path(new[] { USDC.Address, WETH.Address }, new[] { 500 })),
            new V3SwapExactIn(TEST_RECIPIENT, "1", "1000", PackV3Path(new[] { USDC.Address, WETH.Address }, new[] { 500 })),
        }, TEST_RECIPIENT, WETH.Address));
    }

    // ---- v4 sole-OPEN_DELTA-take credits ----

    private static V4Swap OpenDeltaSwap(IEnumerable<V4Action> extra) => new(
        new List<V4Action>
        {
            new V4SwapExactInSingle(
                new PoolKey(USDC.Address, WETH.Address, 500, 10, Constants.ETH_ADDRESS),
                true, "1000000", "5000", "0x"),
        }.Concat(extra).ToList());

    [Fact]
    public void OpenDelta_CreditsTheSwapMinimumThroughASoleOpenDeltaTakeToTheRecipient()
    {
        Assert.Equal(BigInteger.Parse("5000"), DirectTransfers.SumDirectOutputMin(
            new SwapStep[] { OpenDeltaSwap(new[] { new V4Take(WETH.Address, TEST_RECIPIENT, "0") }) },
            TEST_RECIPIENT, WETH.Address));
    }

    [Fact]
    public void OpenDelta_CreditsNothingWhenASecondTakeOfTheCurrencyExists()
    {
        Assert.Equal(BigInteger.Zero, DirectTransfers.SumDirectOutputMin(
            new SwapStep[]
            {
                OpenDeltaSwap(new V4Action[]
                {
                    new V4Take(WETH.Address, TEST_RECIPIENT, "0"),
                    new V4Take(WETH.Address, TEST_RECIPIENT, "0"),
                }),
            },
            TEST_RECIPIENT, WETH.Address));
    }

    [Fact]
    public void OpenDelta_CreditsNothingWhenTheBlockSettlesTheOutputCurrency()
    {
        Assert.Equal(BigInteger.Zero, DirectTransfers.SumDirectOutputMin(
            new SwapStep[]
            {
                OpenDeltaSwap(new V4Action[]
                {
                    new V4Settle(WETH.Address, "1"),
                    new V4Take(WETH.Address, TEST_RECIPIENT, "0"),
                }),
            },
            TEST_RECIPIENT, WETH.Address));
    }

    [Fact]
    public void OpenDelta_CreditsNothingWhenTheTakeGoesToTheRouterOrCarriesAConcreteAmount()
    {
        Assert.Equal(BigInteger.Zero, DirectTransfers.SumDirectOutputMin(
            new SwapStep[] { OpenDeltaSwap(new[] { new V4Take(WETH.Address, ROUTER, "0") }) },
            TEST_RECIPIENT, WETH.Address));

        // a concrete take credits its own amount, not the swap minimum
        Assert.Equal(BigInteger.Parse("7"), DirectTransfers.SumDirectOutputMin(
            new SwapStep[] { OpenDeltaSwap(new[] { new V4Take(WETH.Address, TEST_RECIPIENT, "7") }) },
            TEST_RECIPIENT, WETH.Address));
    }

    [Fact]
    public void OpenDelta_DoesNotCreditExactOutAmounts()
    {
        var swap = new V4Swap(new List<V4Action>
        {
            new V4SwapExactOutSingle(
                new PoolKey(USDC.Address, WETH.Address, 500, 10, Constants.ETH_ADDRESS),
                true, "5000", "1000000", "0x"),
            new V4Take(WETH.Address, TEST_RECIPIENT, "0"),
        });

        Assert.Equal(BigInteger.Zero,
            DirectTransfers.SumDirectOutputMin(new SwapStep[] { swap }, TEST_RECIPIENT, WETH.Address));
    }

    // ---- directTransferSweepFloor ----

    private static BigInteger Floor(NormalizedSwapSpecification spec, BigInteger netMin, params SwapStep[] steps) =>
        DirectTransfers.DirectTransferSweepFloor(spec, netMin, steps, WETH.Address);

    private static SwapStep DirectLeg(string minOut) =>
        new V3SwapExactIn(TEST_RECIPIENT, "1", minOut, PackV3Path(new[] { USDC.Address, WETH.Address }, new[] { 500 }));

    [Fact]
    public void SweepFloor_ForgivesASubBufferShortfall()
    {
        // netMin 1_000_000; 0.5bps = 50; shortfall 10 is forgiven
        Assert.Equal(BigInteger.Zero, Floor(BuildSpec(allowDirectTransfers: true), 1_000_000, DirectLeg("999990")));
    }

    [Fact]
    public void SweepFloor_EnforcesTheFullShortfallWhenItExceedsTheBuffer()
    {
        Assert.Equal(new BigInteger(100_000),
            Floor(BuildSpec(allowDirectTransfers: true), 1_000_000, DirectLeg("900000")));
    }

    [Fact]
    public void SweepFloor_DropsTheFlexibilityBufferToZeroAtZeroSlippage()
    {
        // only the per-leg rounding term (2 wei/leg) survives
        var spec = BuildSpec(allowDirectTransfers: true, slippage: new Percent(0, 100));
        Assert.Equal(BigInteger.Zero, Floor(spec, 1_000_000, DirectLeg("999998")));
        Assert.Equal(new BigInteger(3), Floor(spec, 1_000_000, DirectLeg("999997")));
    }

    [Fact]
    public void SweepFloor_HasNoOutputSideSlippageForExactOutput()
    {
        var spec = BuildExactOutSpec(allowDirectTransfers: true);
        Assert.Equal(BigInteger.Zero, Floor(spec, 1_000_000, DirectLeg("999998")));
        Assert.Equal(new BigInteger(3), Floor(spec, 1_000_000, DirectLeg("999997")));
    }

    [Fact]
    public void SweepFloor_ScalesTheRoundingTermByLegCount()
    {
        var spec = BuildSpec(allowDirectTransfers: true, slippage: new Percent(0, 100));
        // two legs -> 4 wei tolerated
        Assert.Equal(BigInteger.Zero, Floor(spec, 1_000_000, DirectLeg("499998"), DirectLeg("499998")));
    }

    [Fact]
    public void SweepFloor_DoesNotLetZeroMinPaddingLegsWidenTheRoundingTolerance()
    {
        var spec = BuildSpec(allowDirectTransfers: true, slippage: new Percent(0, 100));
        // the zero-min leg carries no flooring error, so tolerance stays at 2 wei
        Assert.Equal(new BigInteger(3), Floor(spec, 1_000_000, DirectLeg("999997"), DirectLeg("0")));
    }

    [Fact]
    public void SweepFloor_ClampsToZeroWhenDirectCoverageExceedsNetMin()
    {
        Assert.Equal(BigInteger.Zero,
            Floor(BuildSpec(allowDirectTransfers: true), 1_000_000, DirectLeg("2000000")));
    }

    [Fact]
    public void SweepFloor_DoesNotReduceTheFloorForDirectDeliveriesInOtherTokens()
    {
        var otherToken = new V3SwapExactIn(TEST_RECIPIENT, "1", "1000000",
            PackV3Path(new[] { USDC.Address, DAI.Address }, new[] { 500 }));

        Assert.Equal(new BigInteger(1_000_000),
            Floor(BuildSpec(allowDirectTransfers: true), 1_000_000, otherToken));
    }

    // ---- end-to-end sweep floor ----

    [Fact]
    public void EncodeSwaps_KeepsTheFullFloorWhenNothingIsDeliveredDirectly()
    {
        BigInteger SweepMin(bool allowDirect)
        {
            var mp = SwapRouter.EncodeSwaps(ToSpec(BuildSpec(allowDirectTransfers: allowDirect)),
                new SwapStep[] { V3ExactIn() });
            var (types, inputs) = ParseCommands(mp.Calldata);
            int idx = types.IndexOf((int)CommandType.SWEEP);
            return (BigInteger)AbiParamDecoder.Decode(new[] { "address", "address", "uint256" }, inputs[idx])[2]!;
        }

        // netMin = quote * (1 - 5%) = 475000000000000000
        Assert.Equal(BigInteger.Parse("475000000000000000"), SweepMin(false));
        Assert.Equal(BigInteger.Parse("475000000000000000"), SweepMin(true));
    }

    [Fact]
    public void EncodeSwaps_ReducesTheFloorByDirectOutputMinimums()
    {
        var mp = SwapRouter.EncodeSwaps(ToSpec(BuildSpec(allowDirectTransfers: true)), new SwapStep[]
        {
            new V3SwapExactIn(TEST_RECIPIENT, "1000000", "400000000000000000",
                PackV3Path(new[] { USDC.Address, WETH.Address }, new[] { 500 })),
        });

        var (types, inputs) = ParseCommands(mp.Calldata);
        int idx = types.IndexOf((int)CommandType.SWEEP);
        var sweepMin = (BigInteger)AbiParamDecoder.Decode(new[] { "address", "address", "uint256" }, inputs[idx])[2]!;

        // 475000000000000000 - 400000000000000000
        Assert.Equal(BigInteger.Parse("75000000000000000"), sweepMin);
    }
}
