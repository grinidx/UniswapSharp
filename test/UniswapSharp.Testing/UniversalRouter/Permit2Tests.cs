using System.Numerics;
using Nethereum.Signer;
using UniswapSharp.Permit2;
using UniswapSharp.UniversalRouter.Utils;
using UniswapSharp.V4.Utils;
using Constants = UniswapSharp.UniversalRouter.Utils.Constants;

namespace UniswapSharp.Testing.UniversalRouter;

// Ported from sdks/universal-router-sdk/test/utils/permit2.test.ts (+ test/utils/permit2.ts helpers).
public class Permit2Tests
{
    private const string PERMIT2_ADDRESS = "0x000000000022D473030F116dDEE9F6B43aC78BA3";
    private const string TEST_DEADLINE = "3000000000000";
    private const string PERMIT_STRUCT =
        "((address token,uint160 amount,uint48 expiration,uint48 nonce) details,address spender,uint256 sigDeadline)";

    // wallet from ethers `new Wallet(utils.zeroPad('0x1234', 32))`
    private const string PrivateKey = "0x0000000000000000000000000000000000000000000000000000000000001234";

    private static PermitSingle MakePermit(string token, string amount)
    {
        string router = Constants.UNIVERSAL_ROUTER_ADDRESS(UniversalRouterVersion.V2_0, 1);
        return new PermitSingle(
            new PermitDetails(token, BigInteger.Parse(amount), BigInteger.Parse(TEST_DEADLINE), 0),
            router,
            BigInteger.Parse(TEST_DEADLINE));
    }

    // returns the 65-byte canonical signature (r + s + v, v ∈ {0x1b, 0x1c}), matching ethers _signTypedData
    private static string GeneratePermitSignature(PermitSingle permit)
    {
        string digestHex = AllowanceTransfer.Hash(permit, PERMIT2_ADDRESS, 1);
        byte[] digest = AbiParamEncoder.ToBytes(digestHex);
        var key = new EthECKey(PrivateKey);
        var sig = key.SignAndCalculateV(digest);
        byte[] r = ToWord(sig.R);
        byte[] s = ToWord(sig.S);
        byte v = sig.V[0]; // 27 or 28
        return "0x" + Convert.ToHexStringLower(r) + Convert.ToHexStringLower(s) + v.ToString("x2");
    }

    private static byte[] ToWord(byte[] b)
    {
        var word = new byte[32];
        Array.Copy(b, 0, word, 32 - b.Length, b.Length);
        return word;
    }

    private static string GetSanitizedSignature(PermitSingle permit, string signature)
    {
        var planner = new RoutePlanner();
        var permit2 = new Permit2Permit(permit.Details, permit.Spender, permit.SigDeadline, signature);
        InputTokens.EncodePermit(planner, permit2);
        var decoded = AbiParamDecoder.Decode(new[] { PERMIT_STRUCT, "bytes" }, planner.Inputs[0]);
        return (string)decoded[1]!;
    }

    [Fact]
    public void DoesNotSanitizeNormalPermit()
    {
        var permit = MakePermit(UniswapData.USDC.Address, "1000000000"); // parseUnits('1000', 6)
        string signature = GeneratePermitSignature(permit);
        Assert.Equal(signature.ToLowerInvariant(), GetSanitizedSignature(permit, signature).ToLowerInvariant());
    }

    [Fact]
    public void DoesNotSanitizeTripleLengthPermit()
    {
        var permit = MakePermit(UniswapData.USDC.Address, "1000000000");
        string signature = GeneratePermitSignature(permit);
        string multisig = signature + signature[2..] + signature[2..];
        Assert.Equal(multisig.ToLowerInvariant(), GetSanitizedSignature(permit, multisig).ToLowerInvariant());
    }

    [Fact]
    public void DoesNotSanitizeShortPermit()
    {
        var permit = MakePermit(UniswapData.USDC.Address, "1000000000");
        const string tiny = "0x12341234132412341344";
        Assert.Equal(tiny.ToLowerInvariant(), GetSanitizedSignature(permit, tiny).ToLowerInvariant());
    }

    [Fact]
    public void SanitizesMalformedPermit()
    {
        var permit = MakePermit(UniswapData.USDC.Address, "1000000000");
        string original = GeneratePermitSignature(permit);

        int recoveryParam = Signatures.SplitSignature(original).RecoveryParam;
        // slice off current v, append recoveryParam as v
        string malformed = original[..^2] + recoveryParam.ToString("x2");

        Assert.Equal(original.ToLowerInvariant(), GetSanitizedSignature(permit, malformed).ToLowerInvariant());
    }
}
