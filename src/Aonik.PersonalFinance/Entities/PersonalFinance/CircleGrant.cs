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

    /// <summary>all | entities | docsOnly.</summary>
    public string Scope { get; set; } = "entities";

    /// <summary>CareEntity ids (JSON array) for scope=entities|docsOnly.</summary>
    public string EntityIdsJson { get; set; } = "[]";

    /// <summary>True for docsOnly — hides every amount (defence in depth over the docs-only projection).</summary>
    public bool NoAmounts { get; set; }

    /// <summary>pending | active | revoked.</summary>
    public string Status { get; set; } = "pending";
}
