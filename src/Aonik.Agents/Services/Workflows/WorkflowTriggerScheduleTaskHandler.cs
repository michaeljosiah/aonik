using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Tasks;

namespace Aonik.Agents.Services.Workflows;

internal sealed class WorkflowTriggerScheduleTaskHandler(AgentsDbContext database, ITenantProvider tenantProvider, IClock clock,
    WorkflowTriggerTaskProtector proofs, WorkflowTriggerAdmissionService admissions, IRecurrenceScheduleCalculator recurrence) : ITaskActionHandler
{
    public string ActionType => "workflow_trigger_schedule";
    public async Task<TaskActionResult> ExecuteAsync(TaskActionContext context, CancellationToken cancellationToken = default)
    {
        if (context.TenantId != tenantProvider.GetCurrentTenantId() || context.Kind != TaskKinds.ScheduledAction || context.AssigneeType != TaskAssigneeTypes.System)
            throw new ArgumentException("Invalid recurring workflow trigger task scope.");
        WorkflowTriggerSchedulePayload payload = proofs.ReadSchedule(context.ActionPayloadJson, context.TenantId, context.WorkItemId);
        if (context.SubjectType != "WorkflowTriggerBinding" || context.SubjectId != payload.BindingId) throw new ArgumentException("The schedule subject does not match its protected binding.");
        var binding = await database.WorkflowTriggerBindings.AsNoTracking().SingleOrDefaultAsync(binding => binding.TenantId == context.TenantId && binding.Id == payload.BindingId, cancellationToken);
        if (binding is null || binding.Status != "Active" || binding.ActiveRevision != payload.Revision) return new(TaskActionOutcome.Skipped, Error: "TriggerRevisionNotActive");
        if (binding.TaskId != context.WorkItemId) throw new ArgumentException("Only the binding's actual recurring task may admit its occurrences.");
        WorkflowTriggerDefinition definition = WorkflowTriggerService.Read(binding.ActiveJson!);
        WorkflowTriggerSchedule schedule = definition.Schedule ?? throw new ArgumentException("The binding is not a recurring source.");
        DateTime occurrence = DateTime.SpecifyKind(context.ScheduledForUtc, DateTimeKind.Utc);
        DateTime observedAt = DateTime.SpecifyKind(context.FirstStartedAtUtc ?? clock.UtcNow, DateTimeKind.Utc);
        if (occurrence < schedule.StartAtUtc || occurrence > schedule.EndAtUtc || occurrence > clock.UtcNow
            || recurrence.GetNextOccurrenceUtc(schedule.Cron, schedule.TimeZone, occurrence.AddSeconds(-1)) != occurrence)
            return new(TaskActionOutcome.Skipped, Error: "TriggerOutsideSchedule");
        if (observedAt - occurrence > TimeSpan.FromMinutes(2) && schedule.MaximumCatchUpOccurrences == 0)
            return new(TaskActionOutcome.Skipped, Error: "MissedOccurrenceSkipped");
        int maximum = Math.Max(1, schedule.MaximumCatchUpOccurrences);
        List<Guid> admitted = [];
        for (int index = 0; index < maximum && occurrence <= observedAt && occurrence <= schedule.EndAtUtc; index++)
        {
            Guid eventId = WorkflowTriggerTaskProtector.Identity("schedule-occurrence", binding.Id, payload.Revision, occurrence);
            foreach (Guid subjectId in definition.Subject.FixedIds)
            {
                TaskActionResult result = await admissions.AdmitAsync(new(binding.Id, payload.Revision, eventId, occurrence,
                    definition.Subject.Type, subjectId, null), cancellationToken);
                if (result.Outcome is TaskActionOutcome.Deferred or TaskActionOutcome.Failed) return result;
                if (result.ResultJson is { } json) admitted.Add(JsonSerializer.Deserialize<JsonElement>(json).GetProperty("runId").GetGuid());
            }
            DateTime? next = recurrence.GetNextOccurrenceUtc(schedule.Cron, schedule.TimeZone, occurrence);
            if (next is null) break;
            occurrence = next.Value;
        }
        return new(TaskActionOutcome.Succeeded, JsonSerializer.Serialize(new { runIds = admitted }, WorkflowDefinitionCodec.JsonOptions));
    }
}
