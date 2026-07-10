using System.Numerics;
using UniswapSharp.V3.Entities;

namespace UniswapSharp.Testing.V3.Entities;

public class TickListDataProviderTests
{
    [Fact]
    public void Constructor_CanTakeEmptyListOfTicks()
    {
        new TickListDataProvider(new List<Tick>(), 1);
    }

    [Fact]
    public void Constructor_ThrowsForZeroTickSpacing()
    {
        Assert.Throws<ArgumentException>(() => new TickListDataProvider(new List<Tick>(), 0));
    }

    [Fact]
    public async Task Constructor_ThrowsForUnevenTickList()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Task.FromResult(new TickListDataProvider(
                new List<Tick>
                {
                    new Tick ( -1, -1,  1 ),
                    new Tick ( 1,  2,  1 )
                },
                1
            ))
        );
    }

    [Fact]
    public async Task GetTick_ThrowsIfTickNotInList()
    {
        var provider = new TickListDataProvider(
            new List<Tick>
            {
                new Tick ( -1, -1,  1 ),
                new Tick ( 1,1,  1 ),
            },
            1
        );
        await Assert.ThrowsAsync<ArgumentException>(() => provider.GetTick(0));
    }

    [Fact]
    public async Task GetTick_GetsSmallestTickFromList()
    {
        var provider = new TickListDataProvider(
            new List<Tick>
            {
                new Tick ( -1, -1,  1 ),
                new Tick ( 1,1,  1 ),
            },
            1
        );
        var tick = await provider.GetTick(-1);
        Assert.Equal(new BigInteger(-1), tick.LiquidityNet);
        Assert.Equal(new BigInteger(1), tick.LiquidityGross);
    }

    [Fact]
    public async Task GetTick_GetsLargestTickFromList()
    {
        var provider = new TickListDataProvider(
            new List<Tick>
            {
                new Tick ( -1, -1,  1 ),
                new Tick ( 1,1,  1 ),
            },
            1
        );
        var tick = await provider.GetTick(1);
        Assert.Equal(new BigInteger(1), tick.LiquidityNet);
        Assert.Equal(new BigInteger(1), tick.LiquidityGross);
    }
}