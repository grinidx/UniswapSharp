using FluentAssertions;
using UniswapSharp.Core.Utils;

namespace UniswapSharp.Testing.Core.Utils;

public class AddressValidationTests
{
    [Fact]
    public void ValidateAndParseAddress_ReturnsSameAddressIfAlreadyChecksummed()
    {
        AddressValidator.ValidateAndParseAddress("0x5C69bEe701ef814a2B6a3EDD4B1652CB9cc5aA6f")
            .Should().Be("0x5C69bEe701ef814a2B6a3EDD4B1652CB9cc5aA6f");
    }

    [Fact]
    public void ValidateAndParseAddress_ReturnsChecksummedAddressIfNotChecksummed()
    {
        AddressValidator.ValidateAndParseAddress("0x5C69bEe701ef814a2B6a3EDD4B1652CB9cc5aA6f".ToLower())
            .Should().Be("0x5C69bEe701ef814a2B6a3EDD4B1652CB9cc5aA6f");
    }

    [Fact]
    public void ValidateAndParseAddress_ThrowsIfNotValid()
    {
        Action act = () => AddressValidator.ValidateAndParseAddress("0x5C69bEe701ef814a2B6a3EDD4B1652CB9cc5aA6");
        act.Should().Throw<ArgumentException>()
            .WithMessage("0x5C69bEe701ef814a2B6a3EDD4B1652CB9cc5aA6 is not a valid address.");
    }

    [Fact]
    public void CheckValidAddress_ReturnsSameAddressIfValid()
    {
        AddressValidator.CheckValidAddress("0x5C69bEe701ef814a2B6a3EDD4B1652CB9cc5aA6f")
            .Should().Be("0x5C69bEe701ef814a2B6a3EDD4B1652CB9cc5aA6f");
    }

    [Fact]
    public void CheckValidAddress_ThrowsIfLengthLessThan42()
    {
        Action act = () => AddressValidator.CheckValidAddress("0x5C69bEe701ef814a2B6a3EDD4B1652CB9cc5aA6");
        act.Should().Throw<ArgumentException>()
            .WithMessage("0x5C69bEe701ef814a2B6a3EDD4B1652CB9cc5aA6 is not a valid address.");
    }

    [Fact]
    public void CheckValidAddress_ThrowsIfLengthGreaterThan42()
    {
        Action act = () => AddressValidator.CheckValidAddress("0x5C69bEe701ef814a2B6a3EDD4B1652CB9cc5aA6fA");
        act.Should().Throw<ArgumentException>()
            .WithMessage("0x5C69bEe701ef814a2B6a3EDD4B1652CB9cc5aA6fA is not a valid address.");
    }

    [Fact]
    public void CheckValidAddress_ThrowsIfItDoesNotStartWith0x()
    {
        Action act = () => AddressValidator.CheckValidAddress("5C69bEe701ef814a2B6a3EDD4B1652CB9cc5aA6f");
        act.Should().Throw<ArgumentException>()
            .WithMessage("5C69bEe701ef814a2B6a3EDD4B1652CB9cc5aA6f is not a valid address.");
    }

    [Fact]
    public void CheckValidAddress_ThrowsIfItIsNotAHexString()
    {
        Action act = () => AddressValidator.CheckValidAddress("0x5C69bEe701ef814a2X6a3EDD4B1652CB9cc5aA6f");
        act.Should().Throw<ArgumentException>()
            .WithMessage("0x5C69bEe701ef814a2X6a3EDD4B1652CB9cc5aA6f is not a valid address.");
    }
}