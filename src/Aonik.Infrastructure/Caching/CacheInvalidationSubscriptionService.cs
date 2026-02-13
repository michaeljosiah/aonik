using Microsoft.Extensions.Hosting;

namespace Aonik.Infrastructure.Caching;

public class CacheInvalidationSubscriptionService : IHostedService
{
    private readonly FusionCacheInvalidationHandler _handler;

    public CacheInvalidationSubscriptionService(FusionCacheInvalidationHandler handler)
    {
        _handler = handler;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = _handler;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
