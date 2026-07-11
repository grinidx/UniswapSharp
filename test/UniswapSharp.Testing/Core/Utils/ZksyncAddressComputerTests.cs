using Nethereum.ABI;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using UniswapSharp.Core.Utils;

namespace UniswapSharp.Testing.Core.Utils;

// Pins ZksyncAddressComputer against the zkSync CREATE2 vector from
// sdks/v3-sdk/src/utils/computePoolAddress.test.ts ('should correctly compute zkevm pool address').
public class ZksyncAddressComputerTests
{
    [Fact]
    public void ComputesZkSyncPoolAddress()
    {
        // salt = keccak256(abiEncode(['address','address','uint24'], [USDCE, WETH, FeeAmount.MEDIUM]))
        var abiEncoded = new ABIEncode().GetABIEncoded(
            new ABIValue("address", "0x3355df6D4c9C3035724Fd0e3914dE96A5a83aaf4"),
            new ABIValue("address", "0x5AEa5775959fBC2557Cc8789bC1bf90A239D9a91"),
            new ABIValue("uint24", 3000));
        string salt = new Sha3Keccack().CalculateHash(abiEncoded).ToHex(true);

        string result = ZksyncAddressComputer.ComputeZksyncCreate2Address(
            "0x8FdA5a7a8dCA67BBcDd10F02Fa0649A937215422",
            "0x010013f177ea1fcbc4520f9a3ca7cd2d1d77959e05aa66484027cb38e712aeed",
            salt);

        Assert.Equal("0xff577f0E828a878743Ecc5E2632cbf65ceCf17cF", result);
    }
}
