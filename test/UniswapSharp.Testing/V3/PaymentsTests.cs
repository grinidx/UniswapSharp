using System.Numerics;
using UniswapSharp.Core.Entities;
using UniswapSharp.Core.Entities.Fractions;
using UniswapSharp.V3;

namespace UniswapSharp.Testing.V3;

// Ported from sdks/v3-sdk/src/payments.test.ts
public class PaymentsTests
{
    private const string recipient = "0x0000000000000000000000000000000000000003";
    private static readonly BigInteger amount = 123;

    private static Payments.FeeOptions FeeOptions() => new()
    {
        Fee = new Percent(1, 1000),
        Recipient = "0x0000000000000000000000000000000000000009",
    };

    private static readonly Token token = new(1, "0x0000000000000000000000000000000000000001", 18, "t0", "token0");

    [Fact]
    public void EncodeUnwrapWETH9_WithoutFeeOptions() =>
        Assert.Equal(
            "0x49404b7c000000000000000000000000000000000000000000000000000000000000007b0000000000000000000000000000000000000000000000000000000000000003",
            Payments.EncodeUnwrapWETH9(amount, recipient));

    [Fact]
    public void EncodeUnwrapWETH9_WithFeeOptions() =>
        Assert.Equal(
            "0x9b2c0a37000000000000000000000000000000000000000000000000000000000000007b0000000000000000000000000000000000000000000000000000000000000003000000000000000000000000000000000000000000000000000000000000000a0000000000000000000000000000000000000000000000000000000000000009",
            Payments.EncodeUnwrapWETH9(amount, recipient, FeeOptions()));

    [Fact]
    public void EncodeSweepToken_WithoutFeeOptions() =>
        Assert.Equal(
            "0xdf2ab5bb0000000000000000000000000000000000000000000000000000000000000001000000000000000000000000000000000000000000000000000000000000007b0000000000000000000000000000000000000000000000000000000000000003",
            Payments.EncodeSweepToken(token, amount, recipient));

    [Fact]
    public void EncodeSweepToken_WithFeeOptions() =>
        Assert.Equal(
            "0xe0e189a00000000000000000000000000000000000000000000000000000000000000001000000000000000000000000000000000000000000000000000000000000007b0000000000000000000000000000000000000000000000000000000000000003000000000000000000000000000000000000000000000000000000000000000a0000000000000000000000000000000000000000000000000000000000000009",
            Payments.EncodeSweepToken(token, amount, recipient, FeeOptions()));

    [Fact]
    public void EncodeRefundETH() =>
        Assert.Equal("0x12210e8a", Payments.EncodeRefundETH());
}
