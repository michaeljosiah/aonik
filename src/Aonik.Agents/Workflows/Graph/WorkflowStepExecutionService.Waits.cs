using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Services.Workflows;
using Aonik.SharedKernel.Events.Integration;

namespace Aonik.Agents.Workflows.Graph;

internal sealed partial class WorkflowStepExecutionService
{
    public async Task<WorkflowStepWait> PrepareWaitAsync(WorkflowDefinitionNode node, WorkflowExecutionFrame frame, CancellationToken cancellationToken)
    {
        await RequireActiveAsync(frame.RunId, cancellationToken);
        if (invocationContext.Definition?.SchemaVersion != 2) throw new WorkflowExecutionException("ControlSchemaRequired");
        WorkflowWaitParameters parameters = node.Parameters.Deserialize<WorkflowWaitParameters>(WorkflowDefinitionCodec.JsonOptions)!;
        WorkflowDefinitionValidator.ValidateWait(parameters);
        WorkflowStepRun step = await StepAsync(node.Key, cancellationToken);
        if (step.Status == "Cancelled") throw new WorkflowExecutionException("RunCancelled");
        if (step.DeadlineUtc is null)
        {
            DateTime deadline = parameters.UntilUtc ?? clock.UtcNow.AddSeconds(parameters.DelaySeconds!.Value);
            if (deadline > clock.UtcNow.AddSeconds(WorkflowControlLimits.MaximumLifetimeSeconds)) throw new WorkflowExecutionException("WaitDeadlineOutOfRange");
            DateTime rootDeadline = Run.DeadlineUtc ?? throw new WorkflowExecutionException("RunDeadlineMissing");
            step.DeadlineUtc = deadline < rootDeadline ? deadline : rootDeadline;
            step.Status = "WaitingForTimer";
            Run.PendingStepRunId = step.Id;
            database.EnqueueIntegrationEvent(new WorkflowContinuationRequestedEvent(step.Id, clock.UtcNow, Run.TenantId, Run.Id, Run.Continuation,
                DateTime.SpecifyKind(step.DeadlineUtc.Value, DateTimeKind.Utc)));
            await database.SaveChangesAsync(cancellationToken);
        }
        return new(Run.Id, step.Id, "Timer");
    }

    private async Task<WorkflowExecutionFrame?> ReadyTimerResponseAsync(WorkflowStepWait wait, CancellationToken cancellationToken)
    {
        await RequireActiveAsync(wait.RunId, cancellationToken, allowWaiting: true);
        WorkflowStepRun step = await database.WorkflowStepRuns.SingleAsync(step => step.TenantId == Run.TenantId && step.RunId == Run.Id
            && step.Id == wait.StepRunId, cancellationToken);
        await database.Entry(step).ReloadAsync(cancellationToken);
        if (step.DeadlineUtc is null || step.Status == "Cancelled") throw new WorkflowExecutionException("TimerIdentityMismatch");
        if (step.Status == "Completed") return JsonSerializer.Deserialize<WorkflowExecutionFrame>(step.ResultJson!, WorkflowDefinitionCodec.JsonOptions);
        if (step.Status != "WaitingForTimer" || Run.PendingStepRunId != step.Id) throw new WorkflowExecutionException("TimerIdentityMismatch");
        return clock.UtcNow < step.DeadlineUtc ? null : new(Run.Id);
    }

    public async Task<WorkflowExecutionFrame> CompleteWaitAsync(WorkflowDefinitionNode node, WorkflowExecutionFrame frame, CancellationToken cancellationToken)
    {
        await RequireActiveAsync(frame.RunId, cancellationToken);
        WorkflowStepRun step = await StepAsync(node.Key, cancellationToken);
        if (step.Status == "Completed") return JsonSerializer.Deserialize<WorkflowExecutionFrame>(step.ResultJson!, WorkflowDefinitionCodec.JsonOptions)!;
        if (step.Status != "WaitingForTimer" || step.DeadlineUtc is not { } deadline || clock.UtcNow < deadline || Run.PendingStepRunId != step.Id)
            throw new WorkflowExecutionException("TimerNotDue");
        var result = new WorkflowExecutionFrame(Run.Id);
        step.Status = "Completed"; step.CompletedAt = clock.UtcNow; step.ResultJson = JsonSerializer.Serialize(result, WorkflowDefinitionCodec.JsonOptions);
        Run.PendingStepRunId = null;
        await database.SaveChangesAsync(cancellationToken);
        return result;
    }
}
