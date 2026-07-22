using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Catalog;

/// <summary>
/// Container pricing for a bundle product priced by size — "box of N" (Spec 068 §4/§5). One per
/// bundle. boxPrice(size) = the preset price when a <see cref="BundleSizePreset"/> row exists for
/// that size, else BasePrice + (size − BaseSize) × PerSpacePrice. Everything here is data; nothing
/// is code. Requires <c>BundlePricingModes.SizeTiered</c> on the product. Anemic.
/// </summary>
public class BundleSizePlan : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid BundleProductId { get; set; }

    /// <summary>Smallest sellable box.</summary>
    public int MinSize { get; set; }

    /// <summary>Tunable ceiling — never hard-coded client-side.</summary>
    public int MaxSize { get; set; }

    /// <summary>Formula anchor (launch: 6).</summary>
    public int BaseSize { get; set; }

    /// <summary>Price at <see cref="BaseSize"/> (launch: 95.00).</summary>
    public decimal BasePrice { get; set; }

    /// <summary>Per space above <see cref="BaseSize"/> (launch: 15.00). Feeds the formula ONLY —
    /// around a discounted preset the marginal cost of a space bends, so grow-flow charges are
    /// always boxPrice(target) − boxPrice(current), never PerSpacePrice × spaces.</summary>
    public decimal PerSpacePrice { get; set; }

    public string Currency { get; set; } = "GBP";

    public List<BundleSizePreset> Presets { get; set; } = new();
}
