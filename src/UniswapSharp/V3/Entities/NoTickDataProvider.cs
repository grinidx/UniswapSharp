using System.Numerics;

namespace UniswapSharp.V3.Entities;

/// <summary>
/// This tick data provider does not know how to fetch any tick data. It throws whenever it is required. Useful if you
/// do not need to load tick data for your use case.
/// </summary>
public class NoTickDataProvider : ITickDataProvider
{
    private const string ERROR_MESSAGE = "No tick data provider was given";

    public Task<Tick> GetTick(int tick)
    {
        throw new InvalidOperationException(ERROR_MESSAGE);
    }

    public Task<(int nextTick, bool initialized)> NextInitializedTickWithinOneWord(int tick, bool lte, int tickSpacing)
    {
        throw new InvalidOperationException(ERROR_MESSAGE);
    }
}
