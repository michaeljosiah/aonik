using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Entities;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Services.Workflows;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Agents.Workflows.Graph;

internal sealed partial class WorkflowStepExecutionService
{
    public async Task<WorkflowStepWait> PrepareHumanAsync(WorkflowDefinitionNode node, WorkflowExecutionFrame frame, CancellationToken cancellationToken)
    {
        await RequireActiveAsync(frame.RunId, cancellationToken);
        if (invocationContext.Definition?.SchemaVersion != 2) throw new WorkflowExecutionException("ControlSchemaRequired");
        WorkflowHumanParameters parameters = WorkflowHumanTaskContract.Parameters(node);
        WorkflowStepRun step = await StepAsync(node.Key, cancellationToken);
        Run.PendingStepRunId = step.Id;
        if (step.ApprovalRequestId.HasValue) return new(Run.Id, step.Id, "Human");
        WorkflowFactsSnapshot facts = await UnchangedFactsAsync(cancellationToken);
        step.DeadlineUtc ??= new[] { clock.UtcNow.AddSeconds(parameters.TimeoutSeconds), Run.DeadlineUtc!.Value }.Min();
        JsonElement arguments = WorkflowHumanTaskContract.Arguments(Run, step, parameters);
        step.ActionKey = WorkflowHumanTaskContract.Key; step.ActionVersion = 1; step.RequiredPermission = "Workflow.Run";
        step.ArgumentsJson = arguments.GetRawText(); step.ArgumentsHash = Hash(arguments);
        step.FactsJson = JsonSerializer.Serialize(facts, WorkflowDefinitionCodec.JsonOptions); step.FactsHash = WorkflowFactsHash.Compute(facts);
        step.ExpectedSubjectVersion = facts.SubjectVersion; step.Status = "Prepared";
        await database.SaveChangesAsync(cancellationToken);
        invocationContext.Step = step;
        try
        {
            ToolGateOutcome gated = await approvals.GateAsync(new(WorkflowHumanTaskContract.ToolName, WorkflowHumanTaskContract.Descriptor.Approval!,
                Arguments(arguments), new("Respond to workflow task", JsonSerializer.SerializeToElement(new { subjectId = Run.SubjectId }), arguments)), cancellationToken);
            if (gated.Decision != ToolGateDecision.PendingApproval) throw new WorkflowExecutionException("HumanTaskGateRefused");
            step.Status = "WaitingForApproval";
            await database.SaveChangesAsync(cancellationToken);
        }
        finally { invocationContext.Step = null; }
        return new(Run.Id, step.Id, "Human");
    }

    private async Task<WorkflowExecutionFrame?> ReadyHumanResponseAsync(WorkflowStepWait wait, CancellationToken cancellationToken)
    {
        await RequireActiveAsync(wait.RunId, cancellationToken, allowWaiting: true);
        WorkflowStepRun step = await database.WorkflowStepRuns.SingleAsync(step => step.TenantId == Run.TenantId && step.RunId == Run.Id
            && step.Id == wait.StepRunId, cancellationToken);
        await database.Entry(step).ReloadAsync(cancellationToken);
        if (step.Status == "Completed") return JsonSerializer.Deserialize<WorkflowExecutionFrame>(step.ResultJson!, WorkflowDefinitionCodec.JsonOptions);
        if (step.ActionKey != WorkflowHumanTaskContract.Key || step.ApprovalRequestId is not { } requestId || Run.PendingStepRunId != step.Id)
            throw new WorkflowExecutionException("HumanTaskIdentityMismatch");
        ToolApprovalRequest request = await database.ToolApprovalRequests.SingleAsync(request => request.TenantId == Run.TenantId
            && request.Id == requestId && request.WorkflowStepRunId == step.Id, cancellationToken);
        await database.Entry(request).ReloadAsync(cancellationToken);
        if (request.Status == ToolApprovalRequestStatus.Pending && request.ExpiresAt > clock.UtcNow) return null;
        if (request.Status == ToolApprovalRequestStatus.Pending)
        {
            request.Status = ToolApprovalRequestStatus.Expired;
            await database.SaveChangesAsync(cancellationToken);
        }
        if (request.Status == ToolApprovalRequestStatus.Cancelled) throw new WorkflowExecutionException("RunCancelled");
        return new(Run.Id);
    }

    public async Task<WorkflowExecutionFrame> CompleteHumanAsync(WorkflowDefinitionNode node, WorkflowExecutionFrame frame, CancellationToken cancellationToken)
    {
        await RequireActiveAsync(frame.RunId, cancellationToken);
        WorkflowStepRun step = await StepAsync(node.Key, cancellationToken);
        if (step.Status == "Completed") return JsonSerializer.Deserialize<WorkflowExecutionFrame>(step.ResultJson!, WorkflowDefinitionCodec.JsonOptions)!;
        WorkflowHumanParameters parameters = WorkflowHumanTaskContract.Parameters(node);
        ToolApprovalRequest request = await database.ToolApprovalRequests.SingleAsync(request => request.TenantId == Run.TenantId
            && request.Id == step.ApprovalRequestId && request.WorkflowStepRunId == step.Id, cancellationToken);
        string route;
        if (request.Status == ToolApprovalRequestStatus.Approved)
        {
            if (request.DecidedByUserId is not { } userId || request.DecidedAt is null || request.DecidedAt >= request.ExpiresAt || request.ResponseJson is null
                || ToolArgumentsHash.Compute(JsonSerializer.Deserialize<Dictionary<string, object?>>(request.ResponseJson)!) != request.ResponseHash)
                throw new WorkflowExecutionException("HumanResponseIdentityMismatch");
            await reviewers.RequireAsync(request, userId, cancellationToken);
            JsonElement response = JsonSerializer.Deserialize<JsonElement>(request.ResponseJson);
            WorkflowHumanTaskContract.ValidateResponse(parameters, new(ToolApprovalDecisionType.Approve, Response: response));
            route = parameters.Choices.Single(choice => choice.Key == response.GetProperty("choice").GetString()).Route;
        }
        else if (request.Status == ToolApprovalRequestStatus.Rejected && request.DecidedByUserId.HasValue && request.DecidedAt < request.ExpiresAt)
            route = parameters.RejectRoute;
        else if (request.Status == ToolApprovalRequestStatus.Expired && clock.UtcNow >= request.ExpiresAt) route = parameters.TimeoutRoute;
        else throw new WorkflowExecutionException("HumanTaskNotDecided");
        WorkflowExecutionFrame result = new(Run.Id, Route: route);
        request.ConsumedAt ??= clock.UtcNow;
        step.SelectedRoute = route; step.Status = "Completed"; step.CompletedAt = clock.UtcNow;
        step.ResultJson = JsonSerializer.Serialize(result, WorkflowDefinitionCodec.JsonOptions);
        Run.PendingStepRunId = null;
        await database.SaveChangesAsync(cancellationToken);
        return result;
    }
}
