using UniswapSharp.Core.Entities;

namespace UniswapSharp.Testing.Core.Entities;

public class EtherTests
{
    [Fact]
    public void StaticConstructorUsesCache()
    {
        // eslint-disable-next-line no-self-compare
        Assert.Equal(Ether.OnChain(1), Ether.OnChain(1));
    }

    [Fact]
    public void CachesOncePerChainId()
    {
        Assert.NotEqual(Ether.OnChain(1), Ether.OnChain(2));
    }

    [Fact]
    public void EqualsReturnsFalseForDiffChains()
    {
        Assert.False(Ether.OnChain(1).Equals(Ether.OnChain(2)));
    }

    [Fact]
    public void EqualsReturnsTrueForSameChains()
    {
        Assert.True(Ether.OnChain(1).Equals(Ether.OnChain(1)));
    }
}