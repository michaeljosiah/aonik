using Aonik.Platform.Entities.Operations;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aonik.Worker.Jobs;

/// <summary>
/// Quartz job listener that refreshes the scheduled job projection with the
/// latest execution outcome after each run and records a durable run history entry.
/// </summary>
internal sealed class ScheduledJobListener : IJobListener
{
    private readonly ScheduledJobProjectionSynchronizer _projectionSynchronizer;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<ScheduledJobListener> _logger;

    public string Name => "ScheduledJobListener";

    public ScheduledJobListener(
        ScheduledJobProjectionSynchronizer projectionSynchronizer,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ScheduledJobListener> logger)
    {
        _projectionSynchronizer = projectionSynchronizer;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    public Task JobToBeExecuted(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task JobExecutionVetoed(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task JobWasExecuted(
        IJobExecutionContext context,
        JobExecutionException? jobException,
        CancellationToken cancellationToken = default)
    {
        if (context.JobDetail.Key.Group != ScheduledJobGroups.ScheduledJobs)
        {
            return;
        }

        var outcome = jobException is null
            ? ScheduledJobRunOutcomes.Succeeded
            : ScheduledJobRunOutcomes.Failed;
        var durationMs = (int)Math.Round(context.JobRunTime.TotalMilliseconds);
        var resultSummary = context.Result as string;
        var errorMessage = jobException?.Message;

        try
        {
            var snapshot = new ScheduledJobExecutionSnapshot(outcome, resultSummary ?? errorMessage, durationMs);
            await _projectionSynchronizer.SyncJobAsync(context.JobDetail.Key, snapshot, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to sync scheduled job projection for {JobKey} after execution.", context.JobDetail.Key);
        }

        try
        {
            var triggeredBy = context.MergedJobDataMap.GetString("TriggeredBy")
                ?? ScheduledJobTriggeredBy.Schedule;

            using var scope = _serviceScopeFactory.CreateScope();
            var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
            tenantContext.TenantId = Guid.Empty;
            tenantContext.ResolutionSource = "system";

            var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            dbContext.ScheduledJobRuns.Add(new ScheduledJobRun
            {
                TenantId = Guid.Empty,
                JobName = context.JobDetail.Key.Name,
                GroupName = context.JobDetail.Key.Group,
                Outcome = outcome,
                ErrorMessage = errorMessage,
                DurationMs = durationMs,
                TriggeredBy = triggeredBy,
                FiredAtUtc = context.FireTimeUtc.UtcDateTime,
                CompletedAtUtc = DateTime.UtcNow,
                FireInstanceId = context.FireInstanceId,
            });

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record scheduled job run for {JobKey}.", context.JobDetail.Key);
        }
    }
}
