using System.Security.Cryptography;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;

namespace UniswapSharp.Tamperproof.Utils;

/// <summary>
/// Bridges the <see cref="SigningAlgorithmConfig"/> registry onto the platform crypto primitives:
/// RSA/ECDSA via <c>System.Security.Cryptography</c>, Ed25519 via BouncyCastle (no managed Ed25519 in
/// net10.0). ECDSA signatures use the raw IEEE P1363 <c>r||s</c> concatenation (not DER), matching
/// WebCrypto.
/// </summary>
internal static class SigningCrypto
{
    /// <summary>
    /// Signs <paramref name="data"/> with a PKCS#8 private key under <paramref name="config"/>.
    /// </summary>
    public static byte[] Sign(SigningAlgorithmConfig config, byte[] pkcs8PrivateKey, byte[] data)
    {
        switch (config.Family)
        {
            case SigningFamily.Rsa:
            case SigningFamily.RsaPss:
                {
                    using var rsa = RSA.Create();
                    rsa.ImportPkcs8PrivateKey(pkcs8PrivateKey, out _);
                    RSASignaturePadding padding = config.Family == SigningFamily.RsaPss
                        ? RSASignaturePadding.Pss
                        : RSASignaturePadding.Pkcs1;
                    return rsa.SignData(data, config.Hash!.Value, padding);
                }

            case SigningFamily.Ecdsa:
                {
                    using var ecdsa = ECDsa.Create();
                    ecdsa.ImportPkcs8PrivateKey(pkcs8PrivateKey, out _);
                    return ecdsa.SignData(
                        data,
                        config.Hash!.Value,
                        DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
                }

            case SigningFamily.Ed25519:
                {
                    var key = (Ed25519PrivateKeyParameters)PrivateKeyFactory.CreateKey(pkcs8PrivateKey);
                    var signer = new Ed25519Signer();
                    signer.Init(true, key);
                    signer.BlockUpdate(data, 0, data.Length);
                    return signer.GenerateSignature();
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(config));
        }
    }

    /// <summary>
    /// Verifies <paramref name="signature"/> over <paramref name="data"/> with an SPKI public key.
    /// </summary>
    public static bool Verify(
        SigningAlgorithmConfig config,
        byte[] spkiPublicKey,
        byte[] signature,
        byte[] data)
    {
        switch (config.Family)
        {
            case SigningFamily.Rsa:
            case SigningFamily.RsaPss:
                {
                    using var rsa = RSA.Create();
                    rsa.ImportSubjectPublicKeyInfo(spkiPublicKey, out _);
                    RSASignaturePadding padding = config.Family == SigningFamily.RsaPss
                        ? RSASignaturePadding.Pss
                        : RSASignaturePadding.Pkcs1;
                    return rsa.VerifyData(data, signature, config.Hash!.Value, padding);
                }

            case SigningFamily.Ecdsa:
                {
                    using var ecdsa = ECDsa.Create();
                    ecdsa.ImportSubjectPublicKeyInfo(spkiPublicKey, out _);
                    return ecdsa.VerifyData(
                        data,
                        signature,
                        config.Hash!.Value,
                        DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
                }

            case SigningFamily.Ed25519:
                {
                    var key = (Ed25519PublicKeyParameters)PublicKeyFactory.CreateKey(spkiPublicKey);
                    var verifier = new Ed25519Signer();
                    verifier.Init(false, key);
                    verifier.BlockUpdate(data, 0, data.Length);
                    return verifier.VerifySignature(signature);
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(config));
        }
    }
}
