using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Inventory;

/// <summary>
/// Stock for a product variant at an optional location (Spec 042 §10). Available = OnHand - Reserved.
/// A bundle product holds no stock of its own; only component variants do. Anemic.
/// </summary>
public class InventoryLevel : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid ProductVariantId { get; set; }
    public string? Location { get; set; }
    public decimal OnHand { get; set; }
    public decimal Reserved { get; set; }
}
