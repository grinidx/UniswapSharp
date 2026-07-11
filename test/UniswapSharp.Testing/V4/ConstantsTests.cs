using System.Numerics;
using UniswapSharp.V4;

namespace UniswapSharp.Testing.V4;

// Smoke coverage for the ported v4-sdk internalConstants / actionConstants.
public class ConstantsTests
{
    [Fact]
    public void CoreConstants()
    {
        Assert.Equal("0x0000000000000000000000000000000000000000", Constants.ADDRESS_ZERO);
        Assert.Equal("0x", Constants.EMPTY_BYTES);
        Assert.Equal(BigInteger.Pow(10, 18), Constants.ONE_ETHER);
        Assert.Equal(BigInteger.Pow(2, 96), Constants.Q96);
        Assert.Equal(BigInteger.Pow(2, 192), Constants.Q192);
        // SQRT_PRICE_1_1 = encodeSqrtRatioX96(1, 1) = Q96
        Assert.Equal(Constants.Q96, Constants.SQRT_PRICE_1_1);
    }

    [Fact]
    public void FeeAmountsAndTickSpacings()
    {
        Assert.Equal(100, (int)Constants.FeeAmount.LOWEST);
        Assert.Equal(500, (int)Constants.FeeAmount.LOW);
        Assert.Equal(3000, (int)Constants.FeeAmount.MEDIUM);
        Assert.Equal(10000, (int)Constants.FeeAmount.HIGH);

        Assert.Equal(1, Constants.TICK_SPACINGS[Constants.FeeAmount.LOWEST]);
        Assert.Equal(10, Constants.TICK_SPACINGS[Constants.FeeAmount.LOW]);
        Assert.Equal(60, Constants.TICK_SPACINGS[Constants.FeeAmount.MEDIUM]);
        Assert.Equal(200, Constants.TICK_SPACINGS[Constants.FeeAmount.HIGH]);
    }

    [Fact]
    public void PositionFunctionSelectors()
    {
        Assert.Equal("initializePool", PositionFunctions.INITIALIZE_POOL);
        Assert.Equal("modifyLiquidities", PositionFunctions.MODIFY_LIQUIDITIES);
        Assert.Equal("0x002a3e3a", PositionFunctions.PERMIT_BATCH);
        Assert.Equal("0x0f5730f1", PositionFunctions.ERC721PERMIT_PERMIT);
    }

    [Fact]
    public void MsgSender()
    {
        Assert.Equal("0x0000000000000000000000000000000000000001", ActionConstants.MSG_SENDER);
    }
}
