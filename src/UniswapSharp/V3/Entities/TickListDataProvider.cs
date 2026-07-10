using System.Numerics;
using UniswapSharp.V3.Utils;

namespace UniswapSharp.V3.Entities;

public class TickListDataProvider : ITickDataProvider
{
    private readonly IReadOnlyList<Tick> ticks;

    public TickListDataProvider(IEnumerable<Tick> ticks, int tickSpacing)
    {
        var ticksList = new List<Tick>();
        foreach (var t in ticks)
        {
            ticksList.Add(t);
        }
        TickList.ValidateList(ticksList, tickSpacing);
        this.ticks = ticksList;
    }

    public async Task<Tick> GetTick(int tick)
    {
        return await Task.FromResult(TickList.GetTick(ticks, tick));
    }

    public async Task<(int nextTick, bool initialized)> NextInitializedTickWithinOneWord(int tick, bool lte, int tickSpacing)
    {
        return await Task.FromResult(TickList.NextInitializedTickWithinOneWord(ticks, tick, lte, tickSpacing));
    }
}
