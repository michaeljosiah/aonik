using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.PersonalFinance;

/// <summary>
/// A person ("Mum") or an asset (a property, land, a vehicle, a business
/// stake, a foreign account) that a Simi user looks after across borders —
/// the foundational, tenant- and user-scoped object of the Simi product
/// (Spec 043). It owns commitments, payment history, documents, and
/// per-currency totals. Anemic per the AONIK entity rule: all behaviour
/// lives in <c>CareEntityService</c> / <c>CareEntityProfileService</c>.
/// </summary>
public class CareEntity : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>Owner. Scoped per user like <c>Bill</c> / <c>PersonalAccount</c>.</summary>
    public Guid UserId { get; set; }

    // ── Identity &amp; type ───────────────────────────────────────────────
    /// <summary>
    /// <c>person</c> | <c>asset</c> | <c>organization</c> (Spec 043 §6, widened by Spec 049 §4).
    /// <c>organization</c> models a body the user has a standing financial relationship with but
    /// does not own and that is not a person (a church, a school, a cooperative, a charity). The
    /// stored value is deliberately generic; the consuming frontend chooses the audience label.
    /// </summary>
    public string Kind { get; set; } = "person";

    /// <summary>
    /// property | land | vehicle | business | account | other. Only a <c>kind = asset</c> carries
    /// an assetType; <c>person</c> and <c>organization</c> must not (Spec 049 §5).
    /// </summary>
    public string? AssetType { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>ISO-3166-1 alpha-2 → flag + default currency.</summary>
    public string CountryCode { get; set; } = string.Empty;

    // ── Presentation &amp; meaning ────────────────────────────────────────
    /// <summary>"mother", "co-owned rental" — free text (avoid bank-speak).</summary>
    public string? Relationship { get; set; }

    /// <summary>Avatar fallback glyph.</summary>
    public string? Emoji { get; set; }

    /// <summary>Optional avatar, stored via Documents (Spec 035).</summary>
    public Guid? PhotoDocumentId { get; set; }

    // ── Extensibility (no schema churn for new asset types) ─────────────
    /// <summary>Type-specific bag: vehicle reg, property address, land title, … Defaults to "{}".</summary>
    public string AttributesJson { get; set; } = "{}";

    // ── Lifecycle ───────────────────────────────────────────────────────
    /// <summary>Hidden from grids; history preserved (never hard-delete).</summary>
    public bool Archived { get; set; }
}
