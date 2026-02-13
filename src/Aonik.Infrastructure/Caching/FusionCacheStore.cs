using ZiggyCreatures.Caching.Fusion;

namespace Aonik.Infrastructure.Caching;

public interface ICacheStore
{
    Task<T?> GetOrSetAsync<T>(
        string key,
        CachePolicy policy,
        Func<CancellationToken, Task<T?>> factory,
        string cacheSet,
        CancellationToken cancellationToken = default);
}

public class FusionCacheStore : ICacheStore
{
    private readonly IFusionCache _cache;
    private readonly ICachePolicyProvider _cachePolicyProvider;
    private readonly ICacheSetRegistry _cacheSetRegistry;

    public FusionCacheStore(
        IFusionCache cache,
        ICachePolicyProvider cachePolicyProvider,
        ICacheSetRegistry cacheSetRegistry)
    {
        _cache = cache;
        _cachePolicyProvider = cachePolicyProvider;
        _cacheSetRegistry = cacheSetRegistry;
    }

    public async Task<T?> GetOrSetAsync<T>(
        string key,
        CachePolicy policy,
        Func<CancellationToken, Task<T?>> factory,
        string cacheSet,
        CancellationToken cancellationToken = default)
    {
        var options = _cachePolicyProvider.Get(policy);

        var value = await _cache.GetOrSetAsync<T?>(
            key,
            async ct => await factory(ct),
            options,
            cancellationToken);

        _cacheSetRegistry.Track(cacheSet, key);
        return value;
    }
}
