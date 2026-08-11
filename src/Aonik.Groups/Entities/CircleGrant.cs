using Aonik.SharedKernel.Primitives;

namespace Aonik.PersonalFinance.Entities;

/// <summary>
/// What a circle member may see of an owner's records (Spec 048). Authoritative
/// for Simi visibility (the legacy HouseholdMember.PermissionsJson is unenforced).
/// Scope + NoAmounts express the three presets: all · entities · docsOnly.
/// </summary>
public class CircleGrant : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>Whose records are shared.</summary>
    public Guid OwnerUserId { get; set; }

    /// <summary>The member; null until an invite is accepted (§7).</summary>
    public Guid? MemberUserId { get; set; }

    /// <summary>Optional container (Spec 020); null = stand-alone grant (Open decision O2).</summary>
    public Guid? HouseholdId { get; set; }

    /// <summary>
    /// Whose records are shared, as a party (Spec 086). Added ALONGSIDE
    /// <see cref="OwnerUserId"/> rather than replacing it: the deployed CircleService compares the
    /// user columns against the authenticated user id, so re-pointing them in place would make
    /// every existing grant vanish — unlistable and unrevocable — until the reader cutover shipped.
    /// </summary>
    public Guid? OwnerPartyId { get; set; }

    /// <summary>The member, as a party. Null until an invite is accepted.</summary>
    public Guid? MemberPartyId { get; set; }

    /// <summary>
    /// What sort of thing is shared — one of <c>ShareResourceKinds</c> (Spec 086 §6). Existing rows
    /// backfill to <c>care-entity</c>, which is what <see cref="EntityIdsJson"/> has always meant.
    /// </summary>
    public string ResourceKind { get; set; } = string.Empty;

    /// <summary>
    /// Domain-specific terms the OWNING MODULE interprets. The platform stores and returns this and
    /// never reads it, which is what keeps one domain's redaction rules off a platform entity.
    /// </summary>
    public string? TermsJson { get; set; }

    /// <summary>all | entities | docsOnly.</summary>
    public string Scope { get; set; } = "entities";

    /// <summary>CareEntity ids (JSON array) for scope=entities|docsOnly.</summary>
    public string EntityIdsJson { get; set; } = "[]";

    /// <summary>True for docsOnly — hides every amount (defence in depth over the docs-only projection).</summary>
    public bool NoAmounts { get; set; }

    /// <summary>pending | active | revoked.</summary>
    public string Status { get; set; } = "pending";
}
