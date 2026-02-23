using Aonik.Platform.Contracts.Models.Settings;

namespace Aonik.Platform.Contracts.Services.Settings;

public interface ICacheManagementService
{
    Task<CacheOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken = default);
    Task<InvalidateCacheSetResponse> InvalidateCacheSetAsync(string cacheSet, CancellationToken cancellationToken = default);
}
