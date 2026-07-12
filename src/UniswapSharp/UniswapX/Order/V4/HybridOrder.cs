using System.Numerics;
using UniswapSharp.Core.Utils;
using UniswapSharp.Permit2;
using UniswapSharp.UniswapX.Utils;
using UniswapSharp.V4.Utils;

namespace UniswapSharp.UniswapX.Order.V4;

/// <summary>Thrown when a hybrid order cannot be resolved (uniswapx-sdk <c>OrderResolutionError</c>).</summary>
public sealed class OrderResolutionError : Exception
{
    public OrderResolutionError(string message) : base(message) { }
}

/// <summary>Thrown on an invalid hybrid price curve (uniswapx-sdk <c>HybridOrderPriceCurveError</c>).</summary>
public sealed class HybridOrderPriceCurveError : Exception
{
    public HybridOrderPriceCurveError(string message) : base(message) { }
}

/// <summary>Thrown on an invalid hybrid cosignature (uniswapx-sdk <c>HybridOrderCosignatureError</c>).</summary>
public sealed class HybridOrderCosignatureError : Exception
{
    public HybridOrderCosignatureError(string message) : base(message) { }
}

/// <summary>Port of uniswapx-sdk <c>order/v4/HybridOrder.ts</c> (<c>UnsignedHybridOrder</c>).</summary>
public class UnsignedHybridOrder
{
    internal const string ZeroAddress = "0x0000000000000000000000000000000000000000";
    internal static readonly BigInteger Wad = BigInteger.Pow(10, 18);
    internal static readonly BigInteger MaxUint240 = (BigInteger.One << 240) - 1;
    internal const int MaxUint16 = 65535;
    internal const int PriceCurveDurationShift = 240;

    internal const string HybridOrderAbi =
        "tuple(tuple(address,address,uint256,uint256,address,bytes,address,bytes,address),address,tuple(address,uint256)," +
        "tuple(address,uint256,address)[],uint256,uint256,uint256,uint256[],tuple(uint256,uint256[],address,uint256,uint256),bytes)";

    public string Permit2Address { get; }
    public UnsignedHybridOrderInfo Info { get; }
    public int ChainId { get; }
    public string Resolver { get; }

    public UnsignedHybridOrder(UnsignedHybridOrderInfo info, int chainId, string resolver, string? permit2Address = null)
    {
        Info = info;
        ChainId = chainId;
        Resolver = resolver;
        Permit2Address = OrderUtils.GetPermit2(chainId, permit2Address);
    }

    public static UnsignedHybridOrder Parse(string encoded, int chainId, string? permit2 = null)
    {
        var (resolver, info) = ParseSerializedHybridOrder(encoded);
        var unsignedInfo = new UnsignedHybridOrderInfo
        {
            Reactor = info.Reactor,
            Swapper = info.Swapper,
            Nonce = info.Nonce,
            Deadline = info.Deadline,
            PreExecutionHook = info.PreExecutionHook,
            PreExecutionHookData = info.PreExecutionHookData,
            PostExecutionHook = info.PostExecutionHook,
            PostExecutionHookData = info.PostExecutionHookData,
            AuctionResolver = info.AuctionResolver,
            Cosigner = info.Cosigner,
            Input = info.Input,
            Outputs = info.Outputs,
            AuctionStartBlock = info.AuctionStartBlock,
            BaselinePriorityFee = info.BaselinePriorityFee,
            ScalingFactor = info.ScalingFactor,
            PriceCurve = info.PriceCurve,
        };
        return new UnsignedHybridOrder(unsignedInfo, chainId, resolver, permit2);
    }

    public static BigInteger EncodePriceCurveElement(int duration, BigInteger scalingFactor)
    {
        if (duration < 0 || duration > MaxUint16)
        {
            throw new HybridOrderPriceCurveError($"Duration must be between 0 and {MaxUint16} (fits in 16 bits)");
        }
        if (scalingFactor < 0 || scalingFactor > MaxUint240)
        {
            throw new HybridOrderPriceCurveError("Scaling factor must be between 0 and 2^240-1");
        }
        return EncodePriceCurveElementInternal(duration, scalingFactor);
    }

