using Aonik.Platform.Contracts.Services.Tasks;
using Aonik.Platform.Entities.Tasks;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Platform.Services.Tasks;

/// <summary>
/// Platform implementation of the cross-module <see cref="ITaskService"/> (Spec 034).
/// Persists <see cref="WorkItem"/> rows; the <c>WorkItemDispatcher</c> (driven by a
/// Quartz heartbeat in the Worker) is what actually fires them. Schedule-time
/// validation rejects an unknown <c>ActionType</c> so it is never stored.
/// </summary>
internal sealed class WorkItemService : ITaskService, IWorkItemAdminService
{
    private static readonly HashSet<string> ValidAssigneeTypes = new(StringComparer.Ordinal)
    {
        TaskAssigneeTypes.System,
        TaskAssigneeTypes.User,
        TaskAssigneeTypes.Agent,
    };

    private readonly PlatformDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;
    private readonly RecurrenceCalculator _recurrence;
    private readonly ITaskActionHandlerCatalog _handlerCatalog;

    public WorkItemService(
        PlatformDbContext dbContext,
        ITenantProvider tenantProvider,
        IClock clock,
        RecurrenceCalculator recurrence,
        ITaskActionHandlerCatalog handlerCatalog)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _clock = clock;
        _recurrence = recurrence;
        _handlerCatalog = handlerCatalog;
    }

    public async Task<TaskResponse> ScheduleAsync(ScheduleTaskRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new ArgumentException("Task title is required.", nameof(request));
        }

        if (!ValidAssigneeTypes.Contains(request.AssigneeType))
        {
            throw new ArgumentException(
                $"Unknown assignee type '{request.AssigneeType}'. Expected System, User, or Agent.", nameof(request));
        }

        // The action-handler boundary: reject an action no module can execute, before persisting.
        if (!_handlerCatalog.IsRegistered(request.ActionType))
        {
            throw new ArgumentException(
                $"No task action handler is registered for action type '{request.ActionType}'.", nameof(request));
        }

        var isRecurring = !string.IsNullOrWhiteSpace(request.RecurrenceCron);
        if (isRecurring && request.RunAtUtc.HasValue)
        {
            throw new ArgumentException(
                "Provide either RunAtUtc (one-off) or RecurrenceCron (recurring), not both.", nameof(request));
        }

        if (isRecurring && !_recurrence.IsValidCron(request.RecurrenceCron))
        {
            throw new ArgumentException(
                $"Invalid Quartz cron expression '{request.RecurrenceCron}'.", nameof(request));
        }

        var now = _clock.UtcNow;
        var tenantId = _tenantProvider.GetCurrentTenantId();

        string scheduleType;
        string status = TaskStatuses.Scheduled;
        DateTime? nextRunAtUtc;

        if (isRecurring)
        {
            scheduleType = TaskScheduleTypes.Recurring;
            var from = request.StartAtUtc is { } start && start > now ? start : now;
            nextRunAtUtc = _recurrence.GetNextOccurrenceUtc(request.RecurrenceCron!, request.Timezone, from);

            // A recurrence whose window has already closed has no occurrences — born complete.
            if (nextRunAtUtc is null || (request.EndAtUtc is { } end && nextRunAtUtc > end))
            {
                nextRunAtUtc = null;
                status = TaskStatuses.Completed;
            }
        }
        else
        {
            scheduleType = TaskScheduleTypes.OneOff;
            nextRunAtUtc = request.RunAtUtc ?? now;
        }

        var workItem = new WorkItem
        {
            TenantId = tenantId,
            Title = request.Title.Trim(),
            Description = request.Description,
            Kind = string.IsNullOrWhiteSpace(request.Kind) ? TaskKinds.ScheduledAction : request.Kind,
            SubjectType = request.SubjectType,
            SubjectId = request.SubjectId,
            AssigneeType = request.AssigneeType,
            AssigneeId = request.AssigneeId,
            AssigneeKey = request.AssigneeKey,
            ActionType = request.ActionType,
            ActionPayloadJson = string.IsNullOrWhiteSpace(request.ActionPayloadJson) ? "{}" : request.ActionPayloadJson,
            ScheduleType = scheduleType,
            NextRunAtUtc = nextRunAtUtc,
            RecurrenceCron = request.RecurrenceCron,
            Timezone = request.Timezone,
            StartAtUtc = request.StartAtUtc,
            EndAtUtc = request.EndAtUtc,
            MaxRuns = request.MaxRuns,
            RunCount = 0,
            AttemptCount = 0,
            Status = status,
            Priority = request.Priority,
            SourceModule = string.IsNullOrWhiteSpace(request.SourceModule) ? "Platform" : request.SourceModule,
            CorrelationId = request.CorrelationId,
        };

        _dbContext.WorkItems.Add(workItem);
        await _dbContext.SaveChangesAsync(ct);

        return Map(workItem);
    }

    public async Task<IReadOnlyList<TaskResponse>> ListAsync(string? status, int take, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.WorkItems.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }

        var capped = take <= 0 ? 100 : Math.Min(take, 500);
        var items = await query
            .OrderBy(x => x.NextRunAtUtc ?? DateTime.MaxValue)
            .ThenByDescending(x => x.CreatedAt)
            .Take(capped)
            .ToListAsync(cancellationToken);

        return items.Select(Map).ToList();
    }

    public async Task<TaskResponse?> GetAsync(Guid taskId, CancellationToken ct = default)
    {
        var workItem = await _dbContext.WorkItems
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == taskId, ct);

        return workItem is null ? null : Map(workItem);
    }

    public async Task<IReadOnlyList<TaskResponse>> ListForSubjectAsync(
        string subjectType, Guid subjectId, CancellationToken ct = default)
    {
        var items = await _dbContext.WorkItems
            .AsNoTracking()
            .Where(x => x.SubjectType == subjectType && x.SubjectId == subjectId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        return items.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<TaskResponse>> ListForAssigneeAsync(
        string assigneeType, Guid? assigneeId, CancellationToken ct = default)
    {
        var query = _dbContext.WorkItems
            .AsNoTracking()
            .Where(x => x.AssigneeType == assigneeType);

        query = assigneeId.HasValue
            ? query.Where(x => x.AssigneeId == assigneeId.Value)
            : query.Where(x => x.AssigneeId == null);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        return items.Select(Map).ToList();
    }

    public async Task PauseAsync(Guid taskId, CancellationToken ct = default)
    {
        var workItem = await LoadAsync(taskId, ct);

        // Only an armed task can be paused; terminal/in-flight tasks are left untouched.
        if (workItem.Status == TaskStatuses.Scheduled)
        {
            workItem.Status = TaskStatuses.Paused;
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    public async Task ResumeAsync(Guid taskId, CancellationToken ct = default)
    {
        var workItem = await LoadAsync(taskId, ct);

        if (workItem.Status != TaskStatuses.Paused)
        {
            return;
        }

        workItem.Status = TaskStatuses.Scheduled;

        // Re-arm: a recurring task with no armed occurrence gets its next cron fire;
        // a one-off with no time fires on the next sweep.
        if (workItem.NextRunAtUtc is null)
        {
            workItem.NextRunAtUtc =
                workItem.ScheduleType == TaskScheduleTypes.Recurring && workItem.RecurrenceCron is not null
                    ? _recurrence.GetNextOccurrenceUtc(workItem.RecurrenceCron, workItem.Timezone, _clock.UtcNow)
                    : _clock.UtcNow;

            // A recurrence with nothing left to fire stays complete rather than resuming.
            if (workItem.NextRunAtUtc is null)
            {
                workItem.Status = TaskStatuses.Completed;
            }
        }

        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task CancelAsync(Guid taskId, CancellationToken ct = default)
    {
        var workItem = await LoadAsync(taskId, ct);

        if (workItem.Status is TaskStatuses.Completed or TaskStatuses.Cancelled)
        {
            return;
        }

        workItem.Status = TaskStatuses.Cancelled;
        workItem.NextRunAtUtc = null;
        await _dbContext.SaveChangesAsync(ct);
    }

    private async Task<WorkItem> LoadAsync(Guid taskId, CancellationToken ct) =>
        await _dbContext.WorkItems.FirstOrDefaultAsync(x => x.Id == taskId, ct)
        ?? throw new KeyNotFoundException($"Task {taskId} not found.");

    private static TaskResponse Map(WorkItem w) => new(
        w.Id,
        w.TenantId,
        w.Title,
        w.Description,
        w.Kind,
        w.SubjectType,
        w.SubjectId,
        w.AssigneeType,
        w.AssigneeId,
        w.AssigneeKey,
        w.ActionType,
        w.ScheduleType,
        w.NextRunAtUtc,
        w.RecurrenceCron,
        w.Timezone,
        w.StartAtUtc,
        w.EndAtUtc,
        w.RunCount,
        w.MaxRuns,
        w.Status,
        w.Priority,
        w.SourceModule,
        w.CorrelationId,
        w.LastError,
        w.CreatedAt,
        w.UpdatedAt);
}
