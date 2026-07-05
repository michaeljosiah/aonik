using Aonik.PersonalFinance.Contracts.Models;

namespace Aonik.PersonalFinance.Contracts.Services;

/// <summary>
/// Per-currency rollups over the current user's payment logs (Spec 045 §7) —
/// the Today hero ("£4,250 · 6 people &amp; places" + "+ ₦1.2m recorded in
/// naira"). Grouped by currency; never a converted grand total.
/// </summary>
public interface IPaymentLogSummaryService
{
    Task<YearSummary> GetYearSummaryAsync(int year, CancellationToken cancellationToken = default);
}
