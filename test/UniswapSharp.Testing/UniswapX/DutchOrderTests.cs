using System.Numerics;
using AwesomeAssertions;
using Nethereum.Signer;
using UniswapSharp.Permit2;
using UniswapSharp.UniswapX.Order;

namespace UniswapSharp.Testing.UniswapX;

// Port of uniswapx-sdk src/order/DutchOrder.test.ts.
public class DutchOrderTests
{
    private const string Zero = "0x0000000000000000000000000000000000000000";

    private static DutchOrderInfo GetOrderInfo(Action<DutchOrderInfo>? mutate = null)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var info = new DutchOrderInfo
        {
            Deadline = now + 1000,
            Reactor = Zero,
            Swapper = Zero,
            Nonce = 10,
            AdditionalValidationContract = Zero,
            AdditionalValidationData = "0x",
            ExclusiveFiller = Zero,
            ExclusivityOverrideBps = 0,
            DecayStartTime = now,
            DecayEndTime = now + 1000,
            Input = new DutchInput
            {
                Token = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48",
                StartAmount = BigInteger.Parse("1000000"),
                EndAmount = BigInteger.Parse("1000000"),
            },
            Outputs = new List<DutchOutput>
            {
                new()
                {
                    Token = "0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2",
                    StartAmount = BigInteger.Parse("1000000000000000000"),
                    EndAmount = BigInteger.Parse("900000000000000000"),
                    Recipient = Zero,
                },
            },
        };
        mutate?.Invoke(info);
        return info;
    }

    [Fact]
    public void ParsesASerializedOrder()
    {
        var orderInfo = GetOrderInfo();
        var order = new DutchOrder(orderInfo, 1);
        string serialized = order.Serialize();
        var parsed = DutchOrder.Parse(serialized, 1);
        parsed.Info.Should().BeEquivalentTo(orderInfo);
    }

    [Fact]
    public void ValidSignatureOverInfo()
    {
        var order = new DutchOrder(GetOrderInfo(), 1);
        var key = EthECKey.GenerateKey();

        var permitData = order.PermitData();
        byte[] digest = Eip712TypedDataEncoder.Hash(permitData.Domain, permitData.Types, permitData.Values);
        string signature = SignDigest(key, digest);

        order.GetSigner(signature).Should().Be(UniswapSharp.Core.Utils.AddressValidator.GetAddress(key.GetPublicAddress()));
    }

    [Fact]
    public void ResolvesBeforeDecayStartTime()
    {
        var order = new DutchOrder(GetOrderInfo(), 1);
        var resolved = order.Resolve(new OrderResolutionOptions(order.Info.DecayStartTime - 100));
        resolved.Input.Token.Should().Be(order.Info.Input.Token);
        resolved.Input.Amount.Should().Be(order.Info.Input.StartAmount);
        resolved.Outputs.Should().HaveCount(1);
        resolved.Outputs[0].Token.Should().Be(order.Info.Outputs[0].Token);
        resolved.Outputs[0].Amount.Should().Be(order.Info.Outputs[0].StartAmount);
    }

    [Fact]
    public void ResolvesAtDecayStartTime()
    {
        var order = new DutchOrder(GetOrderInfo(), 1);
        var resolved = order.Resolve(new OrderResolutionOptions(order.Info.DecayStartTime));
        resolved.Input.Amount.Should().Be(order.Info.Input.StartAmount);
        resolved.Outputs[0].Amount.Should().Be(order.Info.Outputs[0].StartAmount);
    }

    [Fact]
    public void ResolvesAtDecayEndTime()
    {
        var order = new DutchOrder(GetOrderInfo(), 1);
        var resolved = order.Resolve(new OrderResolutionOptions(order.Info.DecayEndTime));
        resolved.Input.Amount.Should().Be(order.Info.Input.EndAmount);
        resolved.Outputs[0].Amount.Should().Be(order.Info.Outputs[0].EndAmount);
    }

    [Fact]
    public void ResolvesAfterDecayEndTime()
    {
        var order = new DutchOrder(GetOrderInfo(), 1);
        var resolved = order.Resolve(new OrderResolutionOptions(order.Info.DecayEndTime + 100));
        resolved.Input.Amount.Should().Be(order.Info.Input.EndAmount);
        resolved.Outputs[0].Amount.Should().Be(order.Info.Outputs[0].EndAmount);
    }

    [Fact]
    public void ResolvesWhenFillerHasExclusivity()
    {
        const string exclusiveFiller = "0x0000000000000000000000000000000000000001";
        var order = new DutchOrder(GetOrderInfo(i =>
        {
            i.ExclusiveFiller = exclusiveFiller;
            i.ExclusivityOverrideBps = 100;
        }), 1);
        var resolved = order.Resolve(new OrderResolutionOptions(order.Info.DecayStartTime - 1, exclusiveFiller));
        resolved.Input.Amount.Should().Be(order.Info.Input.StartAmount);
        resolved.Outputs[0].Amount.Should().Be(order.Info.Outputs[0].StartAmount);
    }

    [Fact]
    public void ResolvesWhenFillerDoesntHaveExclusivity()
    {
        const string nonExclusiveFiller = "0x0000000000000000000000000000000000000000";
        const string exclusiveFiller = "0x0000000000000000000000000000000000000001";
        BigInteger exclusivityOverrideBps = 100;
        var order = new DutchOrder(GetOrderInfo(i =>
        {
            i.ExclusiveFiller = exclusiveFiller;
            i.ExclusivityOverrideBps = exclusivityOverrideBps;
        }), 1);
        var resolved = order.Resolve(new OrderResolutionOptions(order.Info.DecayStartTime - 1, nonExclusiveFiller));
        resolved.Input.Amount.Should().Be(order.Info.Input.StartAmount);
        resolved.Outputs[0].Amount.Should().Be(
            order.Info.Outputs[0].StartAmount * (exclusivityOverrideBps + 10000) / 10000);
    }

    [Fact]
    public void ResolvesWhenFillerDoesntHaveExclusivityButDecayStartTimeIsPast()
    {
        const string nonExclusiveFiller = "0x0000000000000000000000000000000000000000";
        const string exclusiveFiller = "0x0000000000000000000000000000000000000001";
        var order = new DutchOrder(GetOrderInfo(i =>
        {
            i.ExclusiveFiller = exclusiveFiller;
            i.ExclusivityOverrideBps = 100;
        }), 1);
        var resolved = order.Resolve(new OrderResolutionOptions(order.Info.DecayEndTime, nonExclusiveFiller));
        resolved.Input.Amount.Should().Be(order.Info.Input.StartAmount);
        resolved.Outputs[0].Amount.Should().Be(order.Info.Outputs[0].EndAmount);
    }

    [Fact]
    public void ResolvesWhenFillerIsNotSetButThereIsExclusivity()
    {
        const string exclusiveFiller = "0x0000000000000000000000000000000000000001";
        BigInteger exclusivityOverrideBps = 100;
        var order = new DutchOrder(GetOrderInfo(i =>
        {
            i.ExclusiveFiller = exclusiveFiller;
            i.ExclusivityOverrideBps = exclusivityOverrideBps;
        }), 1);
        var resolved = order.Resolve(new OrderResolutionOptions(order.Info.DecayStartTime - 1));
        resolved.Input.Amount.Should().Be(order.Info.Input.StartAmount);
        resolved.Outputs[0].Amount.Should().Be(
            order.Info.Outputs[0].StartAmount * (exclusivityOverrideBps + 10000) / 10000);
    }

    internal static string SignDigest(EthECKey key, byte[] digest)
    {
        var sig = key.SignAndCalculateV(digest);
        var full = new byte[65];
        CopyRight(sig.R, full, 0);
        CopyRight(sig.S, full, 32);
        full[64] = sig.V[0];
        return "0x" + Convert.ToHexStringLower(full);
    }

    private static void CopyRight(byte[] src, byte[] dst, int wordStart)
    {
        // Left-pad the (possibly < 32-byte) big-endian value into a 32-byte word.
        Array.Copy(src, 0, dst, wordStart + (32 - src.Length), src.Length);
    }
}
