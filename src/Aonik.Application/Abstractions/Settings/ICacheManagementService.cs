using Aonik.Application.Models.Settings;

namespace Aonik.Application.Abstractions.Settings;

public interface ICacheManagementService
{
    Task<CacheOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken = default);
    Task<InvalidateCacheSetResponse> InvalidateCacheSetAsync(string cacheSet, CancellationToken cancellationToken = default);
}
