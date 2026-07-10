using System.Collections.Concurrent;
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
    public async Task OnChain_IsThreadSafe_ReturnsStableSingletonPerChain()
    {
        // The cache is a shared static and xUnit runs test classes in parallel, so
        // OnChain is called concurrently. With a non-thread-safe Dictionary, many
        // threads hitting the same fresh chain's cache-miss window each create and
        // return their own Ether instance (and concurrent writes can corrupt the
        // Dictionary outright). Every caller must instead get the same singleton.
        int threads = Math.Max(8, Environment.ProcessorCount * 4);

        for (int round = 0; round < 150; round++)
        {
            int chainId = 900_000 + round; // fresh id each round => guaranteed cache miss
            var returned = new ConcurrentBag<Ether>();
            using var barrier = new Barrier(threads);
            var tasks = new Task[threads];

            var ct = TestContext.Current.CancellationToken;
            for (int t = 0; t < threads; t++)
            {
                tasks[t] = Task.Run(() =>
                {
                    barrier.SignalAndWait(ct); // release all threads into the miss window at once
                    Ether ether = Ether.OnChain(chainId);
                    Assert.Equal(chainId, ether.ChainId);
                    returned.Add(ether);
                }, ct);
            }

            await Task.WhenAll(tasks);
            Assert.Single(returned.Distinct());
        }
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
