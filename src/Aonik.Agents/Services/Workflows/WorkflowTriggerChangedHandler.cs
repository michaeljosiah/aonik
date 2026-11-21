using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Tasks;
using Aonik.SharedKernel.Events;
using Aonik.SharedKernel.Events.Integration;

namespace Aonik.Agents.Services.Workflows;

internal sealed class WorkflowTriggerChangedHandler(AgentsDbContext database, ITenantProvider tenantProvider, ITaskService tasks,
    WorkflowTriggerTaskProtector proofs, WorkflowRunService runs, IClock clock, IConnectorSubscriptionService? subscriptions = null)
    : IEventHandler<WorkflowTriggerChangedEvent>
{
    public async Task HandleAsync(WorkflowTriggerChangedEvent message, CancellationToken cancellationToken = default)
    {
        if (message.TenantId != tenantProvider.GetCurrentTenantId() || message.EventId == Guid.Empty || message.BindingId == Guid.Empty
            || message.Revision < 1 || message.Change is not ("Activate" or "Disable")) throw new ArgumentException("Invalid trigger configuration event scope.");
        if (message.PreviousTaskId is { } previous && await tasks.GetAsync(previous, cancellationToken) is { } existing)
        {
            if (existing.SubjectType != "WorkflowTriggerBinding" || existing.SubjectId != message.BindingId || existing.SourceModule != "Agents")
                throw new ArgumentException("The prior trigger task belongs to another source.");
            await tasks.CancelAsync(previous, cancellationToken);
        }
        WorkflowTriggerBinding? binding = await database.WorkflowTriggerBindings.SingleOrDefaultAsync(binding => binding.TenantId == message.TenantId && binding.Id == message.BindingId, cancellationToken);
        if (binding is null) return;
        if (message.Change == "Disable")
        {
            if (subscriptions is not null) await subscriptions.DisableBindingAsync(binding.Id, message.Revision, cancellationToken);
            List<WorkflowRun> pending = await database.WorkflowRuns.Where(run => run.TenantId == message.TenantId && run.TriggerBindingId == binding.Id
                && run.TriggerBindingRevision <= message.Revision && run.Status != WorkflowRunStatuses.Completed
                && run.Status != WorkflowRunStatuses.Cancelled && run.Status != WorkflowRunStatuses.Failed).Take(101).ToListAsync(cancellationToken);
            if (pending.Count > 100) throw new InvalidOperationException("Trigger cancellation exceeds its bounded active-run limit.");
            foreach (WorkflowRun run in pending)
            {
                run.Status = WorkflowRunStatuses.Cancelled; run.Outcome = "Cancelled"; run.ErrorCode = "TriggerDisabled";
                run.CompletedAt = clock.UtcNow; run.Continuation++;
                await WorkflowRunCancellation.StageAsync(database, run, "TriggerDisabled", clock.UtcNow, cancellationToken);
                runs.Queue(run, run.LeaseExpiresAt is { } lease && lease > clock.UtcNow ? lease : clock.UtcNow);
            }
            await database.SaveChangesAsync(cancellationToken);
            return;
        }
        if (binding.Status != "Active" || binding.ActiveRevision != message.Revision) return;
        WorkflowTriggerDefinition definition = WorkflowTriggerService.Read(binding.ActiveJson!);
        if (definition.Schedule is { } schedule)
        {
            if (message.TaskId is not { } taskId || taskId != binding.TaskId || message.ScheduleProof is null
                || proofs.ReadSchedule(message.ScheduleProof, message.TenantId, taskId) != new WorkflowTriggerSchedulePayload(binding.Id, message.Revision))
                throw new ArgumentException("The recurring trigger task is missing its exact server-issued schedule proof.");
            await tasks.ScheduleAsync(new($"Run workflow trigger {binding.Id:D}", TaskKinds.ScheduledAction, "workflow_trigger_schedule", message.ScheduleProof,
                TaskAssigneeTypes.System, SubjectType: "WorkflowTriggerBinding", SubjectId: binding.Id, RecurrenceCron: schedule.Cron, Timezone: schedule.TimeZone,
                StartAtUtc: schedule.StartAtUtc, EndAtUtc: schedule.EndAtUtc, MaxRuns: schedule.MaximumOccurrences, SourceModule: "Agents",
                CorrelationId: $"trigger:{binding.Id:D}:{message.Revision}", TaskId: taskId), cancellationToken);
        }
        else if (definition.Mailbox is not null)
        {
            if (subscriptions is null) throw new InvalidOperationException("Mailbox synchronization is unavailable.");
            binding.SubscriptionId = await subscriptions.ActivateAsync(binding.Id, message.Revision, cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
        }
    }
}
