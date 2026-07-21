using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Catalog;

/// <summary>
/// One selectable value within an <see cref="OptionGroup"/> (Spec 066 §5) — "Full table", note
/// "450g", price 10.00. Anemic.
/// </summary>
/// <remarks>
/// <see cref="Price"/> is the choice's <em>absolute</em> price, never a delta. The adjustment a
/// customer pays is always derived (Spec 066 §8) as
/// <c>Σ(chosen price) − (effective default price)</c> per group, so repricing a choice or moving a
/// default reprices every future quote with no data rewrite — and produces legitimate negative
/// adjustments when the chosen option costs less than the default.
/// </remarks>
public class OptionChoice : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid OptionGroupId { get; set; }

    /// <summary>Stable identifier within the group, e.g. "full". Immutable after creation.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Display label, e.g. "Full table". Freely editable without a release.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Optional secondary line, e.g. "450g".</summary>
    public string? Note { get; set; }

    /// <summary>Absolute price of this choice in the group's <see cref="OptionGroup.Currency"/>.</summary>
    public decimal Price { get; set; }

    /// <summary>
    /// Marks the group's recommended default — the choice applied when the customer does not
    /// choose, and the baseline every adjustment is measured against. At most one active choice per
    /// active group may carry this, enforced by a filtered unique index rather than by service code
    /// alone (Spec 066 §5), so concurrent default moves serialize in the database.
    /// Display labelling ("Abby's choice", "Recommended") is tenant configuration, not platform.
    /// </summary>
    public bool IsRecommendedDefault { get; set; }

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
