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

    private static Dictionary<int, Ether> _etherCache = new Dictionary<int, Ether>();

    public static Ether OnChain(int chainId)
    {
        if (!_etherCache.TryGetValue(chainId, out var ether))
        {
            ether = new Ether(chainId);
            _etherCache[chainId] = ether;
        }
        return ether;
    }

    public override bool Equals(BaseCurrency other)
    {
        return other.IsNative && other.ChainId == this.ChainId;
    }
}
