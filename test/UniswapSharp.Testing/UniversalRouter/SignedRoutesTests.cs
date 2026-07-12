using System.Numerics;
using System.Text.RegularExpressions;
using UniswapSharp.UniversalRouter;
using UniswapSharp.UniversalRouter.Utils;

namespace UniswapSharp.Testing.UniversalRouter;

// Ported from sdks/universal-router-sdk/test/unit/signedRoutes.test.ts
public class SignedRoutesTests
{
    private const int chainId = 1;
    private static readonly string routerAddress =
        Constants.UNIVERSAL_ROUTER_ADDRESS(UniversalRouterVersion.V2_0, chainId);
    private const long deadline = 2000000000;

    private static readonly string mockCalldata =
        SwapRouter.EncodeExecute("0x00", new List<string> { "0x" }, deadline);

    private static readonly Regex HexLower = new("^0x[0-9a-f]+$");
    private static readonly Regex Bytes32Lower = new("^0x[0-9a-f]{64}$");

    [Fact]
    public void GetExecuteSignedPayload_GeneratesEip712PayloadFromCalldata()
    {
        var signedOptions = new SignedRouteOptions
        {
            Intent = "0x" + new string('0', 64),
            Data = "0x" + new string('1', 64),
            Sender = "0x0000000000000000000000000000000000000000",
        };

        var payload = SwapRouter.GetExecuteSignedPayload(mockCalldata, signedOptions, deadline, chainId, routerAddress);

        Assert.Equal("UniversalRouter", payload.Domain.Name);
        Assert.Equal("2", payload.Domain.Version);
        Assert.Equal(new BigInteger(chainId), payload.Domain.ChainId);
        Assert.Equal(routerAddress, payload.Domain.VerifyingContract);
        Assert.Matches(HexLower, payload.Value.Commands);
        Assert.NotNull(payload.Value.Inputs);
        Assert.Matches(Bytes32Lower, payload.Value.Nonce);
        Assert.Equal(signedOptions.Intent, payload.Value.Intent);
        Assert.Equal(signedOptions.Data, payload.Value.Data);
        Assert.Equal(signedOptions.Sender, payload.Value.Sender);
        Assert.Equal(deadline.ToString(), payload.Value.Deadline);
    }

    [Fact]
    public void GetExecuteSignedPayload_UsesProvidedNonce()
    {
        string customNonce = "0x" + new string('a', 64);
        var signedOptions = new SignedRouteOptions
        {
            Intent = "0x" + new string('0', 64),
            Data = "0x" + new string('1', 64),
            Sender = "0x0000000000000000000000000000000000000000",
            Nonce = customNonce,
        };

        var payload = SwapRouter.GetExecuteSignedPayload(mockCalldata, signedOptions, deadline, chainId, routerAddress);
        Assert.Equal(customNonce, payload.Value.Nonce);
    }

    [Fact]
    public void GetExecuteSignedPayload_UsesNonceSkipCheckSentinel()
    {
        var signedOptions = new SignedRouteOptions
        {
            Intent = "0x" + new string('0', 64),
            Data = "0x" + new string('1', 64),
            Sender = "0x0000000000000000000000000000000000000000",
            Nonce = Eip712.NONCE_SKIP_CHECK,
        };

        var payload = SwapRouter.GetExecuteSignedPayload(mockCalldata, signedOptions, deadline, chainId, routerAddress);
        Assert.Equal(Eip712.NONCE_SKIP_CHECK, payload.Value.Nonce);
    }

    [Fact]
    public void EncodeExecuteSigned_EncodesWithSignature()
    {
        string signature = "0x" + new string('0', 130);
        var signedOptions = new SignedRouteOptions
        {
            Intent = "0x" + new string('0', 64),
            Data = "0x" + new string('1', 64),
            Sender = "0x1234567890123456789012345678901234567890",
            Nonce = Eip712.GenerateNonce(),
        };

        var result = SwapRouter.EncodeExecuteSigned(mockCalldata, signature, signedOptions, deadline, BigInteger.Zero);

        Assert.Matches(HexLower, result.Calldata);
        Assert.Equal("0x00", result.Value);
        Assert.Equal(SwapRouter.GetSighash("executeSigned"), result.Calldata[..10]);
    }

    [Fact]
    public void EncodeExecuteSigned_SetsVerifySenderBasedOnSenderAddress()
    {
        string signature = "0x" + new string('0', 130);

        var signedOptions1 = new SignedRouteOptions
        {
            Intent = "0x" + new string('0', 64),
            Data = "0x" + new string('1', 64),
            Sender = "0x0000000000000000000000000000000000000000",
            Nonce = Eip712.GenerateNonce(),
        };
        var result1 = SwapRouter.EncodeExecuteSigned(mockCalldata, signature, signedOptions1, deadline, BigInteger.Zero);
        Assert.False(SwapRouter.DecodeExecuteSigned(result1.Calldata).VerifySender);

        var signedOptions2 = new SignedRouteOptions
        {
            Intent = "0x" + new string('0', 64),
            Data = "0x" + new string('1', 64),
            Sender = "0x1234567890123456789012345678901234567890",
            Nonce = Eip712.GenerateNonce(),
        };
        var result2 = SwapRouter.EncodeExecuteSigned(mockCalldata, signature, signedOptions2, deadline, BigInteger.Zero);
        Assert.True(SwapRouter.DecodeExecuteSigned(result2.Calldata).VerifySender);
    }

    [Fact]
    public void EncodeExecuteSigned_MaintainsNonceConsistencyBetweenPayloadAndEncoding()
    {
        string customNonce = "0x" + new string('b', 64);
        string signature = "0x" + new string('0', 130);
        var signedOptions = new SignedRouteOptions
        {
            Intent = "0x" + new string('0', 64),
            Data = "0x" + new string('1', 64),
            Sender = "0x0000000000000000000000000000000000000000",
            Nonce = customNonce,
        };

        var payload = SwapRouter.GetExecuteSignedPayload(mockCalldata, signedOptions, deadline, chainId, routerAddress);
        var result = SwapRouter.EncodeExecuteSigned(mockCalldata, signature, signedOptions, deadline, BigInteger.Zero);
        var decoded = SwapRouter.DecodeExecuteSigned(result.Calldata);

        Assert.Equal(customNonce, decoded.Nonce);
        Assert.Equal(payload.Value.Nonce, decoded.Nonce);
    }

    [Fact]
    public void GenerateNonce_GeneratesValid32ByteNonces()
    {
        string nonce1 = Eip712.GenerateNonce();
        string nonce2 = Eip712.GenerateNonce();

        Assert.Matches(Bytes32Lower, nonce1);
        Assert.Matches(Bytes32Lower, nonce2);
        Assert.NotEqual(nonce1, nonce2);
    }
}
