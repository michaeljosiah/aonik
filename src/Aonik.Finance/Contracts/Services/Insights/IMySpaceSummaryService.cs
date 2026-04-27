using Aonik.Finance.Contracts.Models.Insights;

namespace Aonik.Finance.Contracts.Services.Insights;

public interface IMySpaceSummaryService
{
    /// <summary>
    /// Returns the dashboard summary for the current tenant. The cash timeline
    /// is built in <paramref name="currencyOverride"/> when supplied (and the
    /// requested code is in the tenant's configured currency set); otherwise
    /// the tenant's primary settlement currency is used.
    /// </summary>
    Task<MySpaceSummaryResponse> GetSummaryAsync(
        string? currencyOverride = null,
        CancellationToken cancellationToken = default);
}
