using System.Numerics;
using UniswapSharp.LiquidityLauncher;

namespace UniswapSharp.Testing.LiquidityLauncher;

// Ported from sdks/liquidity-launcher-sdk/src/encode.test.ts.
public class EncodeTests
{
    // Golden vector: call[0] of a real Unichain launch multicall (depositToken(token, 0.0001e18)).
    [Fact]
    public void EncodeDepositToken_MatchesOnChainDepositTokenCalldata()
    {
        string data = Encode.EncodeDepositToken("0x15d0e0c55a3e7ee67152ad7e89acf164253ff68d", BigInteger.Parse("100000000000000"));
        Assert.Equal(
            "0x44599bc500000000000000000000000015d0e0c55a3e7ee67152ad7e89acf164253ff68d00000000000000000000000000000000000000000000000000005af3107a4000",
            data);
    }

    [Fact]
    public void EncodeAuctionSteps_PacksEachStepAsBytes3MpsBytes5BlockDelta()
    {
        string data = Encode.EncodeAuctionSteps(new[] { new AuctionStepInput(1, 0, 2) });
        // mps=1 -> 0x000001, blockDelta=2 -> 0x0000000002
        Assert.Equal("0x0000010000000002", data);
    }

    [Fact]
    public void EncodeAuctionSteps_ReturnsHexPrefixForAnEmptySchedule() =>
        Assert.Equal("0x", Encode.EncodeAuctionSteps(Array.Empty<AuctionStepInput>()));

    [Fact]
    public void EncodeAuctionSteps_RejectsANonIncreasingStep() =>
        Assert.Throws<LauncherSdkError>(() => Encode.EncodeAuctionSteps(new[] { new AuctionStepInput(1, 5, 5) }));

    // ---- launcher selector pins (ported from abis.test.ts) ----
    //
    // Selectors read off the deployed chain-4663 launcher's dispatcher (2026-08-04). Upstream pins
    // them against its LIQUIDITY_LAUNCHER_ABI; this port has no ABI surface, so the equivalent guard
    // is to pin the selector each encoder actually emits. That is the same regression these catch:
    // a signature typo produces calldata the dispatcher rejects with empty data.
    [Theory]
    [InlineData("createToken(address,string,string,uint8,uint128,address,bytes)", "dec14be1")]
    [InlineData("distributeToken(address,(address,uint128,bytes),bytes32)", "b6982b48")]
    [InlineData("depositToken(address,uint160)", "44599bc5")]
    [InlineData("multicall(bytes[])", "ac9650d8")]
    public void LauncherSignatures_PinTheDeployedDispatcherSelectors(string signature, string selector) =>
        Assert.Equal(selector, UniswapSharp.V3.Utils.AbiFunctionEncoder.Selector(signature));

    [Fact]
    public void EncodedLauncherCalldata_StartsWithThePinnedSelectors()
    {
        Assert.StartsWith("0x44599bc5",
            Encode.EncodeDepositToken("0x0000000000000000000000000000000000000001", 1));

        Assert.StartsWith("0xac9650d8", Encode.EncodeMulticall(new[] { "0x1234" }));
    }
}
