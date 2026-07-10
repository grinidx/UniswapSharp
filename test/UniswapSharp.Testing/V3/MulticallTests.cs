using UniswapSharp.V3;

namespace UniswapSharp.Testing.V3;

// Ported from sdks/v3-sdk/src/multicall.test.ts (#encodeMulticall)
public class MulticallTests
{
    [Fact]
    public void EncodeMulticall_String() =>
        Assert.Equal("0x01", Multicall.EncodeMulticall("0x01"));

    [Fact]
    public void EncodeMulticall_SingleElementArray() =>
        Assert.Equal("0x01", Multicall.EncodeMulticall(new[] { "0x01" }));

    [Fact]
    public void EncodeMulticall_MultiElementArray() =>
        Assert.Equal(
            "0xac9650d800000000000000000000000000000000000000000000000000000000000000200000000000000000000000000000000000000000000000000000000000000002000000000000000000000000000000000000000000000000000000000000004000000000000000000000000000000000000000000000000000000000000000800000000000000000000000000000000000000000000000000000000000000020aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa0000000000000000000000000000000000000000000000000000000000000020bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            Multicall.EncodeMulticall(new[]
            {
                "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            }));
}
