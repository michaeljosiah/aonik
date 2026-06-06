using Aonik.Platform.Contracts.Services.Tasks;
using Aonik.Platform.Entities.Tasks;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Tasks;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aonik.Platform.Services.Tasks;

/// <summary>
/// Dispatches due <see cref="WorkItem"/>s (Spec 034). Cluster-safety is layered:
/// <list type="number">
/// <item>A claim-before-execute lease with a per-claim <em>fencing token</em> (mirrors
///   <c>OutboxProcessor</c>): concurrent claims race on the row's optimistic-concurrency token,
///   so only one worker wins.</item>
/// <item><em>Lease renewal</em>: while a handler runs, the holder heart-beats its lease, so a slow
///   handler is never reclaimed — a lapsed lease means the worker is genuinely gone, not just busy.</item>
/// <item>A <em>fenced outcome write</em>: a worker that lost its lease (it stalled past the lease and
///   was reclaimed) re-reads the token and abandons its bookkeeping instead of double-recording.</item>
/// <item>The unique <c>(WorkItemId, ScheduledForUtc)</c> run row dedupes occurrence <em>rows</em>.</item>
/// </list>
/// The lease does not by itself guarantee exactly-once <em>execution</em>: a worker that stalls past
/// its (un-renewed) lease and later resumes can briefly run a handler concurrently with its
/// replacement. Handlers must therefore be idempotent for their side effects (GET-before-act) —
/// exactly as proposal handlers are, and as the outbox relies on inbox idempotency — so a duplicate
/// invocation produces no duplicate effect. The cross-tenant due-scan runs under a system (see-all)
/// context; every item is processed under its own tenant so all writes are tenant-correct and the
/// propose-don't-execute boundary holds (a handler's only effect for a high-risk action is a Proposal).
/// </summary>
internal sealed class WorkItemDispatcher : IWorkItemDispatcher
{
    /// <summary>Identifies this process in lease tokens. Stable for the process lifetime.</summary>
    private static readonly string InstanceId = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RecurrenceCalculator _recurrence;
    private readonly IClock _clock;
    private readonly ILogger<WorkItemDispatcher> _logger;

    public WorkItemDispatcher(
        IServiceScopeFactory scopeFactory,
        RecurrenceCalculator recurrence,
        IClock clock,
        ILogger<WorkItemDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _recurrence = recurrence;
        _clock = clock;
        _logger = logger;
    }

    public async Task<WorkItemDispatchSummary> DispatchDueAsync(
        WorkItemDispatchOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var dueRefs = await ScanDueAsync(options.BatchSize, cancellationToken).ConfigureAwait(false);
        if (dueRefs.Count == 0)
        {
            return WorkItemDispatchSummary.Empty;
        }

        int succeeded = 0, proposed = 0, skipped = 0, failed = 0;

        foreach (var dueRef in dueRefs)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var outcome = await ProcessOneAsync(dueRef, options, cancellationToken).ConfigureAwait(false);
            switch (outcome)
            {
                case TaskActionOutcome.Succeeded: succeeded++; break;
                case TaskActionOutcome.Proposed: proposed++; break;
                case TaskActionOutcome.Failed: failed++; break;
                default: skipped++; break;
            }
        }

