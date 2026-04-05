using Aonik.Platform.Entities.Operations;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Platform.Entities.Compliance;
using Aonik.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Quartz;
using System.Text.Json;

namespace Aonik.Worker.Jobs;

internal sealed class ScheduledJobCommandProcessor : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ScheduledJobProjectionSynchronizer _projectionSynchronizer;
    private readonly ILogger<ScheduledJobCommandProcessor> _logger;

    public ScheduledJobCommandProcessor(
        IServiceScopeFactory serviceScopeFactory,
        ISchedulerFactory schedulerFactory,
        ScheduledJobProjectionSynchronizer projectionSynchronizer,
        ILogger<ScheduledJobCommandProcessor> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _schedulerFactory = schedulerFactory;
        _projectionSynchronizer = projectionSynchronizer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var processedAny = await ProcessNextCommandAsync(stoppingToken);
            if (!processedAny)
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
        }
    }

    private async Task<bool> ProcessNextCommandAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        SetSystemTenant(scope.ServiceProvider);

        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var command = await dbContext.ScheduledJobAdminCommands
            .OrderBy(x => x.CreatedAt)
            .FirstOrDefaultAsync(x => x.Status == ScheduledJobCommandStatuses.Pending, cancellationToken);

        if (command is null)
        {
            return false;
        }

        command.Status = ScheduledJobCommandStatuses.Processing;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another replica already claimed this command — skip it.
            _logger.LogDebug("Command {CommandId} was already claimed by another instance.", command.Id);
            return false;
        }

        var jobKey = new JobKey(command.JobName, command.GroupName);

        try
        {
            var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
            if (!await scheduler.CheckExists(jobKey, cancellationToken))
            {
                throw new InvalidOperationException($"Scheduled job {jobKey} was not found in Quartz.");
            }

            switch (command.CommandType)
            {
                case ScheduledJobCommandTypes.Pause:
                    await scheduler.PauseJob(jobKey, cancellationToken);
                    command.ResultMessage = "Job paused successfully.";
                    break;
                case ScheduledJobCommandTypes.Resume:
                    await scheduler.ResumeJob(jobKey, cancellationToken);
                    command.ResultMessage = "Job resumed successfully.";
                    break;
                case ScheduledJobCommandTypes.Trigger:
                    await scheduler.TriggerJob(jobKey,
                        new JobDataMap { { "TriggeredBy", ScheduledJobTriggeredBy.AdminTrigger } },
                        cancellationToken);
                    command.ResultMessage = "Job triggered successfully.";
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported scheduled job command type {command.CommandType}.");
            }

            command.Status = ScheduledJobCommandStatuses.Succeeded;
            command.ProcessedAtUtc = DateTime.UtcNow;
            dbContext.AuditLogs.Add(CreateCommandAuditLog(command, AuditEventNames.ScheduledJobCommandSucceeded, null));
            await dbContext.SaveChangesAsync(cancellationToken);

            await _projectionSynchronizer.SyncJobAsync(jobKey, cancellationToken: cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Scheduled job command {CommandId} failed.", command.Id);
            command.Status = ScheduledJobCommandStatuses.Failed;
            command.ResultMessage = ex.Message;
            command.ProcessedAtUtc = DateTime.UtcNow;
            dbContext.AuditLogs.Add(CreateCommandAuditLog(command, AuditEventNames.ScheduledJobCommandFailed, ex.Message));
            await dbContext.SaveChangesAsync(cancellationToken);

            await _projectionSynchronizer.SyncJobAsync(jobKey, cancellationToken: cancellationToken);
            return true;
        }
    }

    private static void SetSystemTenant(IServiceProvider serviceProvider)
    {
        var tenantContext = serviceProvider.GetRequiredService<ITenantContext>();
        tenantContext.TenantId = Guid.Empty;
        tenantContext.ResolutionSource = "system";
    }

    private static AuditLog CreateCommandAuditLog(
        ScheduledJobAdminCommand command,
        string action,
        string? errorMessage)
    {
        var timestamp = command.ProcessedAtUtc ?? DateTime.UtcNow;

        return new AuditLog
        {
            TenantId = command.TenantId,
            Timestamp = timestamp,
            ActorType = command.RequestedByUserId.HasValue ? "User" : "System",
            ActorId = command.RequestedByUserId ?? Guid.Empty,
            Action = action,
            ResourceType = nameof(ScheduledJobAdminCommand),
            ResourceId = command.Id,
            CorrelationId = command.Id.ToString("D"),
            DetailsJson = JsonSerializer.Serialize(new
            {
                jobName = command.JobName,
                groupName = command.GroupName,
                commandType = command.CommandType,
                status = command.Status,
                resultMessage = command.ResultMessage,
                errorMessage,
                requestedByUserId = command.RequestedByUserId,
                createdAtUtc = command.CreatedAt,
                processedAtUtc = command.ProcessedAtUtc
            }),
            CreatedAt = timestamp,
            CreatedBy = command.RequestedByUserId
        };
    }
}
