namespace Aonik.SharedKernel.Abstractions.Entitlements;

/// <summary>
/// The active signing key, and the set a client verifies against (Spec 090 §6).
/// </summary>
public interface IEntitlementKeyRing
{
    /// <summary>
    /// Generate a key and make it current. Several are valid at once, which is what makes rotation a non-event.
    /// </summary>
    Task<EntitlementKeyDescriptor> RotateAsync(
        TimeSpan signingLifetime,
        TimeSpan graceAllowance,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The key to sign with now, or null when none is current.
    ///
    /// <para>
    /// Null is a refusal to issue rather than a prompt to invent one: generating a key implicitly during an issue
    /// request would make the first token of a deployment depend on whichever request arrived first.
    /// </para>
    /// </summary>
    Task<EntitlementKeyDescriptor?> GetSigningKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The published set — <strong>complete, never a delta</strong>.
    ///
    /// <para>
    /// Completeness is what makes absence meaningful and withdrawal enforceable. A delta cannot express "this key
    /// is gone", and a client that never learns a key was withdrawn keeps honouring tokens signed with it.
    /// </para>
    /// </summary>
    Task<PublishedKeySet> GetPublishedSetAsync(CancellationToken cancellationToken = default);
}

/// <param name="PublicKey">Raw 32-byte Ed25519 material, base64url-unpadded.</param>
/// <param name="VerifyNotAfter">
/// When the key stops being <em>published</em>, which is a different date from when it stops signing (§6.1).
/// </param>
public sealed record EntitlementKeyDescriptor(
    string Kid,
    string PublicKey,
    DateTime NotBefore,
    DateTime SigningNotAfter,
    DateTime VerifyNotAfter);

/// <param name="SignedBytes">
/// The exact bytes the root signature covers, base64url-encoded.
///
/// <para>
/// The response carries them so <strong>no client re-serialises to verify</strong> — the same trick as the token
/// itself. A byte-precise token with an ambiguously-encoded key set fails the interoperability criterion at the
/// same late moment, for the same reason.
/// </para>
/// </param>
public sealed record PublishedKeySet(int Version, string SignedBytes, string Signature);

/// <summary>
/// Protects a signing key at rest.
///
/// <para>
/// Lives in SharedKernel because <c>Aonik.Subscriptions</c> owns entitlements and references only SharedKernel,
/// while the DataProtection stack it needs lives in Infrastructure. A module reaching across for a protector
/// would be the dependency edge ADR-005 exists to prevent.
/// </para>
/// </summary>
public interface IEntitlementKeyProtector
{
    string Protect(string value);

    string Unprotect(string value);
}

/// <summary>
/// Signs and verifies with Ed25519.
///
/// <para>
/// A seam rather than a direct library call, because .NET 10's BCL has no Ed25519 and the implementation is
/// therefore a third-party choice. Keeping it behind an interface means the choice is replaceable without
/// touching the format, which is the part that must never move.
/// </para>
/// </summary>
public interface IEd25519Signer
{
    /// <summary>Returns raw 32-byte public and 32-byte private key material.</summary>
    (byte[] PublicKey, byte[] PrivateKey) GenerateKeyPair();

    byte[] Sign(ReadOnlySpan<byte> message, ReadOnlySpan<byte> privateKey);

    bool Verify(ReadOnlySpan<byte> message, ReadOnlySpan<byte> signature, ReadOnlySpan<byte> publicKey);
}
