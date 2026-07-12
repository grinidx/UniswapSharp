using UniswapSharp.UniversalRouter.Utils;
using UniswapSharp.V4.Utils;

namespace UniswapSharp.Testing.UniversalRouter.Utils;

// Ported from sdks/universal-router-sdk/test/unit/toV4URVersion.test.ts
public class ToV4URVersionTests
{
    [Fact]
    public void MapsEachUrVersionToMatchingV4SdkUrVersion()
    {
        Assert.Equal(URVersion.V2_0, V4URVersion.ToV4URVersion(UniversalRouterVersion.V2_0));
        Assert.Equal(URVersion.V2_1_1, V4URVersion.ToV4URVersion(UniversalRouterVersion.V2_1_1));
        Assert.Equal(URVersion.V2_2_0, V4URVersion.ToV4URVersion(UniversalRouterVersion.V2_2_0));
    }

    [Fact]
    public void DefaultsToV2_0WhenVersionIsNull()
    {
        Assert.Equal(URVersion.V2_0, V4URVersion.ToV4URVersion(null));
    }

    [Fact]
    public void ThrowsForUrVersionsWithNoV4SdkEquivalent()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => V4URVersion.ToV4URVersion(UniversalRouterVersion.V1_2));
        Assert.Contains("No v4-sdk URVersion mapping", ex.Message);
    }

    [Fact]
    public void ThrowsForUnknownVersionValue()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => V4URVersion.ToV4URVersion((UniversalRouterVersion)999));
        Assert.Contains("No v4-sdk URVersion mapping", ex.Message);
    }

    [Fact]
    public void ResolvesEveryUrVersionThatSharesAStringWithUrVersion()
    {
        var urVersionValues = new HashSet<string> { "2.0", "2.1.1", "2.2.0" };
        foreach (var version in Enum.GetValues<UniversalRouterVersion>())
        {
            if (urVersionValues.Contains(version.Value()))
            {
                // resolves to matching URVersion (shared string value)
                Assert.Equal(version.Value(), V4URVersion.ToV4URVersion(version) switch
                {
                    URVersion.V2_0 => "2.0",
                    URVersion.V2_1_1 => "2.1.1",
                    URVersion.V2_2_0 => "2.2.0",
                    _ => "?",
                });
            }
            else
            {
                Assert.Throws<InvalidOperationException>(() => V4URVersion.ToV4URVersion(version));
            }
        }
    }
}
