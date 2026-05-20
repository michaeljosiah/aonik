namespace Aonik.SharedKernel.Abstractions.Finance.Categorization;

/// <summary>
/// Cross-module entry point into the Chronicle taxonomy. PersonalFinance
/// implements this against its <c>TransactionCategoryReference</c>; Finance
/// (and any other module) consumes it via SharedKernel so neither module
/// needs a direct project reference on the other. Resolves
/// <a href="../../docs/specifications/028.finance-account-transaction-auto-categorization.html">Spec 028</a>
/// §15 Open · O-1 in favour of the "promote slice to SharedKernel" path.
///
/// All methods are pure functions over static reference data — the
/// implementation can safely be registered as a singleton.
/// </summary>
public interface IChronicleCategoryMapper
{
    /// <summary>
    /// Maps a Plaid <c>personal_finance_category.{primary,detailed}</c> pair onto a
    /// Chronicle category and optional sub-category. Behaviour:
    /// <list type="bullet">
    ///   <item>Both inputs null/empty &#8594; <c>(null, null)</c>.</item>
    ///   <item>Detailed key matches the detailed map &#8594; that entry's <c>(category, sub)</c>.</item>
    ///   <item>Detailed key absent but primary recognised &#8594; <c>(canonical, null)</c>.</item>
    ///   <item>Primary unrecognised &#8594; <c>("other", null)</c>.</item>
    /// </list>
    /// </summary>
    PlaidCategoryMapping MapPlaidCategory(string? plaidPrimary, string? plaidDetailed);

    /// <summary>
    /// Returns true if the given code is a recognised Chronicle top-level category.
    /// </summary>
    bool IsValidCategory(string? categoryCode);

    /// <summary>
    /// Returns true if the given sub-category code is valid for the specified
    /// parent category.
    /// </summary>
    bool IsValidSubCategory(string? categoryCode, string? subCategoryCode);
}
