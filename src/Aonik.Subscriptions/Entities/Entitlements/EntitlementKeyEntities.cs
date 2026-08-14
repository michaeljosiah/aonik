using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Primitives;

namespace Aonik.Subscriptions.Entities.Entitlements;

/// <summary>
/// One Ed25519 signing key (Spec 090 §6, §12).
///
/// <para>
/// <c>NotAfter</c> has <strong>two distinct meanings and needs two fields</strong>. Overlapping keys stop a
/// rotation invalidating tokens at the moment of rotation; they say nothing about <em>retirement</em>. If a key
/// is dropped from the published set while tokens it signed are still inside their grace window, every offline
/// paying customer holding one silently degrades to the free tier — with a valid, unexpired, unrevoked token in
/// hand. It looks exactly like a licensing bug to the customer and exactly like correct rotation to us, and it
/// lands on the users least able to recover: the ones who are offline.
/// </para>
/// </summary>
public class EntitlementSigningKey : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>Names the key in tokens and the published set. E.g. <c>2026-08-a</c>.</summary>
    public string Kid { get; set; } = string.Empty;

    public string Algorithm { get; set; } = "Ed25519";

    /// <summary>Raw 32-byte public key, base64url-unpadded — the published encoding, stored ready to publish.</summary>
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>The private key, protected at rest. It never leaves the server.</summary>
    public string ProtectedPrivateKey { get; set; } = string.Empty;

    public DateTime NotBefore { get; set; }

    /// <summary>Stop <em>issuing</em> with this key.</summary>
    public DateTime SigningNotAfter { get; set; }

    /// <summary>
    /// Stop <em>publishing</em> it — at least the maximum grace beyond <see cref="SigningNotAfter"/>, and pushed
    /// later by issuance itself: a token may not be issued whose grace outlives the key that signed it (§6.1).
    /// </summary>
    public DateTime VerifyNotAfter { get; set; }

    public string Status { get; set; } = EntitlementKeyStatuses.Active;
}

/// <summary>
/// The audit row per issued token (Spec 090 §12) — what makes "one token on a hundred machines" observable.
/// </summary>
public class EntitlementTokenIssue : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public string SubscriberKind { get; set; } = SubscriberKindsFallback.Party;

    public Guid SubscriberId { get; set; }

    /// <summary>Unique per token. Without it per-token revocation cannot work at all.</summary>
    public Guid Jti { get; set; }

    public string Kid { get; set; } = string.Empty;

    /// <summary>The revocation handle baked into this token (§9.1).</summary>
    public string RevocationHandle { get; set; } = string.Empty;

    public string? DeviceFingerprint { get; set; }

    public DateTime IssuedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Persisted, <strong>not derived</strong> (§12). <c>exp</c> and <c>gra</c> are distinct claims, and the
    /// grace window is tenant-configurable and can change after a token is signed — recomputing from
    /// <see cref="ExpiresAt"/> plus today's configuration gives the wrong answer for exactly the tokens that
    /// matter, and §6.1's retirement invariant needs <c>MAX(gra)</c> per <c>kid</c>. Store the value that was
    /// actually signed.
    /// </summary>
    public DateTime GraceUntil { get; set; }
}

/// <summary>A revocation entry (Spec 090 §9, §12).</summary>
public class EntitlementRevocation : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>Set for a per-token revocation.</summary>
    public Guid? Jti { get; set; }

    /// <summary>
    /// Set for a subscriber-wide revocation — as the <em>handle</em>, never the subscriber id, so the public
    /// list does not name who was revoked (§9.1). Only a holder of the token can compute the handle to look up.
    /// </summary>
    public string? RevocationHandle { get; set; }

    public string Reason { get; set; } = string.Empty;

    public DateTime AddedAt { get; set; }

    /// <summary>
    /// When the entry may be swept: once every token it could match has left its grace window, keeping the
    /// published list from growing without bound.
    /// </summary>
    public DateTime SweepAfter { get; set; }
}

public static class EntitlementKeyStatuses
{
    public const string Active = "active";

    /// <summary>Signing has stopped; the key remains published until its <c>VerifyNotAfter</c>.</summary>
    public const string Retiring = "retiring";

    /// <summary>
    /// Compromised, withdrawn immediately, breakage accepted — because the alternative is honouring forged
    /// tokens. A decision made loudly with support forewarned, never a scheduled event.
    /// </summary>
    public const string Withdrawn = "withdrawn";
}

/// <summary>
/// Local copy of the subscriber-kind default so the entity file does not need a using for one constant.
/// </summary>
internal static class SubscriberKindsFallback
{
    public const string Party = "party";
}
