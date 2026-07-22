using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Catalog;

/// <summary>
/// A merchandised price point that overrides the size-plan formula at its size (Spec 068 §5) —
/// presets always win: 12 is 170.00 even where the formula says 185.00. Anemic.
/// </summary>
public class BundleSizePreset : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid BundleSizePlanId { get; set; }

    public int Size { get; set; }

    public decimal Price { get; set; }

    /// <summary>"Most popular", "Minimum order".</summary>
    public string? Badge { get; set; }

    public string? Blurb { get; set; }

    /// <summary>Authored display saving — never computed.</summary>
    public decimal? SavingAmount { get; set; }

    public int SortOrder { get; set; }
}
