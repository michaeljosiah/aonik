using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Catalog;

/// <summary>
/// Default ("standard preparation") content for one product (Spec 067 §4). At most one per
/// product. Anemic. Nutrition figures are dedicated nullable decimal columns, not JSON —
/// they are the facts admin validation operates on (plan decision D2); heating steps are
/// display content and stay JSON.
/// </summary>
public class ProductContent : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }

    /// <summary>Which preparation these facts describe: "Light table 225g" (FR-10.2).</summary>
    public string ServingLabel { get; set; } = string.Empty;

    public decimal? Kcal { get; set; }
    public decimal? ProteinGrams { get; set; }
    public decimal? CarbsGrams { get; set; }
    public decimal? FatGrams { get; set; }
    public decimal? FibreGrams { get; set; }
    public decimal? SugarsGrams { get; set; }
    public decimal? SaltGrams { get; set; }

    /// <summary>Full ingredients declaration text; null = not published.</summary>
    public string? Ingredients { get; set; }

    /// <summary>Allergen declaration text; null = not published.</summary>
    public string? Allergens { get; set; }

    /// <summary>Heating/preparation steps: <c>[{ "method": "...", "body": "..." }]</c>.</summary>
    public string HeatingJson { get; set; } = "[]";

    /// <summary>The canonical all-defaults selection this block describes, captured at authoring
    /// time. Resolution cross-checks it against the product's CURRENT all-defaults selection — a
    /// mismatch behaves exactly like <see cref="RequiresReview"/> even if the flag write was
    /// missed, so the binding is self-verifying, not only event-driven (§6).</summary>
    public string DescribesSelectionJson { get; set; } = "{}";

    /// <summary>Set when the product's effective default combination changes after this block was
    /// authored (§6). While true, non-variant resolutions are flagged stale and declarations are
    /// withheld. Cleared by re-upserting the block or explicitly confirming review — both of
    /// which re-capture <see cref="DescribesSelectionJson"/>.</summary>
    public bool RequiresReview { get; set; }

    /// <summary>Monotonic version covering this product's whole content set (default + variants).
    /// Bumped on every content write — the §6 review-flag reaction included — and participates in
    /// the public cache key (§8), so a cached response can never keep serving pre-correction
    /// content as current.</summary>
    public int ContentVersion { get; set; }
}
