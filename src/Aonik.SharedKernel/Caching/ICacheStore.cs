namespace Aonik.SharedKernel.Caching;

public interface ICacheStore
{
    Task<T?> GetOrSetAsync<T>(
        string key,
        CachePolicy policy,
        Func<CancellationToken, Task<T?>> factory,
        string cacheSet,
        CancellationToken cancellationToken = default);
}
