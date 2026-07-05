using Aonik.PersonalFinance.Contracts.Models;

namespace Aonik.PersonalFinance.Contracts.Services;

public interface IDashboardService
{
    Task<DashboardResponse> GetDashboardAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the same available-to-spend number the dashboard renders, plus an
    /// itemised list of every upcoming obligation that was deducted. Lets callers
    /// (Simi, agent prompts, future Payabo cards) explain <em>why</em> a number is
    /// what it is, not just what the number is.
    /// </summary>
    Task<SafeToSpendBreakdownResponse> GetSafeToSpendBreakdownAsync(
        CancellationToken cancellationToken = default);
}
