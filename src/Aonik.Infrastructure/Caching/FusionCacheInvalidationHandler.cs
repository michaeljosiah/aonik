using ZiggyCreatures.Caching.Fusion;
using Aonik.SharedKernel.Caching;

namespace Aonik.Infrastructure.Caching;

public class FusionCacheInvalidationHandler
{
    private readonly IFusionCache _cache;
    private readonly ICacheSetRegistry _cacheSetRegistry;

    public FusionCacheInvalidationHandler(
        ICacheInvalidationPublisher publisher,
        IFusionCache cache,
        ICacheSetRegistry cacheSetRegistry)
    {
        _cache = cache;
        _cacheSetRegistry = cacheSetRegistry;
        publisher.Invalidated += HandleInvalidationAsync;
    }

    private Task HandleInvalidationAsync(CacheInvalidationEvent cacheInvalidationEvent, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(cacheInvalidationEvent.CacheKey))
        {
            _cache.Remove(cacheInvalidationEvent.CacheKey);
            _cacheSetRegistry.RemoveKey(cacheInvalidationEvent.CacheSet, cacheInvalidationEvent.CacheKey);
            return Task.CompletedTask;
        }

        var keys = _cacheSetRegistry.GetKeys(cacheInvalidationEvent.CacheSet);
        foreach (var key in keys)
        {
            _cache.Remove(key);
            _cacheSetRegistry.RemoveKey(cacheInvalidationEvent.CacheSet, key);
        }

        return Task.CompletedTask;
    }
}
