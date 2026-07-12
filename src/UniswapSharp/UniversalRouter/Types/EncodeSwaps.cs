using UniswapSharp.Core;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.UniversalRouter.Entities.Actions;
using UniswapSharp.UniversalRouter.Utils;
using UniswapSharp.V4.Entities;
using UniswapSharp.V4.Utils;

namespace UniswapSharp.UniversalRouter.Types;

// Port of universal-router-sdk src/types/encodeSwaps.ts. BigNumberish fields are modelled as object
// (BigInteger / int / decimal string).

/// <summary>Fee union: percentage-of-output (portion) or fixed (flat). Port of <c>Fee</c>.</summary>
public abstract record Fee;

/// <summary>% of variable output, used with exact-input.</summary>
public sealed record PortionFee(string Recipient, Percent Fee) : Fee;

/// <summary>Fixed amount, deducted from exact-output target.</summary>
public sealed record FlatFee(string Recipient, object Amount) : Fee;

/// <summary>The routing side of a <see cref="SwapSpecification"/>.</summary>
public sealed record SwapRouting(
    BaseCurrency InputToken,
    BaseCurrency OutputToken,
    CurrencyAmount<BaseCurrency> Amount,
    CurrencyAmount<BaseCurrency> Quote);

/// <summary>Port of <c>SwapSpecification</c>.</summary>
public class SwapSpecification
{
    public required TradeType TradeType { get; init; }
    public required SwapRouting Routing { get; init; }
    public required Percent SlippageTolerance { get; init; }
    public string? Recipient { get; init; }
    public Fee? Fee { get; init; }
    public TokenTransferMode? TokenTransferMode { get; init; }
    public Permit2Permit? Permit { get; init; }
    public int? ChainId { get; init; }
    public object? Deadline { get; init; }
    public UniversalRouterVersion? UrVersion { get; init; }
    public bool? SafeMode { get; init; }
    public bool? NativeErc20Input { get; init; }
}

/// <summary>Port of <c>NormalizedSwapSpecification</c> (the four optional fields are resolved to non-null).</summary>
public sealed class NormalizedSwapSpecification
{
    public required TradeType TradeType { get; init; }
    public required SwapRouting Routing { get; init; }
    public required Percent SlippageTolerance { get; init; }
    public required string Recipient { get; init; }
    public required TokenTransferMode TokenTransferMode { get; init; }
    public required UniversalRouterVersion UrVersion { get; init; }
    public required bool SafeMode { get; init; }
    public Fee? Fee { get; init; }
    public Permit2Permit? Permit { get; init; }
    public int? ChainId { get; init; }
    public object? Deadline { get; init; }
    public bool? NativeErc20Input { get; init; }
}

// ---- SwapStep union ----

/// <summary>A router-owned swap step. Port of the <c>SwapStep</c> union.</summary>
public abstract record SwapStep;

public sealed record V2SwapExactIn(
    string Recipient, object AmountIn, object AmountOutMin, IReadOnlyList<string> Path,
    IReadOnlyList<object>? MinHopPriceX36 = null) : SwapStep;

public sealed record V2SwapExactOut(
    string Recipient, object AmountOut, object AmountInMax, IReadOnlyList<string> Path,
    IReadOnlyList<object>? MinHopPriceX36 = null) : SwapStep;

public sealed record V3SwapExactIn(
    string Recipient, object AmountIn, object AmountOutMin, string Path,
    IReadOnlyList<object>? MinHopPriceX36 = null) : SwapStep;

public sealed record V3SwapExactOut(
    string Recipient, object AmountOut, object AmountInMax, string Path,
    IReadOnlyList<object>? MinHopPriceX36 = null) : SwapStep;

public sealed record V4Swap(IReadOnlyList<V4Action> V4Actions) : SwapStep;

public sealed record WrapEth(string Recipient, object Amount) : SwapStep;

public sealed record UnwrapWeth(string Recipient, object AmountMin) : SwapStep;

// ---- V4Action union (the v4-periphery actions UR's V4Router._handleAction dispatches) ----

/// <summary>A V4 periphery action. Port of the <c>V4Action</c> union.</summary>
public abstract record V4Action;

public sealed record V4SwapExactIn(
    string CurrencyIn, IReadOnlyList<PathKey> Path, object AmountIn, object AmountOutMinimum,
    IReadOnlyList<object>? MinHopPriceX36 = null) : V4Action;

public sealed record V4SwapExactInSingle(
    PoolKey PoolKey, bool ZeroForOne, object AmountIn, object AmountOutMinimum, string HookData,
    object? MinHopPriceX36 = null) : V4Action;

public sealed record V4SwapExactOut(
    string CurrencyOut, IReadOnlyList<PathKey> Path, object AmountOut, object AmountInMaximum,
    IReadOnlyList<object>? MinHopPriceX36 = null) : V4Action;

public sealed record V4SwapExactOutSingle(
    PoolKey PoolKey, bool ZeroForOne, object AmountOut, object AmountInMaximum, string HookData,
    object? MinHopPriceX36 = null) : V4Action;

public sealed record V4Settle(string Currency, object Amount) : V4Action;

public sealed record V4SettleAll(string Currency, object MaxAmount) : V4Action;

public sealed record V4Take(string Currency, string Recipient, object Amount) : V4Action;

public sealed record V4TakeAll(string Currency, object MinAmount) : V4Action;

public sealed record V4TakePortion(string Currency, string Recipient, object Bips) : V4Action;
