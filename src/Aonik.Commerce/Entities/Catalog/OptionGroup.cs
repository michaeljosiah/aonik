using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Catalog;

/// <summary>
/// A tenant-level axis of product configuration (Spec 066 §5) — "Portion", "Protein", "Grind".
/// Orthogonal to <see cref="ProductVariant"/> (discrete purchasable SKUs) and to
/// <see cref="BundleSlot"/> (box composition): an option group is a selection layered onto whatever
/// is being bought. Anemic; a product narrows this catalogue through <see cref="ProductOptionGroup"/>.
/// </summary>
public class OptionGroup : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>Stable identifier used in selections and content matching, e.g. "portion".
    /// Immutable after creation — it is the contract carts and content variants match on.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Display label, e.g. "Portion". Freely editable without a release.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Optional helper copy shown under the group, e.g. "How much food arrives".</summary>
    public string? HelpText { get; set; }

    /// <summary>One | Multi. See <see cref="OptionSelectionModes"/>. A product may override this
    /// for itself via <see cref="ProductOptionGroup.SelectionModeOverride"/>.</summary>
    public string SelectionMode { get; set; } = OptionSelectionModes.One;

    /// <summary>ISO currency all <see cref="OptionChoice.Price"/> values in this group are
    /// denominated in. Checked against the requested quote currency (rule V10) — option maths never
    /// reinterprets an amount across currencies.</summary>
    public string Currency { get; set; } = "GBP";

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public List<OptionChoice> Choices { get; set; } = new();
}

/// <summary>
/// Known values for <see cref="OptionGroup.SelectionMode"/> and
/// <see cref="ProductOptionGroup.SelectionModeOverride"/> (Spec 066 §5). Validated on write
/// (rule V12) so normalisation can always decide whether a selection is a string or an array.
/// </summary>
public static class OptionSelectionModes
{
    /// <summary>Exactly one choice may be selected; the selection value is a string.</summary>
    public const string One = "One";

    /// <summary>One or more choices may be selected; the selection value is a non-empty array.</summary>
    public const string Multi = "Multi";

    public static bool IsKnown(string? value)
        => value is One or Multi;
}