    public static (int Duration, BigInteger ScalingFactor) DecodePriceCurveElement(BigInteger value) =>
        DecodePriceCurveElementInternal(value);

    public virtual string Hash() => Hashing.HashHybridOrder(Info);

    public virtual string Serialize()
    {
        string orderData = AbiParamEncoder.Encode(new[] { HybridOrderAbi }, new object?[]
        {
            OrderTuple(EmptyCosignerTuple(), "0x"),
        });
        return AbiParamEncoder.Encode(new[] { "address", "bytes" }, new object?[] { Resolver, orderData });
    }

    protected object?[] OrderTuple(object?[] cosignerDataTuple, string cosignature) => new object?[]
    {
        new object?[]
        {
            Info.Reactor, Info.Swapper, Info.Nonce, Info.Deadline, Info.PreExecutionHook,
            Info.PreExecutionHookData, Info.PostExecutionHook, Info.PostExecutionHookData, Info.AuctionResolver,
        },
        Info.Cosigner,
        new object?[] { Info.Input.Token, Info.Input.MaxAmount },
        Info.Outputs.Select(o => new object?[] { o.Token, o.MinAmount, o.Recipient }).ToArray(),
        Info.AuctionStartBlock,
        Info.BaselinePriorityFee,
        Info.ScalingFactor,
        Info.PriceCurve.ToArray(),
        cosignerDataTuple,
        cosignature,
    };

    private static object?[] EmptyCosignerTuple() => new object?[] { 0, Array.Empty<object?>(), ZeroAddress, 0, 0 };

    public PermitData PermitData() =>
        SignatureTransfer.GetPermitData(ToPermit(), Permit2Address, ChainId, Witness());

    public string GetSigner(string signature)
    {
        var (domain, types, values) = SignatureTransfer.GetPermitData(ToPermit(), Permit2Address, ChainId, Witness());
        byte[] digest = Eip712TypedDataEncoder.Hash(domain, types, values);
        return OrderSigning.RecoverSigner(digest, signature);
    }

    protected PermitTransferFrom ToPermit() => new(
        new TokenPermissions(Info.Input.Token, Info.Input.MaxAmount),
        Info.PreExecutionHook,
        Info.Nonce,
        Info.Deadline);

    protected Witness Witness()
    {
        var value = new Dictionary<string, object?>
        {
            ["info"] = new Dictionary<string, object?>
            {
                ["reactor"] = Info.Reactor,
                ["swapper"] = Info.Swapper,
                ["nonce"] = Info.Nonce,
                ["deadline"] = (BigInteger)Info.Deadline,
                ["preExecutionHook"] = Info.PreExecutionHook,
                ["preExecutionHookData"] = Info.PreExecutionHookData,
                ["postExecutionHook"] = Info.PostExecutionHook,
                ["postExecutionHookData"] = Info.PostExecutionHookData,
                ["auctionResolver"] = Info.AuctionResolver,
            },
            ["cosigner"] = Info.Cosigner,
            ["input"] = new Dictionary<string, object?>
            {
                ["token"] = Info.Input.Token,
                ["maxAmount"] = Info.Input.MaxAmount,
            },
            ["outputs"] = Info.Outputs.Select(o => new Dictionary<string, object?>
            {
                ["token"] = o.Token,
                ["minAmount"] = o.MinAmount,
                ["recipient"] = o.Recipient,
            }).ToArray(),
            ["auctionStartBlock"] = Info.AuctionStartBlock,
            ["baselinePriorityFee"] = Info.BaselinePriorityFee,
            ["scalingFactor"] = Info.ScalingFactor,
            ["priceCurve"] = Info.PriceCurve.Cast<object?>().ToArray(),
        };
        return new Witness(value, "HybridOrder", Hashing.HybridOrderTypes);
    }

    public virtual BlockOverrides? BlockOverrides =>
        Info.AuctionStartBlock.IsZero ? null : new BlockOverrides(OrderUtils.HexStripZeros(Info.AuctionStartBlock));

