namespace Aonik.SharedKernel.Caching;

public interface ICacheInvalidationPublisher
{
    event Func<CacheInvalidationEvent, CancellationToken, Task>? Invalidated;
    Task PublishAsync(CacheInvalidationEvent cacheInvalidationEvent, CancellationToken cancellationToken = default);
}
