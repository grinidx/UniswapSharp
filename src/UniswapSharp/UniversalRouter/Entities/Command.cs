using UniswapSharp.UniversalRouter.Utils;

namespace UniswapSharp.UniversalRouter.Entities;

/// <summary>Configuration for encoding a command. Port of <c>entities/Command.ts</c> (<c>TradeConfig</c>).</summary>
public sealed record TradeConfig(bool AllowRevert);

/// <summary>The kind of router action. Port of <c>RouterActionType</c>.</summary>
public enum RouterActionType
{
    UniswapTrade,
    UnwrapWETH,
}

/// <summary>Interface for entities that can be encoded as a Universal Router command. Port of <c>Command</c>.</summary>
public interface ICommand
{
    RouterActionType TradeType { get; }
    void Encode(RoutePlanner planner, TradeConfig config);
}
