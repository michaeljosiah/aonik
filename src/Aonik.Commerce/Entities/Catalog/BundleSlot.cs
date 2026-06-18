using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Catalog;

/// <summary>
/// A selection group within a composite / build-your-own-box product (Spec 042 §12). The buyer
/// chooses between <see cref="MinItems"/> and <see cref="MaxItems"/> component variants for this
/// slot, sourced either from a category (<see cref="FromCategoryId"/>) or an explicit allow-list
/// (<see cref="BundleSlotOption"/>). Anemic.
/// </summary>
public class BundleSlot : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid BundleProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MinItems { get; set; }
    public int MaxItems { get; set; }

    /// <summary>When set, any active variant whose product is in this category is eligible.</summary>
    public Guid? FromCategoryId { get; set; }

    public bool AllowDuplicates { get; set; }
    public int SortOrder { get; set; }

    public List<BundleSlotOption> Options { get; set; } = new();
}
