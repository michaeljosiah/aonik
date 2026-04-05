using Aonik.Platform.Contracts.Api.Jobs;
using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Contracts.Services.Notifications;
using Aonik.Platform.Contracts.Services.Operations;
using Aonik.Platform.Notifications;
using Aonik.Platform.Entities.Operations;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Platform.Services.Operations;

internal class ScheduledJobAdminService : IScheduledJobAdminService
{
    private readonly PlatformDbContext _dbContext;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ITenantProvider _tenantProvider;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ScheduledJobAdminService> _logger;

    public ScheduledJobAdminService(
        PlatformDbContext dbContext,
        ICurrentUserProvider currentUserProvider,
        ITenantProvider tenantProvider,
        INotificationService notificationService,
        ILogger<ScheduledJobAdminService> logger)
    {
        _dbContext = dbContext;
        _currentUserProvider = currentUserProvider;
        _tenantProvider = tenantProvider;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<ScheduledJobListResponse> ListScheduledJobsAsync(CancellationToken cancellationToken = default)
    {
        var jobs = await _dbContext.ScheduledJobProjections
            .AsNoTracking()
            .Where(x => x.State != ScheduledJobStates.Removed)
            .OrderBy(x => x.DisplayName)
            .ThenBy(x => x.JobName)
            .Select(x => new ScheduledJobSummary(
                x.JobName,
                x.GroupName,
                x.Description,
                x.CronExpression,
                x.State,
                x.NextFireTimeUtc,
                x.PreviousFireTimeUtc,
                x.DisplayName,
                x.LastOutcome,
                x.LastOutcomeSummary,
                x.LastDurationMs))
            .ToListAsync(cancellationToken);

        return new ScheduledJobListResponse(jobs);
    }

    public async Task<ScheduledJobDetailResponse?> GetJobDetailAsync(
        string jobName, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ScheduledJobProjections
            .AsNoTracking()
            .Where(x => x.GroupName == ScheduledJobGroups.ScheduledJobs && x.JobName == jobName)
            .Select(x => new ScheduledJobDetailResponse(
                x.JobName,
                x.GroupName,
                x.DisplayName,
                x.Description,
                x.CronExpression,
                x.TimeZoneId,
                x.State,
                x.NextFireTimeUtc,
                x.PreviousFireTimeUtc,
                x.LastOutcome,
                x.LastOutcomeSummary,
                x.LastDurationMs,
                x.LastSyncedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PagedResult<ScheduledJobRunSummary>> ListJobRunsAsync(
        string jobName, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _dbContext.ScheduledJobRuns
            .AsNoTracking()
            .Where(x => x.GroupName == ScheduledJobGroups.ScheduledJobs && x.JobName == jobName);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.FiredAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ScheduledJobRunSummary(
                x.Id,
                x.Outcome,
                x.ErrorMessage,
                x.DurationMs,
                x.TriggeredBy,
                x.FiredAtUtc,
                x.CompletedAtUtc,
                x.FireInstanceId))
            .ToListAsync(cancellationToken);

        return new PagedResult<ScheduledJobRunSummary>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<PagedResult<ScheduledJobCommandSummary>> ListJobCommandsAsync(
        string jobName, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _dbContext.ScheduledJobAdminCommands
            .AsNoTracking()
            .Where(x => x.GroupName == ScheduledJobGroups.ScheduledJobs && x.JobName == jobName);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ScheduledJobCommandSummary(
                x.Id,
                x.CommandType,
                x.Status,
                x.ResultMessage,
                x.RequestedByUserId,
                x.CreatedAt,
                x.ProcessedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<ScheduledJobCommandSummary>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<SchedulerHealthResponse?> GetSchedulerHealthAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.SchedulerHealthSnapshots
            .AsNoTracking()
            .OrderByDescending(x => x.RecordedAtUtc)
            .Select(x => new SchedulerHealthResponse(
                x.SchedulerName,
                x.SchedulerInstanceId,
                x.IsStarted,
                x.InStandbyMode,
                x.ThreadPoolSize,
                x.ActiveJobCount,
                x.TotalJobCount,
                x.TotalTriggerCount,
                x.RecordedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<ScheduledJobActionResponse?> QueuePauseAsync(string jobName, CancellationToken cancellationToken = default)
    {
        return QueueCommandAsync(jobName, ScheduledJobCommandTypes.Pause, cancellationToken);
    }

    public Task<ScheduledJobActionResponse?> QueueResumeAsync(string jobName, CancellationToken cancellationToken = default)
    {
        return QueueCommandAsync(jobName, ScheduledJobCommandTypes.Resume, cancellationToken);
    }

    public Task<ScheduledJobActionResponse?> QueueTriggerAsync(string jobName, CancellationToken cancellationToken = default)
    {
        return QueueCommandAsync(jobName, ScheduledJobCommandTypes.Trigger, cancellationToken);
    }

    private async Task<ScheduledJobActionResponse?> QueueCommandAsync(
        string jobName,
        string commandType,
        CancellationToken cancellationToken)
    {
        var projection = await _dbContext.ScheduledJobProjections
            .FirstOrDefaultAsync(
                x => x.GroupName == ScheduledJobGroups.ScheduledJobs && x.JobName == jobName,
                cancellationToken);

        if (projection is null)
        {
            return null;
        }

        var command = new ScheduledJobAdminCommand
        {
            TenantId = Guid.Empty,
            JobName = projection.JobName,
            GroupName = projection.GroupName,
            CommandType = commandType,
            PayloadJson = "{}",
            RequestedByUserId = _currentUserProvider.GetCurrentUserId(),
            Status = ScheduledJobCommandStatuses.Pending,
        };

        _dbContext.ScheduledJobAdminCommands.Add(command);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await TryNotifyCommandQueuedAsync(projection, command, commandType, cancellationToken);

        return new ScheduledJobActionResponse(
            projection.JobName,
            commandType.ToLowerInvariant(),
            true,
            $"{commandType} command queued successfully.",
            command.Id,
            command.Status);
    }

    private async Task TryNotifyCommandQueuedAsync(
        ScheduledJobProjection projection,
        ScheduledJobAdminCommand command,
        string commandType,
        CancellationToken cancellationToken)
    {
        if (!_tenantProvider.TryGetCurrentTenantId(out var tenantId)
            || !_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            return;
        }

        try
        {
            var lowerCommandType = commandType.ToLowerInvariant();
            await _notificationService.CreateForUserAsync(
                new CreateNotificationRequest(
                    tenantId,
                    userId,
                    Type: "ScheduledJobCommandQueued",
                    Source: "Scheduler",
                    Title: $"{projection.DisplayName} {lowerCommandType} queued",
                    Body: $"{commandType} command queued successfully for {projection.DisplayName}.",
                    Severity: NotificationSeverities.Info,
                    ActionUrl: "/settings/background-jobs",
                    CorrelationId: command.Id.ToString(),
                    AiRunId: null,
                    MetadataJson: $"{{\"jobName\":\"{projection.JobName}\",\"groupName\":\"{projection.GroupName}\",\"commandId\":\"{command.Id}\",\"commandType\":\"{commandType}\"}}"),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create notification for scheduled job command {CommandId}", command.Id);
        }
    }
}
