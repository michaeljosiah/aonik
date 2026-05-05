using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Contracts.Models.Settings;
using Aonik.SharedKernel.Caching;

namespace Aonik.Infrastructure.Caching;

public class CacheManagementService : ICacheManagementService
{
    private readonly CacheSetRegistry _cacheSetRegistry;
    private readonly ICacheInvalidationPublisher _cacheInvalidationPublisher;

    public CacheManagementService(
        CacheSetRegistry cacheSetRegistry,
        ICacheInvalidationPublisher cacheInvalidationPublisher)
    {
        _cacheSetRegistry = cacheSetRegistry;
        _cacheInvalidationPublisher = cacheInvalidationPublisher;
    }

    public Task<CacheOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var cacheSets = _cacheSetRegistry
            .GetCacheSets()
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(name => new CacheSetSummary(name, _cacheSetRegistry.GetKeys(name).Count))
            .ToArray();

        var totalEntries = cacheSets.Sum(cacheSet => cacheSet.EntryCount);
        var response = new CacheOverviewResponse(cacheSets, cacheSets.Length, totalEntries);
        return Task.FromResult(response);
    }

    public async Task<InvalidateCacheSetResponse> InvalidateCacheSetAsync(string cacheSet, CancellationToken cancellationToken = default)
    {
        await _cacheInvalidationPublisher.PublishAsync(new CacheInvalidationEvent(cacheSet), cancellationToken);

        return new InvalidateCacheSetResponse(
            cacheSet,
            Invalidated: true,
            InvalidatedAtUtc: DateTimeOffset.UtcNow);
    }
}
