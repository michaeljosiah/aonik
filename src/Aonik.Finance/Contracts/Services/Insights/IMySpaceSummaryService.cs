using Aonik.Finance.Contracts.Models.Insights;

namespace Aonik.Finance.Contracts.Services.Insights;

public interface IMySpaceSummaryService
{
    Task<MySpaceSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken = default);
}
