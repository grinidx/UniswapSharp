namespace UniswapSharp.Router.Entities;

/// <summary>
/// Port of router-sdk <c>entities/protocol.ts</c>. Identifies which protocol a route uses.
/// </summary>
public enum Protocol
{
    V2,
    V3,
    V4,
    MIXED,
}
