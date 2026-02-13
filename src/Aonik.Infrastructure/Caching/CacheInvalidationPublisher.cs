namespace Aonik.Infrastructure.Caching;

public interface ICacheInvalidationPublisher
{
    event Func<CacheInvalidationEvent, CancellationToken, Task>? Invalidated;
    Task PublishAsync(CacheInvalidationEvent cacheInvalidationEvent, CancellationToken cancellationToken = default);
}

public class CacheInvalidationPublisher : ICacheInvalidationPublisher
{
    public event Func<CacheInvalidationEvent, CancellationToken, Task>? Invalidated;

    public async Task PublishAsync(CacheInvalidationEvent cacheInvalidationEvent, CancellationToken cancellationToken = default)
    {
        var handlers = Invalidated;
        if (handlers is null)
        {
            return;
        }

        var invocationList = handlers.GetInvocationList();
        foreach (var handler in invocationList)
        {
            if (handler is Func<CacheInvalidationEvent, CancellationToken, Task> typedHandler)
            {
                await typedHandler(cacheInvalidationEvent, cancellationToken);
            }
        }
    }
}
