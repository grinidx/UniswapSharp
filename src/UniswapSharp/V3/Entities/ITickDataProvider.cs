using System.Numerics;

namespace UniswapSharp.V3.Entities;

/// <summary>
/// Provides information about ticks
/// </summary>
public interface ITickDataProvider
{
    /// <summary>
    /// Return information corresponding to a specific tick
    /// </summary>
    /// <param name="tick">The tick to load</param>
    Task<Tick> GetTick(int tick);

    /// <summary>
    /// Return the next tick that is initialized within a single word
    /// </summary>
    /// <param name="tick">The current tick</param>
    /// <param name="lte">Whether the next tick should be lte the current tick</param>
    /// <param name="tickSpacing">The tick spacing of the pool</param>
    Task<(int nextTick, bool initialized)> NextInitializedTickWithinOneWord(int tick, bool lte, int tickSpacing);
}
