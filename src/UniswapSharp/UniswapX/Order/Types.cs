using System.Numerics;
using UniswapSharp.Permit2;

namespace UniswapSharp.UniswapX.Order;

/// <summary>Optional block overrides applied when quoting an order on chain (uniswapx-sdk <c>BlockOverrides</c>).</summary>
public sealed record BlockOverrides(string? Number = null);

/// <summary>General interface implemented by off-chain orders (uniswapx-sdk <c>OffChainOrder</c>).</summary>
public interface IOffChainOrder
{
    int ChainId { get; }

    /// <summary>Returns the ABI-encoded serialized order which can be submitted on-chain.</summary>
    string Serialize();

    /// <summary>Recovers the address which produced <paramref name="signature"/> over this order's permit hash.</summary>
    string GetSigner(string signature);

    /// <summary>Returns the data for generating the maker EIP-712 permit signature.</summary>
    PermitData PermitData();

    /// <summary>Returns the order hash used as a key on-chain.</summary>
    string Hash();

    /// <summary>Any block overrides to be applied when quoting the order on chain.</summary>
    BlockOverrides? BlockOverrides { get; }
}

/// <summary>A resolved token/amount pair (uniswapx-sdk <c>TokenAmount</c>).</summary>
public sealed record TokenAmount(string Token, BigInteger Amount);

/// <summary>A resolved relay fee (uniswapx-sdk <c>ResolvedRelayFee</c>).</summary>
public sealed record ResolvedRelayFee(string Token, BigInteger Amount);

/// <summary>The resolved input/outputs of a UniswapX order (uniswapx-sdk <c>ResolvedUniswapXOrder</c>).</summary>
public sealed record ResolvedUniswapXOrder(TokenAmount Input, IReadOnlyList<TokenAmount> Outputs);

/// <summary>The resolved fee of a relay order (uniswapx-sdk <c>ResolvedRelayOrder</c>).</summary>
public sealed record ResolvedRelayOrder(ResolvedRelayFee Fee);

/// <summary>Common order info (uniswapx-sdk <c>OrderInfo</c>).</summary>
public sealed record OrderInfo
{
    public required string Reactor { get; set; }
    public required string Swapper { get; set; }
    public required BigInteger Nonce { get; set; }
    public required long Deadline { get; set; }
    public required string AdditionalValidationContract { get; set; }
    public required string AdditionalValidationData { get; set; }
}

/// <summary>Options to resolve a dutch order (uniswapx-sdk <c>OrderResolutionOptions</c>).</summary>
public sealed record OrderResolutionOptions(long Timestamp, string? Filler = null);

/// <summary>Options to resolve a priority order (uniswapx-sdk <c>PriorityOrderResolutionOptions</c>).</summary>
public sealed record PriorityOrderResolutionOptions(BigInteger PriorityFee, BigInteger? CurrentBlock = null);

/// <summary>Options to resolve a V3 dutch order (uniswapx-sdk <c>V3OrderResolutionOptions</c>).</summary>
public sealed record V3OrderResolutionOptions(long CurrentBlock, string? Filler = null);

/// <summary>A dutch output (uniswapx-sdk <c>DutchOutput</c>).</summary>
public sealed record DutchOutput
{
    public required string Token { get; set; }
    public required BigInteger StartAmount { get; set; }
    public required BigInteger EndAmount { get; set; }
    public required string Recipient { get; set; }
}

/// <summary>JSON form of a dutch output (uniswapx-sdk <c>DutchOutputJSON</c>).</summary>
public sealed record DutchOutputJSON(string Token, string StartAmount, string EndAmount, string Recipient);

/// <summary>JSON form of a dutch input (uniswapx-sdk <c>DutchInputJSON</c>).</summary>
public sealed record DutchInputJSON(string Token, string StartAmount, string EndAmount);

/// <summary>JSON form of V2 cosigner data (uniswapx-sdk <c>CosignerDataJSON</c>).</summary>
public sealed record CosignerDataJSON(
    long DecayStartTime,
    long DecayEndTime,
    string ExclusiveFiller,
    long ExclusivityOverrideBps,
    string InputOverride,
    IReadOnlyList<string> OutputOverrides);

/// <summary>A dutch input (uniswapx-sdk <c>DutchInput</c>).</summary>
public sealed record DutchInput
{
    public required string Token { get; set; }
    public required BigInteger StartAmount { get; set; }
    public required BigInteger EndAmount { get; set; }
}

/// <summary>V2 dutch cosigner data (uniswapx-sdk <c>CosignerData</c>).</summary>
public sealed record CosignerData
{
    public required long DecayStartTime { get; set; }
    public required long DecayEndTime { get; set; }
    public required string ExclusiveFiller { get; set; }
    public required BigInteger ExclusivityOverrideBps { get; set; }
    public required BigInteger InputOverride { get; set; }
    public required IReadOnlyList<BigInteger> OutputOverrides { get; set; }
}

/// <summary>A priority input (uniswapx-sdk <c>PriorityInput</c>).</summary>
public sealed record PriorityInput
{
    public required string Token { get; set; }
    public required BigInteger Amount { get; set; }
    public required BigInteger MpsPerPriorityFeeWei { get; set; }
}

/// <summary>A priority output (uniswapx-sdk <c>PriorityOutput</c>).</summary>
public sealed record PriorityOutput
{
    public required string Token { get; set; }
    public required BigInteger Amount { get; set; }
    public required BigInteger MpsPerPriorityFeeWei { get; set; }
    public required string Recipient { get; set; }
}

/// <summary>A nonlinear dutch decay curve (uniswapx-sdk <c>NonlinearDutchDecay</c>). Amounts can be negative.</summary>
public sealed record NonlinearDutchDecay
{
    public required IReadOnlyList<int> RelativeBlocks { get; set; }
    public required IReadOnlyList<BigInteger> RelativeAmounts { get; set; }
}

/// <summary>A V3 dutch input (uniswapx-sdk <c>V3DutchInput</c>).</summary>
public sealed record V3DutchInput
{
    public required string Token { get; set; }
    public required BigInteger StartAmount { get; set; }
    public required NonlinearDutchDecay Curve { get; set; }
    public required BigInteger MaxAmount { get; set; }
    public required BigInteger AdjustmentPerGweiBaseFee { get; set; }
}

/// <summary>A V3 dutch output (uniswapx-sdk <c>V3DutchOutput</c>).</summary>
public sealed record V3DutchOutput
{
    public required string Token { get; set; }
    public required BigInteger StartAmount { get; set; }
    public required NonlinearDutchDecay Curve { get; set; }
    public required string Recipient { get; set; }
    public required BigInteger MinAmount { get; set; }
    public required BigInteger AdjustmentPerGweiBaseFee { get; set; }
}
