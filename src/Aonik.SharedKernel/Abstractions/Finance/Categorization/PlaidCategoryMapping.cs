namespace Aonik.SharedKernel.Abstractions.Finance.Categorization;

/// <summary>
/// Result of mapping a Plaid <c>(primary, detailed)</c> category pair onto the
/// Chronicle taxonomy. Both fields are nullable so the mapper can signal
/// "no provider data" (both null) distinctly from "primary only mapped to
/// a top-level code without a sub-category".
/// </summary>
public sealed record PlaidCategoryMapping(string? Category, string? SubCategory);
