using System.Numerics;
using AwesomeAssertions;
using UniswapSharp.Permit2;

namespace UniswapSharp.Testing.Permit2;

// Port of permit2-sdk src/signatureTransfer.test.ts.
public class SignatureTransferTests
{
    private const string Zero = "0x0000000000000000000000000000000000000000";
    private const string Bytes32Zero = "0x0000000000000000000000000000000000000000000000000000000000000000";

    private static Witness MockWitness() => new(
        WitnessValue: new Dictionary<string, object?> { ["mock"] = Bytes32Zero },
        WitnessTypeName: "MockWitness",
        WitnessType: new Dictionary<string, IReadOnlyList<TypedDataField>>
        {
            ["MockWitness"] = new[] { new TypedDataField("mock", "uint256") },
        });

    // describe('Max values')

    [Fact]
    public void MaxValues()
    {
        Action act = () => SignatureTransfer.Hash(
            new PermitTransferFrom(
                new TokenPermissions(Zero, Constants.MaxSignatureTransferAmount),
                Zero,
                Constants.MaxUnorderedNonce,
                Constants.MaxSigDeadline),
            Zero,
            1);
        act.Should().NotThrow();
    }

    [Fact]
    public void NonceOutOfRange()
    {
        Action act = () => SignatureTransfer.Hash(
            new PermitTransferFrom(
                new TokenPermissions(Zero, BigInteger.Zero),
                Zero,
                Constants.MaxUnorderedNonce + 1,
                BigInteger.Zero),
            Zero,
            1);
        act.Should().Throw<ArgumentException>().WithMessage("NONCE_OUT_OF_RANGE");
    }

    [Fact]
    public void AmountOutOfRange()
    {
        Action act = () => SignatureTransfer.Hash(
            new PermitTransferFrom(
                new TokenPermissions(Zero, Constants.MaxSignatureTransferAmount + 1),
                Zero,
                BigInteger.Zero,
                BigInteger.Zero),
            Zero,
            1);
        act.Should().Throw<ArgumentException>().WithMessage("AMOUNT_OUT_OF_RANGE");
    }

    [Fact]
    public void DeadlineOutOfRange()
    {
        Action act = () => SignatureTransfer.Hash(
            new PermitTransferFrom(
                new TokenPermissions(Zero, BigInteger.Zero),
                Zero,
                BigInteger.Zero,
                Constants.MaxSigDeadline + 1),
            Zero,
            1);
        act.Should().Throw<ArgumentException>().WithMessage("SIG_DEADLINE_OUT_OF_RANGE");
    }

    [Fact]
    public void NonBatchNoWitness()
    {
        string hash = SignatureTransfer.Hash(
            new PermitTransferFrom(
                new TokenPermissions(Zero, BigInteger.Zero),
                Zero,
                BigInteger.Zero,
                BigInteger.Zero),
            Zero,
            1);
        hash.Should().Be("0xb9bf9813799d7f0de28d2142b0bc80ec289d4a6a5b9f41834149df4188804dc5");
    }

    [Fact]
    public void NonBatchWitness()
    {
        string hash = SignatureTransfer.Hash(
            new PermitTransferFrom(
                new TokenPermissions(Zero, BigInteger.Zero),
                Zero,
                BigInteger.Zero,
                BigInteger.Zero),
            Zero,
            1,
            MockWitness());
        hash.Should().Be("0x4236a4a7b3e8e65dbb4cc758ef10dc4887e2959853fb615140d0f5e0ae7be7c9");
    }

    [Fact]
    public void BatchNoWitness()
    {
        string hash = SignatureTransfer.Hash(
            new PermitBatchTransferFrom(
                new[] { new TokenPermissions(Zero, BigInteger.Zero) },
                Zero,
                BigInteger.Zero,
                BigInteger.Zero),
            Zero,
            1);
        hash.Should().Be("0x5ba40c5ba725fec181e4a862c717adf91682b012ad01ea99a978189106d66923");
    }

    [Fact]
    public void BatchWitness()
    {
        string hash = SignatureTransfer.Hash(
            new PermitBatchTransferFrom(
                new[] { new TokenPermissions(Zero, BigInteger.Zero) },
                Zero,
                BigInteger.Zero,
                BigInteger.Zero),
            Zero,
            1,
            MockWitness());
        hash.Should().Be("0xb45d605b0a4d4f16930a4f48294d94c78f34411278fd3133626cc190273e3ccf");
    }

    // Not in the upstream .test.ts: pins the getPermitData shape (domain + primary type + values).
    [Fact]
    public void GetPermitDataReturnsDomainTypesAndValues()
    {
        var data = SignatureTransfer.GetPermitData(
            new PermitTransferFrom(
                new TokenPermissions(Zero, BigInteger.Zero),
                Zero,
                BigInteger.Zero,
                BigInteger.Zero),
            "0x000000000022D473030F116dDEE9F6B43aC78BA3",
            1);

        data.Domain.Name.Should().Be("Permit2");
        data.Domain.ChainId.Should().Be(new BigInteger(1));
        data.Domain.VerifyingContract.Should().Be("0x000000000022D473030F116dDEE9F6B43aC78BA3");
        data.Types.Should().ContainKey("PermitTransferFrom");
        data.Types.Should().ContainKey("TokenPermissions");
        data.Values.Should().ContainKey("permitted");
        data.Values.Should().ContainKey("spender");
        data.Values.Should().ContainKey("nonce");
        data.Values.Should().ContainKey("deadline");
    }
}
