namespace Aonik.SharedKernel.Abstractions.Ai;

/// <summary>
/// Cross-module read interface that exposes lightweight aggregates over the
/// Ai module's <c>AiRun</c> stream. Implemented by the Ai module and consumed
/// by dashboard/insight surfaces in Finance.
/// </summary>
public interface IAiRunStatsService
{
    /// <summary>
    /// Returns the count of AI runs created today (UTC) for the current tenant.
    /// All runs are counted regardless of <c>Outcome</c> — the dashboard's
    /// "agent ops today" tile reads as "ops attempted", not "ops succeeded".
    /// </summary>
    Task<int> CountForTodayAsync(CancellationToken cancellationToken = default);
}
