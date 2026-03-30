using Aonik.Platform.Entities.Operations;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Quartz.Impl.Matchers;

namespace Aonik.Worker.Jobs;

/// <summary>
/// Periodically publishes a scheduler health snapshot so the Admin UI
/// can display scheduler state without direct Quartz access.
/// </summary>
internal sealed class SchedulerHealthPublisher : BackgroundService
{
    private static readonly TimeSpan PublishInterval = TimeSpan.FromSeconds(60);

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ILogger<SchedulerHealthPublisher> _logger;

    public SchedulerHealthPublisher(
        IServiceScopeFactory serviceScopeFactory,
        ISchedulerFactory schedulerFactory,
        ILogger<SchedulerHealthPublisher> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _schedulerFactory = schedulerFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishSnapshotAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish scheduler health snapshot.");
            }

            await Task.Delay(PublishInterval, stoppingToken);
        }
    }

    private async Task PublishSnapshotAsync(CancellationToken cancellationToken)
    {
        var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
        var metadata = await scheduler.GetMetaData(cancellationToken);

        var jobKeys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup(), cancellationToken);
        var triggerKeys = await scheduler.GetTriggerKeys(GroupMatcher<TriggerKey>.AnyGroup(), cancellationToken);
        var executingJobs = await scheduler.GetCurrentlyExecutingJobs(cancellationToken);

        using var scope = _serviceScopeFactory.CreateScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.TenantId = Guid.Empty;
        tenantContext.ResolutionSource = "system";

        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var schedulerName = metadata.SchedulerName;
        var instanceId = metadata.SchedulerInstanceId;

        var snapshot = await dbContext.SchedulerHealthSnapshots
            .FirstOrDefaultAsync(
                x => x.SchedulerName == schedulerName && x.SchedulerInstanceId == instanceId,
                cancellationToken);

        if (snapshot is null)
        {
            snapshot = new SchedulerHealthSnapshot
            {
                TenantId = Guid.Empty,
                SchedulerName = schedulerName,
                SchedulerInstanceId = instanceId,
            };

            dbContext.SchedulerHealthSnapshots.Add(snapshot);
        }

        snapshot.IsStarted = metadata.Started;
        snapshot.InStandbyMode = metadata.InStandbyMode;
        snapshot.ThreadPoolSize = metadata.ThreadPoolSize;
        snapshot.ActiveJobCount = executingJobs.Count;
        snapshot.TotalJobCount = jobKeys.Count;
        snapshot.TotalTriggerCount = triggerKeys.Count;
        snapshot.RecordedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
