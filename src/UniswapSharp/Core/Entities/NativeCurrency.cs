namespace UniswapSharp.Core.Entities;

/// <summary>
/// Represents the native currency of the chain on which it resides, e.g.
/// </summary>
public abstract class NativeCurrency(int chainId, int decimals, string? symbol = null, string? name = null)
    : BaseCurrency(chainId, decimals, symbol, name)
{
    public override bool IsNative { get; } = true;
    public override bool IsToken { get; } = false;
}
