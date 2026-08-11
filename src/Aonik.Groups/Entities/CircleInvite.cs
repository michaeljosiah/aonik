using Aonik.SharedKernel.Primitives;

namespace Aonik.PersonalFinance.Entities;

/// <summary>
/// An opaque, single-use, expiring invite to join an owner's circle (Spec 048 §7). The token is a
/// 256-bit cryptographically-random bearer capability (no signature/MAC — the DB row is the record of truth).
/// Carries the grant terms; on accept it materialises a <see cref="CircleGrant"/>.
/// The link rides the OS share sheet (WhatsApp-first); email/phone are delivery hints.
/// </summary>
public class CircleInvite : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid OwnerUserId { get; set; }

    /// <summary>Opaque, cryptographically random, unique per tenant.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>The owner, as a party (Spec 086). Alongside <see cref="OwnerUserId"/> through the transition.</summary>
    public Guid? OwnerPartyId { get; set; }

    /// <summary>One of <c>ShareResourceKinds</c>. Existing rows backfill to <c>care-entity</c>.</summary>
    public string ResourceKind { get; set; } = string.Empty;

    /// <summary>Domain-specific terms, written and read only by the owning module.</summary>
    public string? TermsJson { get; set; }

    public string Scope { get; set; } = "entities";
    public string EntityIdsJson { get; set; } = "[]";
    public bool NoAmounts { get; set; }

    /// <summary>email | phone | link — optional delivery hint.</summary>
    public string? Channel { get; set; }

    public DateTime ExpiresAt { get; set; }

    /// <summary>pending | accepted | expired | revoked.</summary>
    public string Status { get; set; } = "pending";

    public DateTime? ConsumedAt { get; set; }

    /// <summary>The grant created when this invite was accepted.</summary>
    public Guid? GrantId { get; set; }
}