    public virtual ResolvedUniswapXOrder Resolve(HybridOrderResolutionOptions options) =>
        throw new InvalidOperationException("Cannot resolve unsigned order - cosigner data required");

    public string CosignatureHash(HybridCosignerData cosignerData) =>
        Hashing.HashHybridCosignerData(Hash(), cosignerData, ChainId);

    internal static (string Resolver, CosignedHybridOrderInfo Info) ParseSerializedHybridOrder(string encoded)
    {
        var outer = AbiParamDecoder.Decode(new[] { "address", "bytes" }, encoded);
        string resolver = AddressValidator.GetAddress((string)outer[0]!);
        string orderData = (string)outer[1]!;

        var decoded = AbiParamDecoder.Decode(new[] { HybridOrderAbi }, orderData);
        var tuple = (List<object?>)decoded[0]!;
        var info = (List<object?>)tuple[0]!;
        var input = (List<object?>)tuple[2]!;
        var outputs = (List<object?>)tuple[3]!;
        var priceCurve = (List<object?>)tuple[7]!;
        var cosignerData = (List<object?>)tuple[8]!;

        var parsed = new CosignedHybridOrderInfo
        {
            Reactor = AddressValidator.GetAddress((string)info[0]!),
            Swapper = AddressValidator.GetAddress((string)info[1]!),
            Nonce = (BigInteger)info[2]!,
            Deadline = (long)(BigInteger)info[3]!,
            PreExecutionHook = AddressValidator.GetAddress((string)info[4]!),
            PreExecutionHookData = (string)info[5]!,
            PostExecutionHook = AddressValidator.GetAddress((string)info[6]!),
            PostExecutionHookData = (string)info[7]!,
            AuctionResolver = AddressValidator.GetAddress((string)info[8]!),
            Cosigner = AddressValidator.GetAddress((string)tuple[1]!),
            Input = new HybridInput
            {
                Token = AddressValidator.GetAddress((string)input[0]!),
                MaxAmount = (BigInteger)input[1]!,
            },
            Outputs = outputs.Select(o =>
            {
                var output = (List<object?>)o!;
                return new HybridOutput
                {
                    Token = AddressValidator.GetAddress((string)output[0]!),
                    MinAmount = (BigInteger)output[1]!,
                    Recipient = AddressValidator.GetAddress((string)output[2]!),
                };
            }).ToList(),
            AuctionStartBlock = (BigInteger)tuple[4]!,
            BaselinePriorityFee = (BigInteger)tuple[5]!,
            ScalingFactor = (BigInteger)tuple[6]!,
            PriceCurve = priceCurve.Select(x => (BigInteger)x!).ToList(),
            CosignerData = new HybridCosignerData
            {
                AuctionTargetBlock = (BigInteger)cosignerData[0]!,
                SupplementalPriceCurve = ((List<object?>)cosignerData[1]!).Select(x => (BigInteger)x!).ToList(),
                ExclusiveFiller = AddressValidator.GetAddress((string)cosignerData[2]!),
                ExclusivityOverrideBps = (BigInteger)cosignerData[3]!,
                ExclusivityEndBlock = (BigInteger)cosignerData[4]!,
            },
            Cosignature = (string)tuple[9]!,
        };
        return (resolver, parsed);
    }

    // ---- price-curve math helpers (shared with CosignedHybridOrder.Resolve) ----

    internal static (int Duration, BigInteger ScalingFactor) DecodePriceCurveElementInternal(BigInteger value)
    {
        BigInteger scalingFactor = value & MaxUint240;
        int duration = (int)(value >> PriceCurveDurationShift);
        return (duration, scalingFactor);
    }

    internal static BigInteger EncodePriceCurveElementInternal(int duration, BigInteger scalingFactor) =>
        ((BigInteger)duration << PriceCurveDurationShift) | scalingFactor;

    internal static bool SharesScalingDirection(BigInteger a, BigInteger b)
    {
        if (a == Wad || b == Wad)
        {
            return true;
        }
        return a > Wad == b > Wad;
    }
}

