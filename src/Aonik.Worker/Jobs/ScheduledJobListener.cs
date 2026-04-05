using Aonik.Platform.Entities.Operations;
using Aonik.Platform.Entities.Compliance;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using System.Text.Json;

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

        var executionResult = context.Result is ScheduledJobExecutionResult result
            ? result
            : (ScheduledJobExecutionResult?)null;
        var outcome = jobException is null
            ? executionResult?.Outcome ?? ScheduledJobRunOutcomes.Succeeded
            : ScheduledJobRunOutcomes.Failed;
        var durationMs = (int)Math.Round(context.JobRunTime.TotalMilliseconds);
        var resultSummary = executionResult?.Summary ?? context.Result as string;
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
            var run = new ScheduledJobRun
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
            };

            dbContext.ScheduledJobRuns.Add(run);

            dbContext.AuditLogs.Add(CreateRunAuditLog(run, outcome, resultSummary, errorMessage));

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record scheduled job run for {JobKey}.", context.JobDetail.Key);
        }
    }

    private static AuditLog CreateRunAuditLog(
        ScheduledJobRun run,
        string outcome,
        string? resultSummary,
        string? errorMessage)
    {
        return new AuditLog
        {
            TenantId = Guid.Empty,
            Timestamp = run.FiredAtUtc,
            ActorType = run.TriggeredBy == ScheduledJobTriggeredBy.AdminTrigger ? "User" : "System",
            ActorId = Guid.Empty,
            Action = outcome == ScheduledJobRunOutcomes.Succeeded
                ? AuditEventNames.ScheduledJobRunSucceeded
                : AuditEventNames.ScheduledJobRunFailed,
            ResourceType = nameof(ScheduledJobRun),
            ResourceId = run.Id,
            CorrelationId = run.FireInstanceId ?? string.Empty,
            DetailsJson = JsonSerializer.Serialize(new
            {
                jobName = run.JobName,
                groupName = run.GroupName,
                outcome,
                durationMs = run.DurationMs,
                triggeredBy = run.TriggeredBy,
                firedAtUtc = run.FiredAtUtc,
                completedAtUtc = run.CompletedAtUtc,
                fireInstanceId = run.FireInstanceId,
                resultSummary,
                errorMessage
            }),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = null
        };
    }
}