        return new WorkItemDispatchSummary(dueRefs.Count, succeeded, proposed, skipped, failed);
    }

    /// <summary>
    /// Reads due item ids across all tenants under a system (see-all) context. Read-only
    /// and <c>AcrossTenants()</c> (which also lifts the soft-delete filter, hence the explicit
    /// <c>!IsDeleted</c> guard), so no tenant-scoped write is attempted here.
    /// </summary>
    private async Task<List<WorkItemRef>> ScanDueAsync(int batchSize, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        SetTenant(scope.ServiceProvider, tenantId: null);

        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var now = _clock.UtcNow;

        // Scheduled rows are normal due work; InProgress rows whose lease has lapsed are
        // crash-recovery — a worker claimed and died mid-execution, so we reclaim them once the
        // lease expires (an active lease still hides them). Keep this predicate in sync with IsClaimable.
        return await db.WorkItems
            .AcrossTenants()
            .AsNoTracking()
            .Where(w => (w.Status == TaskStatuses.Scheduled || w.Status == TaskStatuses.InProgress)
                && w.NextRunAtUtc != null
                && w.NextRunAtUtc <= now
                && !w.IsDeleted
                && (w.LeasedUntilUtc == null || w.LeasedUntilUtc <= now))
            .OrderBy(w => w.NextRunAtUtc)
            .Take(Math.Max(batchSize, 1))
            .Select(w => new WorkItemRef(w.Id, w.TenantId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<TaskActionOutcome> ProcessOneAsync(
        WorkItemRef dueRef,
        WorkItemDispatchOptions options,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        SetTenant(scope.ServiceProvider, dueRef.TenantId);

        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var now = _clock.UtcNow;

        var workItem = await db.WorkItems
            .FirstOrDefaultAsync(w => w.Id == dueRef.Id, cancellationToken)
            .ConfigureAwait(false);

        if (workItem is null || !IsClaimable(workItem, now))
        {
            return TaskActionOutcome.Skipped;
        }

        var scheduledFor = workItem.NextRunAtUtc!.Value;

        // Occurrence already finished (defensive — should be rare since re-arm is atomic):
        // ensure the item is moved on and release.
        var existingRun = await db.WorkItemRuns
            .FirstOrDefaultAsync(r => r.WorkItemId == workItem.Id && r.ScheduledForUtc == scheduledFor, cancellationToken)
            .ConfigureAwait(false);
        if (existingRun is not null && !string.IsNullOrEmpty(existingRun.Outcome))
        {
            ReArm(workItem, now);
            ReleaseLease(workItem);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return TaskActionOutcome.Skipped;
        }

        // Claim the lease with a unique fencing token. On SQL Server a concurrent claim loses on the
        // rowversion token; the token additionally lets the fenced outcome write below detect, after a
        // long handler, whether THIS worker still owns the occurrence before recording anything.
        var leaseSeconds = Math.Max(options.LeaseSeconds, 1);
        var leaseToken = $"{InstanceId}:{Guid.NewGuid():N}";
        workItem.Status = TaskStatuses.InProgress;
        workItem.LeasedBy = leaseToken;
        workItem.LeasedUntilUtc = now.AddSeconds(leaseSeconds);
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            return TaskActionOutcome.Skipped; // another worker claimed it first
        }

        // One run per occurrence (the run-row idempotency anchor).
        var run = existingRun;
        if (run is null)
        {
            run = new WorkItemRun
            {
                TenantId = workItem.TenantId,
                WorkItemId = workItem.Id,
                ScheduledForUtc = scheduledFor,
                StartedAtUtc = now,
                Outcome = string.Empty,
            };
            db.WorkItemRuns.Add(run);
            try
            {
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateException)
            {
                // Unique (WorkItemId, ScheduledForUtc) violation: another worker created the run.
                return TaskActionOutcome.Skipped;
            }
        }
        else
        {
            run.StartedAtUtc = now; // crash-recovery retry of an in-flight occurrence
        }

        // Execute the handler while heart-beating the lease, so a slow handler keeps its claim and is
        // never reclaimed mid-flight by another worker. A lapsed lease then means the worker is gone.
        using var renewalCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var renewalTask = RenewLeaseLoopAsync(dueRef.TenantId, workItem.Id, leaseToken, leaseSeconds, renewalCts.Token);

        TaskActionResult result;
        try
        {
            result = await ExecuteHandlerAsync(dueRef.TenantId, workItem, run, scheduledFor, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            await renewalCts.CancelAsync().ConfigureAwait(false);
            try { await renewalTask.ConfigureAwait(false); } catch { /* heartbeat shutdown is best-effort */ }
        }

        // Fenced outcome write: refresh the row (renewals advanced it in a separate context) and only
        // finalize if we STILL hold our lease token. If we lost it, a replacement worker reclaimed this
        // occurrence while we stalled — abandon our bookkeeping so we never double-record or re-arm.
        // (A side effect the handler already performed in that rare window relies on handler
        // idempotency; see class remarks.)
        await db.Entry(workItem).ReloadAsync(cancellationToken).ConfigureAwait(false);
        if (workItem.LeasedBy != leaseToken)
        {
            _logger.LogWarning(
                "Work item {WorkItemId} occurrence {ScheduledFor:o} was reclaimed by another worker mid-execution; abandoning this run's bookkeeping.",
                workItem.Id, scheduledFor);
            return TaskActionOutcome.Skipped;
        }

        ApplyResult(workItem, run, result, options.MaxAttempts, _clock.UtcNow);
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Lost a final race with a reclaiming worker — safe to abandon; it owns the outcome.
            return TaskActionOutcome.Skipped;
        }

        return result.Outcome;
    }

    /// <summary>
    /// Heart-beats the lease on <paramref name="workItemId"/> every half-lease while a handler runs,
    /// but only while THIS worker still holds <paramref name="leaseToken"/>. Stops as soon as the
    /// token is no longer ours (another worker reclaimed) or on cancellation. Runs in its own scope
    /// so its writes don't disturb the dispatcher's bookkeeping context.
    /// </summary>
    private async Task RenewLeaseLoopAsync(
        Guid tenantId,
        Guid workItemId,
        string leaseToken,
        int leaseSeconds,
        CancellationToken cancellationToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(leaseSeconds / 2.0, 0.5));

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, cancellationToken).ConfigureAwait(false);

                using var scope = _scopeFactory.CreateScope();
                SetTenant(scope.ServiceProvider, tenantId);
                var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

                var workItem = await db.WorkItems
                    .FirstOrDefaultAsync(w => w.Id == workItemId && w.LeasedBy == leaseToken, cancellationToken)
                    .ConfigureAwait(false);
                if (workItem is null)
                {
                    return; // lease lost (reclaimed) or row gone — stop heart-beating
                }

                workItem.LeasedUntilUtc = _clock.UtcNow.AddSeconds(leaseSeconds);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                // A renewal failure (e.g. a concurrent reclaim) just stops the heartbeat; the fenced
                // outcome write then detects the lost lease and abandons this worker's bookkeeping.
                _logger.LogDebug(ex, "Lease renewal stopped for work item {WorkItemId}.", workItemId);
                return;
            }
        }
    }

    /// <summary>
    /// Resolves and runs the keyed handler in its OWN scope, so a handler that throws
    /// mid-write never leaves dangling tracked entities on the dispatcher's bookkeeping
    /// context. A missing handler is an expected, recorded failure (the action could not run).
    /// </summary>
    private async Task<TaskActionResult> ExecuteHandlerAsync(
        Guid tenantId,
        WorkItem workItem,
        WorkItemRun run,
        DateTime scheduledFor,
        CancellationToken cancellationToken)
    {
        using var handlerScope = _scopeFactory.CreateScope();
        SetTenant(handlerScope.ServiceProvider, tenantId);

        var handler = handlerScope.ServiceProvider.GetKeyedService<ITaskActionHandler>(workItem.ActionType);
        if (handler is null)
        {
            return new TaskActionResult(
                TaskActionOutcome.Failed,
                Error: $"No task action handler is registered for action type '{workItem.ActionType}'.");
        }

        var context = new TaskActionContext(
            tenantId,
            workItem.Id,
            run.Id,
            workItem.Kind,
            workItem.SubjectType,
            workItem.SubjectId,
            workItem.AssigneeType,
            workItem.AssigneeId,
            workItem.AssigneeKey,
            scheduledFor,
            workItem.ActionPayloadJson);

        try
        {
            return await handler.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Task action '{ActionType}' threw for work item {WorkItemId}.",
                workItem.ActionType, workItem.Id);
            return new TaskActionResult(TaskActionOutcome.Failed, Error: Truncate(ex.Message, 4000));
        }
    }

    /// <summary>Records the occurrence outcome on the run and re-arms (or completes/fails) the work item.</summary>
    private void ApplyResult(WorkItem workItem, WorkItemRun run, TaskActionResult result, int maxAttempts, DateTime now)
    {
        run.AiRunId = result.AiRunId;
        run.ProposalId = result.ProposalId;
        run.ResultJson = result.ResultJson;

        if (result.Outcome == TaskActionOutcome.Failed)
        {
            workItem.AttemptCount += 1;
            workItem.LastError = result.Error;
            run.Error = result.Error;

            if (workItem.AttemptCount < Math.Max(maxAttempts, 1))
            {
                // Retry the SAME occurrence on a later sweep; keep the run in-flight.
                workItem.Status = TaskStatuses.Scheduled; // NextRunAtUtc unchanged (== run.ScheduledForUtc)
                ReleaseLease(workItem);
                return;
            }

            // Attempts exhausted for this occurrence.
            run.Outcome = nameof(TaskActionOutcome.Failed);
            run.CompletedAtUtc = now;

            if (workItem.ScheduleType == TaskScheduleTypes.Recurring)
            {
                // Skip-and-continue: don't let one bad occurrence block the series, but still count
                // it toward MaxRuns so a permanently-failing recurring task eventually stops.
                workItem.RunCount += 1;
                workItem.AttemptCount = 0;
                ReArm(workItem, now);
            }
            else
            {
                workItem.Status = TaskStatuses.Failed;
                workItem.NextRunAtUtc = null;
            }

            ReleaseLease(workItem);
            return;
        }

        // Succeeded | Skipped | Proposed — the occurrence is done.
        run.Outcome = result.Outcome.ToString();
        run.CompletedAtUtc = now;
        workItem.RunCount += 1;
        workItem.AttemptCount = 0;
        workItem.LastError = null;

        if (workItem.ScheduleType == TaskScheduleTypes.Recurring)
        {
            ReArm(workItem, now);
        }
        else
        {
            Complete(workItem);
        }

        ReleaseLease(workItem);
    }

    /// <summary>
    /// Arms the next recurring occurrence (next cron fire after <paramref name="now"/>, per the
    /// Spec 034 dispatch diagram), or completes the item when it has run out of occurrences,
    /// hit <c>MaxRuns</c>, or passed <c>EndAtUtc</c>.
    /// </summary>
    private void ReArm(WorkItem workItem, DateTime now)
    {
        if (workItem.ScheduleType != TaskScheduleTypes.Recurring || workItem.RecurrenceCron is null)
        {
            Complete(workItem);
            return;
        }

        if (workItem.MaxRuns is { } max && workItem.RunCount >= max)
        {
            Complete(workItem);
            return;
        }

        var next = _recurrence.GetNextOccurrenceUtc(workItem.RecurrenceCron, workItem.Timezone, now);
        if (next is null || (workItem.EndAtUtc is { } end && next > end))
        {
            Complete(workItem);
            return;
        }

        workItem.NextRunAtUtc = next;
        workItem.Status = TaskStatuses.Scheduled;
    }

    private static void Complete(WorkItem workItem)
    {
        workItem.Status = TaskStatuses.Completed;
        workItem.NextRunAtUtc = null;
    }

    // Mirrors the ScanDueAsync predicate: Scheduled, or InProgress with a lapsed lease (crash recovery).
    private static bool IsClaimable(WorkItem workItem, DateTime now) =>
        (workItem.Status == TaskStatuses.Scheduled || workItem.Status == TaskStatuses.InProgress)
        && workItem.NextRunAtUtc is not null
        && workItem.NextRunAtUtc <= now
        && (workItem.LeasedUntilUtc is null || workItem.LeasedUntilUtc <= now);

    private static void ReleaseLease(WorkItem workItem)
    {
        workItem.LeasedBy = null;
        workItem.LeasedUntilUtc = null;
    }

    private static void SetTenant(IServiceProvider serviceProvider, Guid? tenantId)
    {
        var tenantContext = serviceProvider.GetRequiredService<ITenantContext>();
        tenantContext.TenantId = tenantId;
        tenantContext.ResolutionSource = "task-dispatcher";
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private readonly record struct WorkItemRef(Guid Id, Guid TenantId);
}
