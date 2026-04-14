using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl.Matchers;

namespace Aonik.Worker.Jobs;

/// <summary>
/// Hosted service that, at startup:
///   1. Removes any Quartz jobs persisted in the store whose .NET type can no longer
///      be resolved (e.g. a job class was renamed or deleted but the trigger row
///      still lives in QRTZ_JOB_DETAILS). Without this guard the cluster check-in
///      thread throws TypeLoadException on every cycle — the dev environment was
///      logging ~1.4k of these per day for the removed BehaviouralInsightJob.
///   2. Syncs scheduled job projections.
/// </summary>
internal sealed class ScheduledJobRegistrar : IHostedService
{
    private readonly ScheduledJobProjectionSynchronizer _projectionSynchronizer;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ILogger<ScheduledJobRegistrar> _logger;

    public ScheduledJobRegistrar(
        ScheduledJobProjectionSynchronizer projectionSynchronizer,
        ISchedulerFactory schedulerFactory,
        ILogger<ScheduledJobRegistrar> logger)
    {
        _projectionSynchronizer = projectionSynchronizer;
        _schedulerFactory = schedulerFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await PruneUnresolvableJobsAsync(cancellationToken);

        try
        {
            await _projectionSynchronizer.SyncAllAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to sync scheduled job projections at startup.");
        }
    }

    private async Task PruneUnresolvableJobsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
            var jobKeys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup(), cancellationToken);

            foreach (var jobKey in jobKeys)
            {
                try
                {
                    // Forces Quartz to load the job's .NET type from the persisted store.
                    // Throws JobPersistenceException (wrapping TypeLoadException) for stale entries.
                    _ = await scheduler.GetJobDetail(jobKey, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Quartz job {JobKey} could not be loaded — its .NET type is no longer resolvable. Deleting stale persisted entry.",
                        jobKey);

                    try
                    {
                        await scheduler.DeleteJob(jobKey, cancellationToken);
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.LogError(deleteEx, "Failed to delete stale Quartz job {JobKey}.", jobKey);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to scan Quartz scheduler for unresolvable jobs at startup.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
