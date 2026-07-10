using System.Numerics;
using Nethereum.Util;
using UniswapSharp.V3;
using NFPM = UniswapSharp.V3.NonfungiblePositionManager;

namespace UniswapSharp.Testing.V3;

// Ported from nonfungiblePositionManager.test.ts (#safeTransferFromParameters, #getPermitData)
public class NonfungiblePositionManagerSafeTransferTests
{
    private const string recipient = "0x0000000000000000000000000000000000000003";
    private const string sender = "0x0000000000000000000000000000000000000004";
    private static readonly BigInteger tokenId = 1;

    [Fact]
    public void SafeTransferFrom_NoDataParam()
    {
        var result = NFPM.SafeTransferFromParameters(new NFPM.SafeTransferOptions
        {
            Sender = sender,
            Recipient = recipient,
            TokenId = tokenId
        });
        Assert.Equal("0x42842e0e000000000000000000000000000000000000000000000000000000000000000400000000000000000000000000000000000000000000000000000000000000030000000000000000000000000000000000000000000000000000000000000001", result.Calldata);
        Assert.Equal("0x00", result.Value);
    }

    [Fact]
    public void SafeTransferFrom_DataParam()
    {
        var result = NFPM.SafeTransferFromParameters(new NFPM.SafeTransferOptions
        {
            Sender = sender,
            Recipient = recipient,
            TokenId = tokenId,
            Data = "0x0000000000000000000000000000000000009004"
        });
        Assert.Equal("0xb88d4fde000000000000000000000000000000000000000000000000000000000000000400000000000000000000000000000000000000000000000000000000000000030000000000000000000000000000000000000000000000000000000000000001000000000000000000000000000000000000000000000000000000000000008000000000000000000000000000000000000000000000000000000000000000140000000000000000000000000000000000009004000000000000000000000000", result.Calldata);
        Assert.Equal("0x00", result.Value);
    }

    [Fact]
    public void GetPermitData_Succeeds()
    {
        const string positionManager = "0x0000000000000000000000000000000000000001";
        var permit = new NFPM.NFTPermitValues
        {
            Spender = "0x0000000000000000000000000000000000000002",
            TokenId = 1,
            Deadline = 123,
            Nonce = 1
        };

        var data = NFPM.GetPermitData(permit, positionManager, 1);

        Assert.Equal("Uniswap V3 Positions NFT-V1", data.Domain.Name);
        Assert.Equal(1, data.Domain.ChainId);
        Assert.Equal("1", data.Domain.Version);
        Assert.Equal(positionManager, data.Domain.VerifyingContract);
        Assert.Same(permit, data.Values);

        var fields = data.Types["Permit"];
        Assert.Equal(new[] { "spender", "tokenId", "nonce", "deadline" }, fields.Select(f => f.Name));
        Assert.Equal(new[] { "address", "uint256", "uint256", "uint256" }, fields.Select(f => f.Type));

        // EIP-712 type hash, per ERC721Permit.sol
        string encodedType = "Permit(" + string.Join(",", fields.Select(f => f.Type + " " + f.Name)) + ")";
        string typeHash = "0x" + Sha3Keccack.Current.CalculateHash(encodedType);
        Assert.Equal("0x49ecf333e5b8c95c40fdafc95c1ad136e8914a8fb55e9dc8bb01eaa83a2df9ad", typeHash);
    }
}
