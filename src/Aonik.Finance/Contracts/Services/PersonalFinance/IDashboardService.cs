using Aonik.Finance.Contracts.Models.PersonalFinance;

namespace Aonik.Finance.Contracts.Services.PersonalFinance;

public interface IDashboardService
{
    Task<DashboardResponse> GetDashboardAsync(
        CancellationToken cancellationToken = default);
}
