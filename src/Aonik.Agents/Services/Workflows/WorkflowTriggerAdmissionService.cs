using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Persistence;
using Aonik.Agents.Workflows.Graph;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Tasks;

namespace Aonik.Agents.Services.Workflows;

internal sealed class WorkflowTriggerAdmissionService(AgentsDbContext database, ITenantProvider tenantProvider, WorkflowRunService runs, IClock clock)
{
    public async Task<TaskActionResult> AdmitAsync(WorkflowTriggerAdmissionPayload payload, CancellationToken cancellationToken = default)
    {
        Guid tenantId = tenantProvider.GetCurrentTenantId();
        Guid? existing = await database.WorkflowRuns.AsNoTracking().Where(run => run.TenantId == tenantId && run.TriggerBindingId == payload.BindingId
            && run.TriggerBindingRevision == payload.Revision && run.SourceEventId == payload.SourceEventId && run.SubjectId == payload.SubjectId)
            .Select(run => (Guid?)run.Id).SingleOrDefaultAsync(cancellationToken);
        if (existing.HasValue) return Result("Replay", existing.Value);
        var binding = await database.WorkflowTriggerBindings.AsNoTracking().SingleOrDefaultAsync(binding => binding.TenantId == tenantId && binding.Id == payload.BindingId, cancellationToken);
        if (binding is null || binding.Status != "Active" || binding.ActiveRevision != payload.Revision || binding.ActiveJson is null)
            return new(TaskActionOutcome.Skipped, Error: "TriggerRevisionNotActive");
        WorkflowTriggerDefinition definition = WorkflowTriggerService.Read(binding.ActiveJson);
        string key = "trigger-" + WorkflowDefinitionCodec.Hash(JsonSerializer.Serialize(new { payload.BindingId, payload.Revision, payload.SourceEventId, payload.SubjectId }, WorkflowDefinitionCodec.JsonOptions));
        try
        {
            Guid runId = await runs.AdmitTriggeredAsync(definition.AutomationIdentityId, definition.WorkflowVersionId, payload.SubjectType, payload.SubjectId, key,
                new(binding.Id, payload.Revision, binding.ActivationProposalId!.Value, binding.ActiveHash!, payload.SourceEventId, payload.SourceOccurredAtUtc,
                    payload.CausationRunId, payload.SelfGenerated), cancellationToken);
            return Result("Admitted", runId);
        }
        catch (WorkflowExecutionException exception) when (exception.Code is "TriggerCapacityReached" or "TriggerSubjectAlreadyActive")
        { return new(TaskActionOutcome.Deferred, Error: exception.Code, DeferredUntilUtc: clock.UtcNow.AddMinutes(1)); }
        catch (WorkflowExecutionException exception) when (exception.Code is "SelfGeneratedTriggerIgnored" or "TriggerCausationLimit" or "TriggerOccurrenceLimit")
        { return new(TaskActionOutcome.Skipped, Error: exception.Code); }
        catch (PermissionDeniedException) { return new(TaskActionOutcome.Failed, Error: "ExecutionAuthorityChanged"); }
        catch (ConnectorAccessException exception) { return new(TaskActionOutcome.Failed, Error: exception.Code); }
    }
    private static TaskActionResult Result(string status, Guid runId) => new(TaskActionOutcome.Succeeded,
        JsonSerializer.Serialize(new { status, runId }, WorkflowDefinitionCodec.JsonOptions));
}