/// <summary>Port of uniswapx-sdk <c>order/v4/HybridOrder.ts</c> (<c>CosignedHybridOrder</c>).</summary>
public sealed class CosignedHybridOrder : UnsignedHybridOrder
{
    public new CosignedHybridOrderInfo Info { get; }

    public CosignedHybridOrder(CosignedHybridOrderInfo info, int chainId, string resolver, string? permit2Address = null)
        : base(info, chainId, resolver, permit2Address)
    {
        Info = info;
    }

    public static new CosignedHybridOrder Parse(string encoded, int chainId, string? permit2 = null)
    {
        var (resolver, info) = ParseSerializedHybridOrder(encoded);
        return new CosignedHybridOrder(info, chainId, resolver, permit2);
    }

    public static CosignedHybridOrder FromUnsignedOrder(
        UnsignedHybridOrder order, HybridCosignerData cosignerData, string cosignature)
    {
        return new CosignedHybridOrder(
            new CosignedHybridOrderInfo
            {
                Reactor = order.Info.Reactor,
                Swapper = order.Info.Swapper,
                Nonce = order.Info.Nonce,
                Deadline = order.Info.Deadline,
                PreExecutionHook = order.Info.PreExecutionHook,
                PreExecutionHookData = order.Info.PreExecutionHookData,
                PostExecutionHook = order.Info.PostExecutionHook,
                PostExecutionHookData = order.Info.PostExecutionHookData,
                AuctionResolver = order.Info.AuctionResolver,
                Cosigner = order.Info.Cosigner,
                Input = order.Info.Input,
                Outputs = order.Info.Outputs,
                AuctionStartBlock = order.Info.AuctionStartBlock,
                BaselinePriorityFee = order.Info.BaselinePriorityFee,
                ScalingFactor = order.Info.ScalingFactor,
                PriceCurve = order.Info.PriceCurve,
                CosignerData = cosignerData,
                Cosignature = cosignature,
            },
            order.ChainId,
            order.Resolver,
            order.Permit2Address);
    }

    public override string Hash() => Hashing.HashHybridOrder(Info);

    public override string Serialize()
    {
        var cosignerTuple = new object?[]
        {
            Info.CosignerData.AuctionTargetBlock,
            Info.CosignerData.SupplementalPriceCurve.ToArray(),
            Info.CosignerData.ExclusiveFiller,
            Info.CosignerData.ExclusivityOverrideBps,
            Info.CosignerData.ExclusivityEndBlock,
        };
        string orderData = AbiParamEncoder.Encode(new[] { HybridOrderAbi }, new object?[]
        {
            OrderTuple(cosignerTuple, Info.Cosignature),
        });
        return AbiParamEncoder.Encode(new[] { "address", "bytes" }, new object?[] { Resolver, orderData });
    }

    public override BlockOverrides? BlockOverrides
    {
        get
        {
            BigInteger block = !Info.CosignerData.AuctionTargetBlock.IsZero
                ? Info.CosignerData.AuctionTargetBlock
                : Info.AuctionStartBlock;
            return block.IsZero ? null : new BlockOverrides(OrderUtils.HexStripZeros(block));
        }
    }

