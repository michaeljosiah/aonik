using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Catalog;

/// <summary>
/// Authored content for one exact, COMPLETE selection combination of one product (Spec 067 §4).
/// Keyed by the complete canonical selection (Spec 066 §7), so identity is stable when a
/// recommended default later changes. Anemic.
/// </summary>
public class ProductContentVariant : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }

    /// <summary>The complete canonical selection this content describes. Authoring input may be
    /// partial; it is normalised through Spec 066 (omitted groups filled with the then-current
    /// defaults) and stored complete.</summary>
    public string SelectionJson { get; set; } = "{}";

    /// <summary>SHA-256 (hex, lower) of <see cref="SelectionJson"/> — the fixed-length uniqueness
    /// key. A long canonical JSON string cannot participate in a SQL Server nonclustered unique
    /// index (1,700-byte key limit); the JSON itself is never indexed.</summary>
    public string SelectionHash { get; set; } = string.Empty;

    public string ServingLabel { get; set; } = string.Empty;

    public decimal? Kcal { get; set; }
    public decimal? ProteinGrams { get; set; }
    public decimal? CarbsGrams { get; set; }
    public decimal? FatGrams { get; set; }
    public decimal? FibreGrams { get; set; }
    public decimal? SugarsGrams { get; set; }
    public decimal? SaltGrams { get; set; }

    /// <summary>Explicit or withheld — NEVER dynamically inherited. Null = withheld for this
    /// combination (the storefront shows its "not yet published" state); non-null = the authored
    /// declaration for exactly this combination. What is stored is what is served: a later
    /// default-block edit can never silently change what a variant declares — the salmon variant
    /// returning the standard preparation's shellfish line via inheritance is exactly the §2
    /// incident this spec exists to prevent.</summary>
    public string? Ingredients { get; set; }

    /// <summary>Same explicit-or-withheld rule as <see cref="Ingredients"/>.</summary>
    public string? Allergens { get; set; }

    /// <summary>Same rule again: heating is option-dependent content too — a full portion served
    /// the light portion's timings is undercooked food, not a display nit (§4/§5).</summary>
    public string? HeatingJson { get; set; }

    public bool IsActive { get; set; } = true;
}
