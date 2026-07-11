using System.Numerics;
using AwesomeAssertions;
using UniswapSharp.Permit2;

namespace UniswapSharp.Testing.Permit2;

// Port of permit2-sdk src/allowanceTransfer.test.ts.
public class AllowanceTransferTests
{
    private const string Zero = "0x0000000000000000000000000000000000000000";

    // describe('Max values')

    [Fact]
    public void MaxValuesPass()
    {
        Action act = () => AllowanceTransfer.Hash(
            new PermitSingle(
                new PermitDetails(
                    Zero,
                    Constants.MaxAllowanceTransferAmount,
                    Constants.MaxAllowanceExpiration,
                    Constants.MaxOrderedNonce),
                Zero,
                Constants.MaxSigDeadline),
            Zero,
            1);
        act.Should().NotThrow();
    }

    [Fact]
    public void NonceOutOfRange()
    {
        Action act = () => AllowanceTransfer.Hash(
            new PermitSingle(
                new PermitDetails(Zero, BigInteger.Zero, BigInteger.Zero, Constants.MaxOrderedNonce + 1),
                Zero,
                BigInteger.Zero),
            Zero,
            1);
        act.Should().Throw<ArgumentException>().WithMessage("NONCE_OUT_OF_RANGE");
    }

    [Fact]
    public void AmountOutOfRange()
    {
        Action act = () => AllowanceTransfer.Hash(
            new PermitSingle(
                new PermitDetails(Zero, Constants.MaxAllowanceTransferAmount + 1, BigInteger.Zero, 0),
                Zero,
                BigInteger.Zero),
            Zero,
            1);
        act.Should().Throw<ArgumentException>().WithMessage("AMOUNT_OUT_OF_RANGE");
    }

    [Fact]
    public void ExpirationOutOfRange()
    {
        Action act = () => AllowanceTransfer.Hash(
            new PermitSingle(
                new PermitDetails(Zero, BigInteger.Zero, Constants.MaxAllowanceExpiration + 1, 0),
                Zero,
                BigInteger.Zero),
            Zero,
            1);
        act.Should().Throw<ArgumentException>().WithMessage("EXPIRATION_OUT_OF_RANGE");
    }

    [Fact]
    public void SigDeadlineOutOfRange()
    {
        Action act = () => AllowanceTransfer.Hash(
            new PermitSingle(
                new PermitDetails(Zero, BigInteger.Zero, BigInteger.Zero, 0),
                Zero,
                Constants.MaxSigDeadline + 1),
            Zero,
            1);
        act.Should().Throw<ArgumentException>().WithMessage("SIG_DEADLINE_OUT_OF_RANGE");
    }

    [Fact]
    public void NonBatch()
    {
        string hash = AllowanceTransfer.Hash(
            new PermitSingle(
                new PermitDetails(Zero, BigInteger.Zero, BigInteger.Zero, 0),
                Zero,
                BigInteger.Zero),
            Zero,
            1);
        hash.Should().Be("0xd47437bffdbc4d123a2165feb6ca646b8700c038622ce304f84e9048bc744f36");
    }

    [Fact]
    public void Batch()
    {
        string hash = AllowanceTransfer.Hash(
            new PermitBatch(
                new[] { new PermitDetails(Zero, BigInteger.Zero, BigInteger.Zero, 0) },
                Zero,
                BigInteger.Zero),
            Zero,
            1);
        hash.Should().Be("0x49642ada5f77eb9458f8265eb01fed2684c2f25d50534fea3efdf2cf395deb2f");
    }
}
