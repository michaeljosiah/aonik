using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Entities;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;
using Aonik.Agents.Services.Workflows;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Agents.Workflows.Graph;

/// <summary>Durable action evidence and authorization around MAF executors; MAF owns graph routing.</summary>
internal sealed class WorkflowStepExecutionService(
    AgentsDbContext database, WorkflowInvocationContext invocationContext, WorkflowExecutionAuthorization authorization,
    WorkflowActionRegistry actions, IToolApprovalService approvals, IClock clock)
{
    private WorkflowRun Run => invocationContext.Run ?? throw new WorkflowExecutionException("ExecutionIdentityMissing");

    public async Task<WorkflowExecutionFrame> ExecuteNodeAsync(WorkflowDefinitionNode node, WorkflowExecutionFrame frame, CancellationToken cancellationToken)
    {
        await RequireActiveAsync(frame.RunId, cancellationToken);
        WorkflowStepRun step = await StepAsync(node.Key, cancellationToken);
        if (step.Status == "Completed" && step.ResultJson is { } saved)
            return JsonSerializer.Deserialize<WorkflowExecutionFrame>(saved, WorkflowDefinitionCodec.JsonOptions)!;
        WorkflowExecutionFrame output = frame with { Route = null };
        if (WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.Decision))
        {
            WorkflowFactsSnapshot facts = Facts();
            DecisionTable table = node.Parameters.Deserialize<DecisionTable>(WorkflowDefinitionCodec.JsonOptions)!;
            if (table.Rows.Count > 50 || table.Rows.Any(row => row.All.Count > 8)) throw new WorkflowExecutionException("DecisionLimitExceeded");
            DecisionEvaluation evaluated = facts.BlockReason is { } reason
                ? new("NeedsManualReview", [], [], [], reason) : DecisionTableEvaluator.Evaluate(table, facts.Facts);
            step.DecisionTraceJson = JsonSerializer.Serialize(evaluated, WorkflowDefinitionCodec.JsonOptions);
            step.SelectedRoute = evaluated.Routes.SingleOrDefault();
            output = evaluated.Status == "Completed" ? output with { Route = step.SelectedRoute }
                : output with { Stopped = true, Outcome = evaluated.Status };
        }
        else if (WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.End))
        {
            string? outcome = node.Parameters.TryGetProperty("outcome", out JsonElement configured) && configured.ValueKind == JsonValueKind.String
                ? configured.GetString() : null;
            if (outcome is not (null or "Completed" or "NeedsManualReview" or "Blocked")) throw new WorkflowExecutionException("InvalidTerminalOutcome");
            output = output with { Outcome = outcome ?? frame.Outcome ?? "Completed" };
        }
        step.Status = output.Stopped ? "Blocked" : "Completed";
        step.ResultJson = JsonSerializer.Serialize(output, WorkflowDefinitionCodec.JsonOptions);
        step.CompletedAt = clock.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        return output;
    }

    public async Task<WorkflowStepWait> PrepareToolAsync(WorkflowDefinitionNode node, WorkflowExecutionFrame frame, CancellationToken cancellationToken)
    {
        await RequireActiveAsync(frame.RunId, cancellationToken);
        WorkflowStepRun step = await StepAsync(node.Key, cancellationToken);
        Run.PendingStepRunId = step.Id;
        if (step.Status is "Completed" or "Executing") return new(Run.Id, step.Id);
        WorkflowToolParameters parameters = node.Parameters.Deserialize<WorkflowToolParameters>(WorkflowDefinitionCodec.JsonOptions)
            ?? throw new WorkflowExecutionException("InvalidActionContract");
        IWorkflowActionHandler handler = actions.Resolve(parameters.ActionKey, parameters.ContractVersion);
        await authorization.RequireModuleAsync(handler.GetType(), cancellationToken);
        WorkflowFactsSnapshot facts = await UnchangedFactsAsync(cancellationToken);
        if (facts.BlockReason is { } blocked)
        {
            step.Status = "Blocked";
            step.ErrorCode = blocked;
            await database.SaveChangesAsync(cancellationToken);
            return new(Run.Id, step.Id);
        }
        WorkflowInputBinder.ValidateNames(parameters, handler.Descriptor);
        JsonElement arguments = WorkflowInputBinder.Bind(parameters, facts, await OutputsAsync(cancellationToken));
        handler.ValidateArguments(arguments);
        string hash = Hash(arguments);
        if (step.ArgumentsHash is { } previousHash && previousHash != hash) throw new WorkflowExecutionException("ActionArgumentsChanged");
        step.ActionKey = handler.Descriptor.Key;
        step.ActionVersion = handler.Descriptor.Version;
        step.RequiredPermission = handler.Descriptor.RequiredPermission;
        step.ArgumentsJson = arguments.GetRawText();
        step.ArgumentsHash = hash;
        step.FactsJson = JsonSerializer.Serialize(facts, WorkflowDefinitionCodec.JsonOptions);
        step.FactsHash = WorkflowFactsHash.Compute(facts);
        step.ExpectedSubjectVersion = facts.SubjectVersion;
        step.Status = "Prepared";
        await database.SaveChangesAsync(cancellationToken);
        if (handler.Descriptor.Approval is { } policy)
        {
            invocationContext.Step = step;
            try
            {
                ToolGateOutcome gated = await approvals.GateAsync(new(handler.Descriptor.ToolName, policy, Arguments(arguments),
                    new(policy.ActionKind ?? handler.Descriptor.Key,
                        JsonSerializer.SerializeToElement(new { subjectId = Run.SubjectId, facts = facts.Facts, definitionHash = Run.DefinitionHash }), arguments)), cancellationToken);
                step.Status = gated.Decision switch
                {
                    ToolGateDecision.ApprovedInline => "Ready",
                    ToolGateDecision.PendingApproval or ToolGateDecision.Queued => "WaitingForApproval",
                    _ => "Blocked",
                };
                if (step.Status == "Blocked") step.ErrorCode = "ApprovalRefused";
            }
            finally { invocationContext.Step = null; }
        }
        else step.Status = "Ready";
        await database.SaveChangesAsync(cancellationToken);
        return new(Run.Id, step.Id);
    }

    /// <returns>A server-derived response, or null while a durable approval is pending.</returns>
    public async Task<WorkflowExecutionFrame?> ReadyResponseAsync(WorkflowStepWait wait, CancellationToken cancellationToken)
    {
        await RequireActiveAsync(wait.RunId, cancellationToken, allowWaiting: true);
        WorkflowStepRun step = await database.WorkflowStepRuns.SingleAsync(step => step.RunId == Run.Id && step.Id == wait.StepRunId, cancellationToken);
        await database.Entry(step).ReloadAsync(cancellationToken);
        if (step.Status == "Blocked") return new(Run.Id, Outcome: step.ErrorCode ?? "Blocked", Stopped: true);
        if (step.Status == "Completed") return new(Run.Id, Outcome: ToolResult(step).Outcome);
        // A dispatched operation is reconciled before considering expiry or another invocation.
        if (step.Status == "Executing") return new(Run.Id);
        if (step.ProposalId is { } proposalId)
        {
            Proposal proposal = await database.Proposals.AsNoTracking().SingleAsync(proposal => proposal.Id == proposalId, cancellationToken);
            if (proposal.Status is ProposalStatus.Proposed or ProposalStatus.Approved) return null;
            if (proposal.Status == ProposalStatus.Applied) return new(Run.Id);
            step.Status = "Blocked";
            step.ErrorCode = proposal.Status == ProposalStatus.Failed ? "ProposalFailed" : "ApprovalRejected";
        }
        else if (step.ApprovalRequestId is { } requestId)
        {
            ToolApprovalRequest request = await database.ToolApprovalRequests.AsNoTracking().SingleAsync(request => request.Id == requestId, cancellationToken);
            if (request.Status == ToolApprovalRequestStatus.Pending && request.ExpiresAt > clock.UtcNow) return null;
            if (request.Status == ToolApprovalRequestStatus.Approved && (request.RiskTier == "Low" || request.ExpiresAt > clock.UtcNow)) return new(Run.Id);
            step.Status = "Blocked";
            step.ErrorCode = request.Status == ToolApprovalRequestStatus.Rejected ? "ApprovalRejected" : "ApprovalExpired";
        }
        else return new(Run.Id);
        await database.SaveChangesAsync(cancellationToken);
        return new(Run.Id, Outcome: step.ErrorCode, Stopped: true);
    }

    public async Task<WorkflowExecutionFrame> ExecuteToolAsync(WorkflowDefinitionNode node, WorkflowExecutionFrame frame, CancellationToken cancellationToken)
    {
        await RequireActiveAsync(frame.RunId, cancellationToken);
        if (frame.Stopped) return frame;
        WorkflowStepRun step = await StepAsync(node.Key, cancellationToken);
        if (step.Status == "Completed") return new(Run.Id, Outcome: ToolResult(step).Outcome);
        IWorkflowActionHandler handler = actions.Resolve(step.ActionKey!, step.ActionVersion!.Value);
        await authorization.RequireModuleAsync(handler.GetType(), cancellationToken);
        WorkflowActionInvocation invocation = Invocation(step);
        if (step.ProposalId is { } proposalId)
        {
            Proposal proposal = await database.Proposals.AsNoTracking().SingleAsync(proposal => proposal.Id == proposalId, cancellationToken);
            if (proposal.Status != ProposalStatus.Applied || proposal.ExecutionResultJson is null) throw new WorkflowExecutionException("ProposalNotApplied");
            // High effects ran only through their typed proposal handler, never through this worker.
            var receipt = JsonSerializer.Deserialize<ProposalHandlerResult>(proposal.ExecutionResultJson, WorkflowDefinitionCodec.JsonOptions)!;
            return await CompleteToolAsync(step, new("Applied", JsonSerializer.SerializeToElement(receipt, WorkflowDefinitionCodec.JsonOptions)), cancellationToken);
        }
        if (step.Status == "Executing")
        {
            WorkflowActionReconciliation recovered = await handler.ReconcileAsync(invocation, cancellationToken);
            if (recovered.Result is { } receipt) return await CompleteToolAsync(step, receipt, cancellationToken);
            if (!recovered.CanRetry) throw new WorkflowExecutionException("ActionOutcomeUnknown");
        }
        else await UnchangedFactsAsync(cancellationToken);
        invocationContext.Step = step;
        try
        {
            if (handler.Descriptor.Approval is { } policy)
            {
                ToolGateOutcome gated = await approvals.GateAsync(new(handler.Descriptor.ToolName, policy,
                    Arguments(invocation.Arguments)), cancellationToken);
                if (gated.Decision != ToolGateDecision.ApprovedInline) throw new WorkflowExecutionException("ActionNotApproved");
            }
            step.Status = "Executing";
            step.Attempts++;
            await database.SaveChangesAsync(cancellationToken);
            WorkflowActionResult result = await handler.ExecuteAsync(invocation, cancellationToken);
            return await CompleteToolAsync(step, result, cancellationToken);
        }
        finally { invocationContext.Step = null; }
    }

    public async Task RequireActiveAsync(Guid runId, CancellationToken cancellationToken, bool allowWaiting = false)
    {
        if (runId != Run.Id) throw new WorkflowExecutionException("RunScopeMismatch");
        await database.Entry(Run).ReloadAsync(cancellationToken);
        if (Run.LeaseId != invocationContext.LeaseId || Run.LeaseExpiresAt <= clock.UtcNow) throw new WorkflowExecutionException("RunLeaseLost");
        if (Run.Status == WorkflowRunStatuses.Cancelled) throw new WorkflowExecutionException("RunCancelled");
        if (Run.Status != WorkflowRunStatuses.Running && !(allowWaiting && Run.Status == WorkflowRunStatuses.WaitingForApproval))
            throw new WorkflowExecutionException("RunNotRunning");
        bool retired = await database.Workflows.AsNoTracking().AnyAsync(workflow => workflow.Id == Run.WorkflowId && workflow.RetiredAt != null, cancellationToken);
        if (retired) throw new WorkflowExecutionException("WorkflowRetired");
        await authorization.RequireActorAsync(WorkflowExecutionAuthorization.Actor(Run), cancellationToken);
        await authorization.RequireEnabledAsync(cancellationToken);
    }

    public async Task ReconcileCancelledAsync(CancellationToken cancellationToken)
    {
        if (Run.Status != WorkflowRunStatuses.Cancelled || Run.LeaseId != invocationContext.LeaseId
            || Run.PendingStepRunId is not { } pending) return;
        WorkflowStepRun? step = await database.WorkflowStepRuns.SingleOrDefaultAsync(step => step.TenantId == Run.TenantId
            && step.RunId == Run.Id && step.Id == pending && step.Status == "Executing", cancellationToken);
        if (step is null) return;
        try
        {
            WorkflowActionReconciliation recovered = await actions.Resolve(step.ActionKey!, step.ActionVersion!.Value)
                .ReconcileAsync(Invocation(step), cancellationToken);
            if (recovered.Result is { } receipt)
            {
                step.ResultJson = JsonSerializer.Serialize(receipt, WorkflowDefinitionCodec.JsonOptions);
                step.Status = "Completed";
                step.CompletedAt = clock.UtcNow;
                step.ErrorCode = null;
                Run.PendingStepRunId = null;
            }
            else
            {
                step.Status = recovered.CanRetry ? "Cancelled" : "NeedsAttention";
                step.ErrorCode = recovered.CanRetry ? "CancelledBeforeEffect" : "ActionOutcomeUnknown";
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            step.Status = "NeedsAttention";
            step.ErrorCode = "ReconciliationUnavailable";
        }
        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task<WorkflowStepRun> StepAsync(string nodeKey, CancellationToken cancellationToken)
    {
        WorkflowStepRun? step = await database.WorkflowStepRuns.SingleOrDefaultAsync(step => step.RunId == Run.Id && step.NodeKey == nodeKey
            && step.InvocationPath == "root", cancellationToken);
        if (step is not null) return step;
        step = new() { TenantId = Run.TenantId, RunId = Run.Id, NodeKey = nodeKey, StartedAt = clock.UtcNow,
            FactsJson = Run.FactsJson, FactsHash = Run.FactsHash, AiRunId = invocationContext.OriginatingAiRunId };
        step.OperationId = step.Id;
        database.WorkflowStepRuns.Add(step);
        Run.SequenceJson = JsonSerializer.Serialize(WorkflowService.ParseGuidArray(Run.SequenceJson).Append(step.Id));
        await database.SaveChangesAsync(cancellationToken);
        return step;
    }

    private WorkflowFactsSnapshot Facts() => JsonSerializer.Deserialize<WorkflowFactsSnapshot>(Run.FactsJson!, WorkflowDefinitionCodec.JsonOptions)!;
    private async Task<WorkflowFactsSnapshot> UnchangedFactsAsync(CancellationToken cancellationToken)
    {
        WorkflowFactsSnapshot current = await authorization.LoadFactsAsync(Run.SubjectType!, Run.SubjectId!.Value,
            WorkflowExecutionAuthorization.Actor(Run), cancellationToken);
        if (WorkflowFactsHash.Compute(current) != Run.FactsHash) throw new WorkflowExecutionException("SubjectChanged");
        return current;
    }

    private async Task<IReadOnlyDictionary<string, WorkflowActionResult>> OutputsAsync(CancellationToken cancellationToken)
        => (await database.WorkflowStepRuns.AsNoTracking().Where(step => step.RunId == Run.Id && step.ActionKey != null
                && step.Status == "Completed" && step.ResultJson != null).ToListAsync(cancellationToken))
            .ToDictionary(step => step.NodeKey, ToolResult, StringComparer.Ordinal);

    private WorkflowActionInvocation Invocation(WorkflowStepRun step) => new(Run.Id, step.Id, step.OperationId,
        WorkflowExecutionAuthorization.Actor(Run), Run.SubjectType!, Run.SubjectId!.Value, step.ExpectedSubjectVersion!, step.FactsHash!,
        JsonSerializer.Deserialize<JsonElement>(step.ArgumentsJson!), step.ApprovalRequestId);
    private static WorkflowActionResult ToolResult(WorkflowStepRun step) => JsonSerializer.Deserialize<WorkflowActionResult>(step.ResultJson!, WorkflowDefinitionCodec.JsonOptions)!;
    private static Dictionary<string, object?> Arguments(JsonElement value) => value.EnumerateObject().ToDictionary(property => property.Name, property => (object?)property.Value);
    private static string Hash(JsonElement arguments) => ToolArgumentsHash.Compute(Arguments(arguments));

    private async Task<WorkflowExecutionFrame> CompleteToolAsync(WorkflowStepRun step, WorkflowActionResult result, CancellationToken cancellationToken)
    {
        await database.Entry(Run).ReloadAsync(cancellationToken);
        if (Run.LeaseId != invocationContext.LeaseId) throw new WorkflowExecutionException("RunLeaseLost");
        step.Status = "Completed";
        step.CompletedAt = clock.UtcNow;
        step.ResultJson = JsonSerializer.Serialize(result, WorkflowDefinitionCodec.JsonOptions);
        Run.PendingStepRunId = null;
        if (Run.Status != WorkflowRunStatuses.Cancelled)
        {
            WorkflowFactsSnapshot current = await authorization.LoadFactsAsync(Run.SubjectType!, Run.SubjectId!.Value,
                WorkflowExecutionAuthorization.Actor(Run), cancellationToken);
            Run.FactsJson = JsonSerializer.Serialize(current, WorkflowDefinitionCodec.JsonOptions);
            Run.FactsHash = WorkflowFactsHash.Compute(current);
        }
        await database.SaveChangesAsync(cancellationToken);
        return new(Run.Id, Outcome: result.Outcome, Stopped: Run.Status == WorkflowRunStatuses.Cancelled);
    }
}
