using Nethereum.Signer;
using UniswapSharp.Core.Utils;

namespace UniswapSharp.UniswapX.Utils;

/// <summary>Signature recovery for off-chain orders (ethers <c>recoverPublicKey</c> + <c>computeAddress</c> equivalent).</summary>
internal static class OrderSigning
{
    /// <summary>Recovers the checksummed signer address from a 65-byte <paramref name="signature"/> over <paramref name="hash"/>.</summary>
    public static string RecoverSigner(byte[] hash, string signature)
    {
        var sig = EthECDSASignatureFactory.ExtractECDSASignature(signature);
        var key = EthECKey.RecoverFromSignature(sig, hash);
        return AddressValidator.GetAddress(key.GetPublicAddress());
    }
}
