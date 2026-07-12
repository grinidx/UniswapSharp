using System.Numerics;
using AwesomeAssertions;
using UniswapSharp.UniswapX.Order;

namespace UniswapSharp.Testing.UniswapX;

// Port of uniswapx-sdk src/order/validation.test.ts.
public class ValidationTests
{
    private const string Zero = "0x0000000000000000000000000000000000000000";

    private static OrderInfo MakeOrderInfo(string data) => new()
    {
        Reactor = Zero,
        Swapper = Zero,
        Nonce = BigInteger.Zero,
        Deadline = 5,
        AdditionalValidationContract = Zero,
        AdditionalValidationData = data,
    };

    [Fact]
    public void ParsesAnExclusiveFillerValidation()
    {
        var validation = Validation.ParseValidation(MakeOrderInfo(
            "0x0000000000000000000000007fa9385be102ac3eac297483dd6233d62b3e14960000000000000000000000000000000000000000000000000000000000000033"));
        validation.Should().Be(new CustomOrderValidation(
            ValidationType.ExclusiveFiller,
            new ExclusiveFillerData("0x7FA9385bE102ac3EAc297483Dd6233D62b3e1496", 51)));
    }

    [Fact]
    public void ParsesEmptyValidationData()
    {
        var validation = Validation.ParseValidation(MakeOrderInfo("0x"));
        validation.Should().Be(new CustomOrderValidation(ValidationType.None, null));
    }

    [Fact]
    public void ParsesInvalidValidationData()
    {
        var validation = Validation.ParseValidation(MakeOrderInfo(
            "0x0000000000000000000000007fa9385be102ac3eac297483dd6233d62b3e1496000000000000000000000000000000000000000000000000000000000000033"));
        validation.Should().Be(new CustomOrderValidation(ValidationType.None, null));
    }

    [Fact]
    public void EncodesExclusiveFillerData()
    {
        const string fillerAddress = "0x1111111111111111111111111111111111111111";
        const string additionalValidationContract = "0x2222222222222222222222222222222222222222";
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 100;
        var validationInfo = Validation.EncodeExclusiveFillerData(
            fillerAddress, timestamp, 1, additionalValidationContract);

        var orderInfo = MakeOrderInfo(validationInfo.AdditionalValidationData);
        validationInfo.AdditionalValidationContract = additionalValidationContract;
        var validation = Validation.ParseValidation(orderInfo);
        validation.Should().Be(new CustomOrderValidation(
            ValidationType.ExclusiveFiller,
            new ExclusiveFillerData(fillerAddress, timestamp)));
    }
}