    public CosignedHybridOrderInfoJSON ToJSON() => new(
        Reactor: Info.Reactor,
        Swapper: Info.Swapper,
        Nonce: Info.Nonce.ToString(),
        Deadline: Info.Deadline,
        PreExecutionHook: Info.PreExecutionHook,
        PreExecutionHookData: Info.PreExecutionHookData,
        PostExecutionHook: Info.PostExecutionHook,
        PostExecutionHookData: Info.PostExecutionHookData,
        AuctionResolver: Info.AuctionResolver,
        Cosigner: Info.Cosigner,
        Input: new HybridInputJSON(Info.Input.Token, Info.Input.MaxAmount.ToString()),
        Outputs: Info.Outputs.Select(o => new HybridOutputJSON(o.Token, o.MinAmount.ToString(), o.Recipient)).ToList(),
        AuctionStartBlock: Info.AuctionStartBlock.ToString(),
        BaselinePriorityFee: Info.BaselinePriorityFee.ToString(),
        ScalingFactor: Info.ScalingFactor.ToString(),
        PriceCurve: Info.PriceCurve.Select(v => v.ToString()).ToList(),
        CosignerData: new HybridCosignerDataJSON(
            Info.CosignerData.AuctionTargetBlock.ToString(),
            Info.CosignerData.SupplementalPriceCurve.Select(v => v.ToString()).ToList(),
            Info.CosignerData.ExclusiveFiller,
            (long)Info.CosignerData.ExclusivityOverrideBps,
            Info.CosignerData.ExclusivityEndBlock.ToString()),
        Cosignature: Info.Cosignature);

    public static CosignedHybridOrder FromJSON(CosignedHybridOrderInfoJSON json, int chainId, string resolver, string? permit2Address = null) =>
        new(
            new CosignedHybridOrderInfo
            {
                Reactor = json.Reactor,
                Swapper = json.Swapper,
                Nonce = BigInteger.Parse(json.Nonce),
                Deadline = json.Deadline,
                PreExecutionHook = json.PreExecutionHook,
                PreExecutionHookData = json.PreExecutionHookData,
                PostExecutionHook = json.PostExecutionHook,
                PostExecutionHookData = json.PostExecutionHookData,
                AuctionResolver = json.AuctionResolver,
                Cosigner = json.Cosigner,
                Input = new HybridInput { Token = json.Input.Token, MaxAmount = BigInteger.Parse(json.Input.MaxAmount) },
                Outputs = json.Outputs.Select(o => new HybridOutput
                {
                    Token = o.Token,
                    MinAmount = BigInteger.Parse(o.MinAmount),
                    Recipient = o.Recipient,
                }).ToList(),
                AuctionStartBlock = BigInteger.Parse(json.AuctionStartBlock),
                BaselinePriorityFee = BigInteger.Parse(json.BaselinePriorityFee),
                ScalingFactor = BigInteger.Parse(json.ScalingFactor),
                PriceCurve = json.PriceCurve.Select(BigInteger.Parse).ToList(),
                CosignerData = new HybridCosignerData
                {
                    AuctionTargetBlock = BigInteger.Parse(json.CosignerData.AuctionTargetBlock),
                    SupplementalPriceCurve = json.CosignerData.SupplementalPriceCurve.Select(BigInteger.Parse).ToList(),
                    ExclusiveFiller = json.CosignerData.ExclusiveFiller,
                    ExclusivityOverrideBps = json.CosignerData.ExclusivityOverrideBps,
                    ExclusivityEndBlock = BigInteger.Parse(json.CosignerData.ExclusivityEndBlock),
                },
                Cosignature = json.Cosignature,
            },
            chainId,
            resolver,
            permit2Address);

    public string CosignatureHash() =>
        Hashing.HashHybridCosignerData(Hash(), Info.CosignerData, ChainId);

    public string RecoverCosigner()
    {
        string messageHash = CosignatureHash();
        byte[] hashBytes = Convert.FromHexString(messageHash[2..]);
        return OrderSigning.RecoverSigner(hashBytes, Info.Cosignature);
    }

