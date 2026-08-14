using Aonik.SharedKernel.Abstractions.Entitlements;

using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;

namespace Aonik.Infrastructure.Entitlements;

/// <summary>
/// Ed25519 via BouncyCastle (Spec 090 §6).
///
/// <para>
/// A dependency the BCL forced: .NET 10 ships no Ed25519. BouncyCastle over a libsodium binding because it is
/// pure managed — no native binary per RID, which keeps container images and the Aspire story boring. The choice
/// is confined to this file; the format and the seam it implements must never move with it.
/// </para>
/// </summary>
internal sealed class BouncyCastleEd25519Signer : IEd25519Signer
{
    public (byte[] PublicKey, byte[] PrivateKey) GenerateKeyPair()
    {
        var random = new SecureRandom();
        var privateKey = new Ed25519PrivateKeyParameters(random);
        var publicKey = privateKey.GeneratePublicKey();

        // Raw 32-byte material both ways, matching the published encoding: every Ed25519 library
        // accepts raw keys, and a wrapper buys nothing when the algorithm is fixed at one value.
        return (publicKey.GetEncoded(), privateKey.GetEncoded());
    }

    public byte[] Sign(ReadOnlySpan<byte> message, ReadOnlySpan<byte> privateKey)
    {
        var signer = new Ed25519Signer();
        signer.Init(forSigning: true, new Ed25519PrivateKeyParameters(privateKey.ToArray()));
        signer.BlockUpdate(message.ToArray(), 0, message.Length);
        return signer.GenerateSignature();
    }

    public bool Verify(ReadOnlySpan<byte> message, ReadOnlySpan<byte> signature, ReadOnlySpan<byte> publicKey)
    {
        if (publicKey.Length != 32 || signature.Length != 64)
        {
            // Refused before the library sees them. A key or signature of the wrong length is a
            // malformed input, not a cryptographic near-miss.
            return false;
        }

        try
        {
            var verifier = new Ed25519Signer();
            verifier.Init(forSigning: false, new Ed25519PublicKeyParameters(publicKey.ToArray()));
            verifier.BlockUpdate(message.ToArray(), 0, message.Length);
            return verifier.VerifySignature(signature.ToArray());
        }
        catch (Exception)
        {
            // Malformed key material and similar are a verification failure, never an exception a
            // caller has to distinguish from "forged".
            return false;
        }
    }
}
