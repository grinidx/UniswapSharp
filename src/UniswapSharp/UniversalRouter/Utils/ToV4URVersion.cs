using UniswapSharp.V4.Utils;

namespace UniswapSharp.UniversalRouter.Utils;

/// <summary>Port of universal-router-sdk <c>utils/toV4URVersion.ts</c>.</summary>
public static class V4URVersion
{
    // the one sanctioned bridge to v4-sdk's URVersion — UniversalRouterVersion and URVersion share
    // string values, so resolve by value: new versions map without code changes, as long as both stay in sync.
    /// <summary>
    /// Maps a <see cref="UniversalRouterVersion"/> to the matching v4-sdk <see cref="URVersion"/> by shared
    /// string value; defaults to <see cref="URVersion.V2_0"/> for <c>null</c>, throws for versions with no v4 equivalent.
    /// </summary>
    public static URVersion ToV4URVersion(UniversalRouterVersion? version)
    {
        if (version is null)
        {
            return URVersion.V2_0;
        }

        return version.Value.Value() switch
        {
            "2.0" => URVersion.V2_0,
            "2.1.1" => URVersion.V2_1_1,
            "2.2.0" => URVersion.V2_2_0,
            var v => throw new InvalidOperationException($"No v4-sdk URVersion mapping for UniversalRouterVersion: {v}"),
        };
    }
}
