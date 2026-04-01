using Aonik.Platform.Contracts.Api.Operations;

namespace Aonik.Platform.Contracts.Services.Operations;

public interface IAlertAdminService
{
    Task<AlertListResponse> ListAlertsAsync(int take = 50, CancellationToken cancellationToken = default);

    Task<AlertDetailResponse?> GetAlertAsync(Guid alertId, CancellationToken cancellationToken = default);
}
