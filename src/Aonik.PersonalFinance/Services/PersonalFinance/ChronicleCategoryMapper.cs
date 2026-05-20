using Aonik.Finance.Services.PersonalFinance;
using Aonik.SharedKernel.Abstractions.Finance.Categorization;

namespace Aonik.PersonalFinance.Services.PersonalFinance;

/// <summary>
/// Adapter that exposes the Chronicle taxonomy slice consumed by Finance
/// (the Plaid map + validation guards) through the SharedKernel
/// <see cref="IChronicleCategoryMapper"/> contract. Wraps the internal
/// <see cref="TransactionCategoryReference"/> reference data so callers
/// outside this module don't need access to internals or the back-pointing
/// project reference Spec 027 Phase 3 removes.
/// </summary>
internal sealed class ChronicleCategoryMapper : IChronicleCategoryMapper
{
    public PlaidCategoryMapping MapPlaidCategory(string? plaidPrimary, string? plaidDetailed)
    {
        var (category, subCategory) = TransactionCategoryReference.MapPlaidCategoryWithSubCategory(
            plaidPrimary,
            plaidDetailed);
        return new PlaidCategoryMapping(category, subCategory);
    }

    public bool IsValidCategory(string? categoryCode)
        => TransactionCategoryReference.IsValidCategory(categoryCode);

    public bool IsValidSubCategory(string? categoryCode, string? subCategoryCode)
        => TransactionCategoryReference.IsValidSubCategory(categoryCode, subCategoryCode);
}
