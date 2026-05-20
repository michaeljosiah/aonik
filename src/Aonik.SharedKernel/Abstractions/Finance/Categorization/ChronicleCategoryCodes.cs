namespace Aonik.SharedKernel.Abstractions.Finance.Categorization;

/// <summary>
/// Canonical Chronicle category codes that cross module boundaries.
/// PersonalFinance owns the full taxonomy (display metadata, sub-category
/// dictionary, icons) — only the catch-all codes referenced by Finance
/// services live here so the categorization slice of <c>TransactionCategoryReference</c>
/// stays consumable without a back-reference from <c>Aonik.Finance</c> on
/// <c>Aonik.PersonalFinance</c> once Spec 027 Phase 3 drops the transitional
/// project reference.
/// </summary>
public static class ChronicleCategoryCodes
{
    /// <summary>Catch-all category for transactions a Plaid primary mapped to
    /// but no specific Chronicle code is appropriate (e.g. <c>GENERAL_SERVICES</c>
    /// without a usable detailed value).</summary>
    public const string Other = "other";

    /// <summary>Used when no provider data is available at all — the row needs
    /// human review.</summary>
    public const string Uncategorized = "uncategorized";
}
