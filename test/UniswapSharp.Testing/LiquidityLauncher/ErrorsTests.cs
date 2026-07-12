using UniswapSharp.LiquidityLauncher;

namespace UniswapSharp.Testing.LiquidityLauncher;

// Ported from sdks/liquidity-launcher-sdk/src/errors.test.ts.
public class ErrorsTests
{
    [Fact]
    public void LauncherSdkError_CarriesStableCodeAndUserFacingMessage()
    {
        var err = new LauncherSdkError(LauncherErrorCode.INVALID_FLOOR_PRICE, "Floor price must be greater than zero");
        Assert.Equal(LauncherErrorCode.INVALID_FLOOR_PRICE, err.Code);
        Assert.Equal("Floor price must be greater than zero", err.Message);
        Assert.IsAssignableFrom<Exception>(err);
    }

    [Fact]
    public void IsLauncherSdkError_RecognizesALauncherSdkError() =>
        Assert.True(LauncherErrors.IsLauncherSdkError(new LauncherSdkError(LauncherErrorCode.INVALID_FEE, "nope")));

    // Upstream's third case (recognizing a structurally-equivalent error across a dual cjs/esm boundary)
    // has no analogue in a single-assembly C# port, so it is intentionally omitted.

    [Fact]
    public void IsLauncherSdkError_RejectsOrdinaryErrorsAndNonErrors()
    {
        Assert.False(LauncherErrors.IsLauncherSdkError(new Exception("plain")));
        Assert.False(LauncherErrors.IsLauncherSdkError(null));
        Assert.False(LauncherErrors.IsLauncherSdkError(new { code = "INVALID_FEE" }));
    }
}
