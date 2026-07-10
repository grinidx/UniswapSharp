using System.Collections.Concurrent;

namespace UniswapSharp.Core.Entities;

public class Ether : NativeCurrency
{
    protected Ether(int chainId) : base(chainId, 18, "ETH", "Ether") { }

    public override Token Wrapped()
    {

        var weth9 = Weth9.Tokens[this.ChainId];
        if (weth9 == null) throw new InvalidOperationException("WRAPPED");
        return weth9;

    }

    // ConcurrentDictionary: xUnit runs test classes in parallel and callers may
    // resolve Ether concurrently; a plain Dictionary races on write (torn state /
    // duplicate instances) and breaks the reference-stable singleton contract.
    private static readonly ConcurrentDictionary<int, Ether> _etherCache = new();

    public static Ether OnChain(int chainId)
    {
        return _etherCache.GetOrAdd(chainId, static id => new Ether(id));
    }

    public override bool Equals(BaseCurrency other)
    {
        return other.IsNative && other.ChainId == this.ChainId;
    }
}
