using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Catalog;

/// <summary>
/// Narrows the tenant option catalogue for one product (Spec 066 §5): which groups the product
/// offers, which choices within them, and an optional per-product default and selection mode.
/// Anemic.
/// </summary>
/// <remarks>
/// The absence of a row means the group is not offered — a product with no rows at all is simply
/// not personalisable, and its DTO carries an empty option list so storefronts hide the
/// personalisation UI entirely rather than rendering an empty panel.
/// </remarks>
public class ProductOptionGroup : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }
    public Guid OptionGroupId { get; set; }

    /// <summary>JSON array of the <see cref="OptionChoice.Key"/> values this product offers, e.g.
    /// <c>["prawns","salmon"]</c> for a fish dish that excludes chicken. Null = every active choice
    /// in the group. Keys are validated against the group at authoring time (rule V8).</summary>
    public string? AllowedChoiceKeysJson { get; set; }

    /// <summary>Per-product override of the group's recommended default. Null = inherit the
    /// group's. Must resolve to a choice this product still offers (rules V8/V11).</summary>
    public string? DefaultChoiceKey { get; set; }

    /// <summary>Per-product override of <see cref="OptionGroup.SelectionMode"/>. Null = inherit.
    /// Widening (One → Multi, e.g. a dish allowing two proteins) and tightening are both
    /// legitimate.</summary>
    public string? SelectionModeOverride { get; set; }

    public int SortOrder { get; set; }
}
