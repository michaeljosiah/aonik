using Aonik.Platform.Entities.Operations;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace Aonik.Worker.Jobs;

internal sealed record ScheduledJobExecutionSnapshot(
    string Outcome,
    string? Summary,
    int DurationMs);

internal sealed class ScheduledJobProjectionSynchronizer
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IReadOnlyDictionary<string, IScheduledJobDefinition> _definitionsByName;
    private readonly ILogger<ScheduledJobProjectionSynchronizer> _logger;

    public ScheduledJobProjectionSynchronizer(
        IServiceScopeFactory serviceScopeFactory,
        ISchedulerFactory schedulerFactory,
        IEnumerable<IScheduledJobDefinition> definitions,
        ILogger<ScheduledJobProjectionSynchronizer> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _schedulerFactory = schedulerFactory;
        _logger = logger;
        _definitionsByName = definitions.ToDictionary(x => x.JobKey.Name, StringComparer.OrdinalIgnoreCase);
    }

    public async Task SyncAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var definition in _definitionsByName.Values)
        {
            await SyncDefinitionAsync(definition, null, cancellationToken);
        }
    }

    public async Task SyncJobAsync(
        JobKey jobKey,
        ScheduledJobExecutionSnapshot? execution = null,
        CancellationToken cancellationToken = default)
    {
        if (!_definitionsByName.TryGetValue(jobKey.Name, out var definition))
        {
            _logger.LogDebug("Skipping projection sync for unknown scheduled job {JobKey}.", jobKey);
            return;
        }

        await SyncDefinitionAsync(definition, execution, cancellationToken);
    }

    private async Task SyncDefinitionAsync(
        IScheduledJobDefinition definition,
        ScheduledJobExecutionSnapshot? execution,
        CancellationToken cancellationToken)
    {
        try
        {
            var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);

            using var scope = _serviceScopeFactory.CreateScope();
            SetSystemTenant(scope.ServiceProvider);

            var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

            var projection = await dbContext.ScheduledJobProjections
                .FirstOrDefaultAsync(
                    x => x.GroupName == definition.JobKey.Group && x.JobName == definition.JobKey.Name,
                    cancellationToken);

            if (projection is null)
            {
                projection = new ScheduledJobProjection
                {
                    TenantId = Guid.Empty,
                    JobName = definition.JobKey.Name,
                    GroupName = definition.JobKey.Group,
                };

                dbContext.ScheduledJobProjections.Add(projection);
            }

            projection.DisplayName = definition.DisplayName;
            projection.Description = definition.Description;
            projection.CronExpression = definition.CronExpression;
            projection.TimeZoneId = definition.TimeZoneId;
            projection.LastSyncedAtUtc = DateTime.UtcNow;

            if (execution is not null)
            {
                projection.LastOutcome = execution.Outcome;
                projection.LastOutcomeSummary = execution.Summary;
                projection.LastDurationMs = execution.DurationMs;
            }

            if (!definition.Enabled)
            {
                projection.State = ScheduledJobStates.Disabled;
                projection.NextFireTimeUtc = null;
                projection.PreviousFireTimeUtc = null;
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            if (!await scheduler.CheckExists(definition.JobKey, cancellationToken))
            {
                projection.State = ScheduledJobStates.Missing;
                projection.NextFireTimeUtc = null;
                projection.PreviousFireTimeUtc = null;
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            var trigger = (await scheduler.GetTriggersOfJob(definition.JobKey, cancellationToken))
                .OrderBy(x => x.GetNextFireTimeUtc() ?? DateTimeOffset.MaxValue)
                .FirstOrDefault();

            if (trigger is null)
            {
                projection.State = ScheduledJobStates.Missing;
                projection.NextFireTimeUtc = null;
                projection.PreviousFireTimeUtc = null;
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            projection.State = MapTriggerState(await scheduler.GetTriggerState(trigger.Key, cancellationToken));
            projection.NextFireTimeUtc = trigger.GetNextFireTimeUtc()?.UtcDateTime;
            projection.PreviousFireTimeUtc = trigger.GetPreviousFireTimeUtc()?.UtcDateTime;

            if (trigger is ICronTrigger cronTrigger)
            {
                projection.CronExpression = cronTrigger.CronExpressionString ?? definition.CronExpression;
                projection.TimeZoneId = cronTrigger.TimeZone.Id;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to sync scheduled job projection for {JobName}.", definition.JobKey.Name);
        }
    }

    private static void SetSystemTenant(IServiceProvider serviceProvider)
    {
        var tenantContext = serviceProvider.GetRequiredService<ITenantContext>();
        tenantContext.TenantId = Guid.Empty;
        tenantContext.ResolutionSource = "system";
    }

    private static string MapTriggerState(TriggerState triggerState)
    {
        return triggerState switch
        {
            TriggerState.Paused => ScheduledJobStates.Paused,
            TriggerState.Blocked => ScheduledJobStates.Blocked,
            TriggerState.Error => ScheduledJobStates.Error,
            TriggerState.Complete => ScheduledJobStates.Complete,
            TriggerState.None => ScheduledJobStates.Missing,
            _ => ScheduledJobStates.Active,
        };
    }
}
