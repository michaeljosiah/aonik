using Microsoft.Extensions.Logging;

namespace Aonik.Worker.Jobs;

/// <summary>
/// Hosted service that syncs scheduled job projections once at startup.
/// </summary>
internal sealed class ScheduledJobRegistrar : IHostedService
{
    private readonly ScheduledJobProjectionSynchronizer _projectionSynchronizer;
    private readonly ILogger<ScheduledJobRegistrar> _logger;

    public ScheduledJobRegistrar(
        ScheduledJobProjectionSynchronizer projectionSynchronizer,
        ILogger<ScheduledJobRegistrar> logger)
    {
        _projectionSynchronizer = projectionSynchronizer;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _projectionSynchronizer.SyncAllAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to sync scheduled job projections at startup.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
