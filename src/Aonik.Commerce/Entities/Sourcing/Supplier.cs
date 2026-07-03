using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Sourcing;

/// <summary>
/// A counterparty we buy raw materials from (Spec 053 §8/§9) — the sourcing-side mirror of a
/// customer. Intentionally light: a name, the currency we transact in, an optional default lead
/// time and payment terms, an active flag. Not a full party/KYC record: <see cref="PartyId"/> is
/// an optional soft reference (opaque Guid, no FK) to a platform <c>Party</c>, mirroring how
/// <c>Order.PayerPartyId</c> soft-refs a Party across the module boundary — a supplier can exist
/// unlinked. Anemic.
/// </summary>
public class Supplier : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>Optional soft reference to a platform counterparty <c>Party</c> (no FK); null when
    /// unlinked. When set, a PO created against this supplier persists a <c>Supplier</c>
    /// <c>OrderPartyRole</c> for it (Spec 053 §11).</summary>
    public Guid? PartyId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>ISO 4217 currency code we buy from this supplier in (e.g. NGN, GBP).</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Default lead time in days; a <see cref="SupplierIngredient.LeadTimeDays"/> overrides per line.</summary>
    public int? LeadTimeDays { get; set; }

    /// <summary>Free-text payment terms, e.g. "Net 30".</summary>
    public string? PaymentTerms { get; set; }

    public bool IsActive { get; set; } = true;
}