    public override ResolvedUniswapXOrder Resolve(HybridOrderResolutionOptions options)
    {
        BigInteger auctionTargetBlock = Info.AuctionStartBlock;
        var effectivePriceCurve = Info.PriceCurve.ToList();

        if (Info.Cosigner != ZeroAddress)
        {
            string recovered = RecoverCosigner();
            if (AddressValidator.GetAddress(recovered) != AddressValidator.GetAddress(Info.Cosigner))
            {
                throw new HybridOrderCosignatureError("Invalid cosignature");
            }

            if (!Info.CosignerData.AuctionTargetBlock.IsZero)
            {
                auctionTargetBlock = Info.CosignerData.AuctionTargetBlock;
            }

            if (Info.CosignerData.SupplementalPriceCurve.Count > 0)
            {
                effectivePriceCurve = ApplySupplementalPriceCurve(effectivePriceCurve, Info.CosignerData.SupplementalPriceCurve);
            }
        }

        if (!auctionTargetBlock.IsZero && options.CurrentBlock < auctionTargetBlock)
        {
            throw new OrderResolutionError("Target block in the future");
        }

        BigInteger currentScalingFactor = DeriveCurrentScalingFactor(Info, effectivePriceCurve, auctionTargetBlock, options.CurrentBlock);

        BigInteger priorityFeeAboveBaseline = options.PriorityFeeWei > Info.BaselinePriorityFee
            ? options.PriorityFeeWei - Info.BaselinePriorityFee
            : BigInteger.Zero;

        BigInteger baseScalingFactor = ConstantsV4.BaseScalingFactor;
        bool useExactIn = Info.ScalingFactor > baseScalingFactor ||
            (Info.ScalingFactor == baseScalingFactor && currentScalingFactor >= baseScalingFactor);

        if (useExactIn)
        {
            BigInteger scalingMultiplier = currentScalingFactor + (Info.ScalingFactor - baseScalingFactor) * priorityFeeAboveBaseline;
            return new ResolvedUniswapXOrder(
                new TokenAmount(Info.Input.Token, Info.Input.MaxAmount),
                ScaleOutputs(Info.Outputs, scalingMultiplier));
        }
        else
        {
            BigInteger scalingMultiplier = currentScalingFactor - (baseScalingFactor - Info.ScalingFactor) * priorityFeeAboveBaseline;
            return new ResolvedUniswapXOrder(
                ScaleInput(Info.Input, scalingMultiplier),
                Info.Outputs.Select(o => new TokenAmount(o.Token, o.MinAmount)).ToList());
        }
    }

    // ---- resolve helper functions ----

    private static List<BigInteger> ApplySupplementalPriceCurve(List<BigInteger> priceCurve, IReadOnlyList<BigInteger> supplemental)
    {
        if (supplemental.Count == 0)
        {
            return priceCurve.ToList();
        }
        if (priceCurve.Count == 0)
        {
            throw new HybridOrderPriceCurveError("Supplemental curve provided without base curve");
        }

        var combined = priceCurve.ToList();
        int length = Math.Min(priceCurve.Count, supplemental.Count);
        for (int i = 0; i < length; i++)
        {
            var (duration, scalingFactor) = DecodePriceCurveElementInternal(priceCurve[i]);
            BigInteger supplementalScaling = supplemental[i];
            if (!SharesScalingDirection(scalingFactor, supplementalScaling))
            {
                throw new HybridOrderPriceCurveError("Supplemental scaling direction mismatch");
            }
            BigInteger mergedScaling = scalingFactor + supplementalScaling - ConstantsV4.BaseScalingFactor;
            if (mergedScaling < 0 || mergedScaling > MaxUint240)
            {
                throw new HybridOrderPriceCurveError("Supplemental scaling factor out of range");
            }
            combined[i] = EncodePriceCurveElementInternal(duration, mergedScaling);
        }
        return combined;
    }

    private static BigInteger DeriveCurrentScalingFactor(
        UnsignedHybridOrderInfo order, List<BigInteger> priceCurve, BigInteger targetBlock, BigInteger fillBlock)
    {
        if (targetBlock.IsZero)
        {
            if (priceCurve.Count != 0)
            {
                throw new HybridOrderPriceCurveError("Invalid target block designation");
            }
            return ConstantsV4.BaseScalingFactor;
        }

        if (targetBlock > fillBlock)
        {
            throw new OrderResolutionError("Invalid target block");
        }

        long blocksPassed = (long)(fillBlock - targetBlock);
        BigInteger currentScalingFactor = GetCalculatedScalingFactor(priceCurve, blocksPassed);

        if (!SharesScalingDirection(order.ScalingFactor, currentScalingFactor))
        {
            throw new HybridOrderPriceCurveError("Scaling direction mismatch");
        }
        return currentScalingFactor;
    }

