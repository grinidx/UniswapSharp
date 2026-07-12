using AwesomeAssertions;
using UniswapSharp.UniswapX;
using UniswapSharp.UniswapX.Utils;

namespace UniswapSharp.Testing.UniswapX;

// Port of uniswapx-sdk src/constants.test.ts.
public class ConstantsTests
{
    private const string CanonicalPermit2 = "0x000000000022D473030F116dDEE9F6B43aC78BA3";
    private const string CanonicalQuoter = "0x00000000a3db63Df9078cBF3dF88B4CAdD5a7F58";
    private const string Zero = "0x0000000000000000000000000000000000000000";

    // DutchV3 rollout to Robinhood (4663) and Arc (5042).
    [Theory]
    [InlineData(4663, "0x000000007A1C8e570011EeDF86A2A35593013cBA")]
    [InlineData(5042, "0x0000000015134054eA82AE0bb9fda66b36402C36")]
    public void DutchV3Rollout_GetReactorResolvesDeployedReactor(int chainId, string reactor)
    {
        OrderUtils.GetReactor(chainId, OrderType.Dutch_V3).ToLowerInvariant()
            .Should().Be(reactor.ToLowerInvariant());
    }

    [Theory]
    [InlineData(4663)]
    [InlineData(5042)]
    public void DutchV3Rollout_MapsToCanonicalOrderQuoter(int chainId)
    {
        Constants.UniswapxOrderQuoterMapping[chainId].ToLowerInvariant()
            .Should().Be(CanonicalQuoter.ToLowerInvariant());
    }

    [Theory]
    [InlineData(4663)]
    [InlineData(5042)]
    public void DutchV3Rollout_MapsToCanonicalPermit2(int chainId)
    {
        Constants.Permit2Mapping[chainId].ToLowerInvariant()
            .Should().Be(CanonicalPermit2.ToLowerInvariant());
    }

    [Theory]
    [InlineData(4663)]
    [InlineData(5042)]
    public void DutchV3Rollout_UsesZeroAddressForExclusiveFillerValidation(int chainId)
    {
        Constants.ExclusiveFillerValidationMapping[chainId].Should().Be(Zero);
    }

    [Fact]
    public void ReactorAddressMapping_MatchesSnapshot()
    {
        var m = Constants.ReactorAddressMapping;
        m[1][OrderType.Dutch].Should().Be("0x6000da47483062A0D734Ba3dc7576Ce6A0B645C4");
        m[1][OrderType.Dutch_V2].Should().Be("0x00000011F84B9aa48e5f8aA8B9897600006289Be");
        m[1][OrderType.Dutch_V3].Should().Be("0x0000000015757c461808EA25Eb309638B62681cf");
        m[1][OrderType.Priority].Should().Be("0x0000000000000000000000000000000000000000");
        m[1][OrderType.Relay].Should().Be("0x0000000000A4e21E2597DCac987455c48b12edBF");
        m[10][OrderType.Dutch_V3].Should().Be("0x000000000923439A92daE8930613568824108631");
        m[11155111][OrderType.Dutch].Should().Be("0xD6c073F2A3b676B8f9002b276B618e0d8bA84Fad");
        m[11155111][OrderType.Dutch_V2].Should().Be("0x0e22B6638161A89533940Db590E67A52474bEBcd");
        m[130][OrderType.Priority].Should().Be("0x00000006021a6Bce796be7ba509BBBA71e956e37");
        m[130][OrderType.Dutch_V3].Should().Be("0x000000005aF66799D1a6317714D66800f9CA1406");
        m[1301][OrderType.Hybrid].Should().Be("0x000000000C75276D956cc35218ca8f132D877957");
        m[8453][OrderType.Priority].Should().Be("0x000000001Ec5656dcdB24D90DFa42742738De729");
        m[8453][OrderType.Dutch_V3].Should().Be("0x000000008a8330B5d1F43A62Bf4C673A49f27ba0");
        m[42161][OrderType.Dutch_V2].Should().Be("0x1bd1aAdc9E230626C44a139d7E70d842749351eb");
        m[42161][OrderType.Dutch_V3].Should().Be("0xB274d5F4b833b61B340b654d600A864fB604a87c");
        m[4663][OrderType.Dutch_V3].Should().Be("0x000000007A1C8e570011EeDF86A2A35593013cBA");
        m[5042][OrderType.Dutch_V3].Should().Be("0x0000000015134054eA82AE0bb9fda66b36402C36");
    }
}
