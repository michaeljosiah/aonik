using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Catalog;

/// <summary>
/// A per-variant, per-currency price, optionally time-boxed (Spec 042 §9). Commerce owns product
/// pricing; the Finance "Pricing" subsystem is FX/fee pricing and is not reused. Anemic.
/// </summary>
public class ProductPrice : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid ProductVariantId { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
}
