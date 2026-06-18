using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Catalog;

/// <summary>
/// An explicit eligible component variant for a <see cref="BundleSlot"/> (Spec 042 §12). Used when a
/// slot's options are curated rather than sourced from a whole category. The optional
/// <see cref="PriceDelta"/> adjusts the component's contribution under sum-based bundle pricing.
/// Anemic.
/// </summary>
public class BundleSlotOption : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid BundleSlotId { get; set; }
    public Guid ProductVariantId { get; set; }
    public decimal? PriceDelta { get; set; }
}
