using System.Numerics;

namespace UniswapSharp.UniswapX.Order.V4;

/// <summary>V4 OrderInfo with hooks support (uniswapx-sdk <c>OrderInfoV4</c>).</summary>
public sealed record HybridInput
{
    public required string Token { get; set; }
    public required BigInteger MaxAmount { get; set; }
}

/// <summary>Hybrid auction output token (uniswapx-sdk <c>HybridOutput</c>).</summary>
public sealed record HybridOutput
{
    public required string Token { get; set; }
    public required BigInteger MinAmount { get; set; }
    public required string Recipient { get; set; }
}

/// <summary>Hybrid cosigner data (uniswapx-sdk <c>HybridCosignerData</c>).</summary>
public sealed record HybridCosignerData
{
    public required BigInteger AuctionTargetBlock { get; set; }
    public required IReadOnlyList<BigInteger> SupplementalPriceCurve { get; set; }
    public required string ExclusiveFiller { get; set; }
    public required BigInteger ExclusivityOverrideBps { get; set; }
    public required BigInteger ExclusivityEndBlock { get; set; }
}

/// <summary>Unsigned hybrid order info (uniswapx-sdk <c>UnsignedHybridOrderInfo</c>).</summary>
public record UnsignedHybridOrderInfo
{
    public required string Reactor { get; set; }
    public required string Swapper { get; set; }
    public required BigInteger Nonce { get; set; }
    public required long Deadline { get; set; }
    public required string PreExecutionHook { get; set; }
    public required string PreExecutionHookData { get; set; }
    public required string PostExecutionHook { get; set; }
    public required string PostExecutionHookData { get; set; }
    public required string AuctionResolver { get; set; }
    public required string Cosigner { get; set; }
    public required HybridInput Input { get; set; }
    public required List<HybridOutput> Outputs { get; set; }
    public required BigInteger AuctionStartBlock { get; set; }
    public required BigInteger BaselinePriorityFee { get; set; }
    public required BigInteger ScalingFactor { get; set; }
    public required IReadOnlyList<BigInteger> PriceCurve { get; set; }
}

/// <summary>Cosigned hybrid order info (uniswapx-sdk <c>CosignedHybridOrderInfo</c>).</summary>
public sealed record CosignedHybridOrderInfo : UnsignedHybridOrderInfo
{
    public required HybridCosignerData CosignerData { get; set; }
    public required string Cosignature { get; set; }
}

/// <summary>Options for resolving a hybrid order (uniswapx-sdk <c>HybridOrderResolutionOptions</c>).</summary>
public sealed record HybridOrderResolutionOptions(BigInteger CurrentBlock, BigInteger PriorityFeeWei, string? Filler = null);

// ---- JSON forms ----

public sealed record HybridInputJSON(string Token, string MaxAmount);
public sealed record HybridOutputJSON(string Token, string MinAmount, string Recipient);
public sealed record HybridCosignerDataJSON(
    string AuctionTargetBlock,
    IReadOnlyList<string> SupplementalPriceCurve,
    string ExclusiveFiller,
    long ExclusivityOverrideBps,
    string ExclusivityEndBlock);

public sealed record CosignedHybridOrderInfoJSON(
    string Reactor,
    string Swapper,
    string Nonce,
    long Deadline,
    string PreExecutionHook,
    string PreExecutionHookData,
    string PostExecutionHook,
    string PostExecutionHookData,
    string AuctionResolver,
    string Cosigner,
    HybridInputJSON Input,
    IReadOnlyList<HybridOutputJSON> Outputs,
    string AuctionStartBlock,
    string BaselinePriorityFee,
    string ScalingFactor,
    IReadOnlyList<string> PriceCurve,
    HybridCosignerDataJSON CosignerData,
    string Cosignature);