    private static BigInteger GetCalculatedScalingFactor(List<BigInteger> parameters, long blocksPassed)
    {
        if (parameters.Count == 0)
        {
            return ConstantsV4.BaseScalingFactor;
        }

        long blocksCounted = 0;
        BigInteger? lastZeroDurationScaling = null;
        int previousDuration = 0;

        for (int i = 0; i < parameters.Count; i++)
        {
            var (duration, scalingFactor) = DecodePriceCurveElementInternal(parameters[i]);

            if (duration == 0)
            {
                if (blocksPassed >= blocksCounted)
                {
                    lastZeroDurationScaling = scalingFactor;
                    if (blocksPassed == blocksCounted)
                    {
                        return scalingFactor;
                    }
                }
                previousDuration = duration;
                continue;
            }

            long segmentEnd = blocksCounted + duration;
            if (blocksPassed < segmentEnd)
            {
                if (previousDuration == 0 && lastZeroDurationScaling is BigInteger lastZero)
                {
                    if (!SharesScalingDirection(lastZero, scalingFactor))
                    {
                        throw new HybridOrderPriceCurveError("Zero duration scaling mismatch");
                    }
                    return LocateCurrentAmount(lastZero, scalingFactor, blocksCounted, blocksPassed, segmentEnd, lastZero > ConstantsV4.BaseScalingFactor);
                }

                BigInteger endScalingFactor = i + 1 < parameters.Count
                    ? DecodePriceCurveElementInternal(parameters[i + 1]).ScalingFactor
                    : ConstantsV4.BaseScalingFactor;

                if (!SharesScalingDirection(scalingFactor, endScalingFactor))
                {
                    throw new HybridOrderPriceCurveError("Scaling direction mismatch");
                }

                return LocateCurrentAmount(scalingFactor, endScalingFactor, blocksCounted, blocksPassed, segmentEnd, scalingFactor > ConstantsV4.BaseScalingFactor);
            }

            blocksCounted = segmentEnd;
            previousDuration = duration;
        }

        if (blocksPassed >= blocksCounted)
        {
            throw new HybridOrderPriceCurveError("Price curve blocks exceeded");
        }

        throw new HybridOrderPriceCurveError("Unable to derive scaling factor");
    }

    private static BigInteger LocateCurrentAmount(
        BigInteger startAmount, BigInteger endAmount, long startBlock, long currentBlock, long endBlock, bool roundUp)
    {
        if (startAmount == endAmount)
        {
            return endAmount;
        }

        long duration = endBlock - startBlock;
        if (duration == 0)
        {
            throw new HybridOrderPriceCurveError("Invalid duration: zero duration when it shouldn't be");
        }
        long elapsed = currentBlock - startBlock;
        long remaining = duration - elapsed;

        BigInteger totalBeforeDivision = startAmount * remaining + endAmount * elapsed;
        if (totalBeforeDivision.IsZero)
        {
            return BigInteger.Zero;
        }
        if (roundUp)
        {
            return (totalBeforeDivision - 1) / duration + 1;
        }
        return totalBeforeDivision / duration;
    }

    private static List<TokenAmount> ScaleOutputs(IReadOnlyList<HybridOutput> outputs, BigInteger scalingMultiplier) =>
        outputs.Select(o => new TokenAmount(o.Token, MulWadUp(o.MinAmount, scalingMultiplier))).ToList();

    private static TokenAmount ScaleInput(HybridInput input, BigInteger scalingMultiplier) =>
        new(input.Token, MulWad(input.MaxAmount, scalingMultiplier));

    private static BigInteger MulWad(BigInteger a, BigInteger b)
    {
        if (a.IsZero || b.IsZero)
        {
            return BigInteger.Zero;
        }
        return a * b / Wad;
    }

    private static BigInteger MulWadUp(BigInteger a, BigInteger b)
    {
        if (a.IsZero || b.IsZero)
        {
            return BigInteger.Zero;
        }
        return (a * b + Wad - 1) / Wad;
    }
}
