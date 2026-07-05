using Aonik.SharedKernel.Primitives;

namespace Aonik.PersonalFinance.Entities;

/// <summary>
/// Canonical reference table for personal transaction categories.
/// Provides a standardized taxonomy that provider-specific categories (e.g. Plaid)
/// are mapped into, ensuring consistent categorisation across all transaction sources.
/// </summary>
public class TransactionCategory : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? GroupName { get; set; }
    public string? IconName { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
