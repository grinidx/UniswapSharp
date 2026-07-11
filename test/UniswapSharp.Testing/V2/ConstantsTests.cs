using UniswapSharp.V2;

namespace UniswapSharp.Testing.V2;

public class ConstantsTests
{
    // Upstream constants.test.ts recomputes the init code hash from the compiled UniswapV2Pair bytecode
    // (imported from @uniswap/v2-core/build/UniswapV2Pair.json). That build artifact is not vendored into
    // this repo, so the "matches computed bytecode hash" case is omitted; we pin the published constant value.
    [Fact]
    public void InitCodeHashMatchesPublishedValue()
    {
        Assert.Equal("0x96e8ac4277198ff8b6f785478aa9a39f403cb768dd02cbee326c3e7da348845f", Constants.INIT_CODE_HASH);
    }
}
