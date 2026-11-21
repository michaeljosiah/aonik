using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Services.Workflows;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Agents.Workflows.Graph;

internal sealed partial class WorkflowStepExecutionService
{
    public Task<WorkflowExecutionFrame> PrepareParallelAsync(WorkflowDefinitionNode node, WorkflowExecutionFrame frame, CancellationToken cancellationToken)
        => SerializeControlAsync<WorkflowExecutionFrame>(async () =>
        {
            await RequireActiveAsync(frame.RunId, cancellationToken);
            WorkflowParallelParameters parameters = WorkflowControlContracts.Parallel(node);
            WorkflowStepRun step = await StepAsync(node.Key, cancellationToken);
            if (step.Status == "Completed") return new(Run.Id, step.SelectedRoute, ToolResult(step).Outcome, Failed: ToolResult(step).Outcome != "Completed");
            if (step.ArgumentsJson is null)
            {
                step.ActionKey = WorkflowControlContracts.ParallelActionKey; step.ActionVersion = 1; step.Status = "Joining";
                step.ArgumentsJson = JsonSerializer.Serialize(parameters, WorkflowDefinitionCodec.JsonOptions);
                step.ArgumentsHash = Hash(JsonSerializer.Deserialize<JsonElement>(step.ArgumentsJson));
                await database.SaveChangesAsync(cancellationToken);
            }
            return new(Run.Id, Route: "next");
        });

    public Task<WorkflowStepWait> PrepareBranchAsync(WorkflowDefinitionNode node, string branchKey, WorkflowExecutionFrame frame, CancellationToken cancellationToken)
        => SerializeControlAsync(async () =>
        {
            await RequireActiveAsync(frame.RunId, cancellationToken);
            WorkflowParallelParameters parameters = WorkflowControlContracts.Parallel(node);
            WorkflowParallelBranch branch = parameters.Branches.Single(branch => branch.Key == branchKey);
            WorkflowStepRun owner = await StepAsync(node.Key, cancellationToken);
            return await PrepareChildCallAsync(new(node.Key, $"root/{branchKey}", branch.Workflow, branch.InvoiceId,
                "branch-completed", "branch-error", Run.DeadlineUtc, owner.Id, branchKey), frame, cancellationToken);
        });

    public Task<WorkflowExecutionFrame> CompleteBranchAsync(WorkflowDefinitionNode node, string branchKey, WorkflowExecutionFrame frame, CancellationToken cancellationToken)
        => SerializeControlAsync(async () =>
        {
            await RequireActiveAsync(frame.RunId, cancellationToken);
            WorkflowStepRun step = await StepAsync(node.Key, cancellationToken, $"root/{branchKey}");
            if (frame.ResumedStepRunId != step.Id) throw new WorkflowExecutionException("BranchWaitIdentityMismatch");
            return await CompleteChildCallAsync(step, cancellationToken);
        });

    public Task<WorkflowExecutionFrame?> JoinParallelBatchAsync(WorkflowDefinitionNode node, IReadOnlyList<string> branchKeys, bool lastBatch,
        CancellationToken cancellationToken) => SerializeControlAsync<WorkflowExecutionFrame?>(async () =>
    {
        await RequireActiveAsync(Run.Id, cancellationToken);
        WorkflowParallelParameters parameters = WorkflowControlContracts.Parallel(node);
        WorkflowStepRun owner = await StepAsync(node.Key, cancellationToken);
        if (owner.Status == "Completed") return new(Run.Id, owner.SelectedRoute, ToolResult(owner).Outcome, Failed: ToolResult(owner).Outcome != "Completed");
        string[] paths = branchKeys.Select(key => $"root/{key}").ToArray();
        List<WorkflowStepRun> calls = await database.WorkflowStepRuns.Where(step => step.TenantId == Run.TenantId && step.RunId == Run.Id
            && step.NodeKey == node.Key && paths.Contains(step.InvocationPath)).ToListAsync(cancellationToken);
        Guid[] callIds = calls.Select(call => call.Id).ToArray();
        List<WorkflowRun> children = await database.WorkflowRuns.AsNoTracking().Where(child => child.TenantId == Run.TenantId && child.ParentRunId == Run.Id
            && child.ParentStepRunId.HasValue && callIds.Contains(child.ParentStepRunId.Value)).ToListAsync(cancellationToken);
        if (calls.Count != branchKeys.Count || children.Count != branchKeys.Count) throw new WorkflowExecutionException("ParallelBranchIdentityMissing");
        string? winner = null;
        bool success;
        if (parameters.JoinPolicy == "AnySuccess")
        {
            WorkflowRun? won = children.Where(ChildSucceeded).OrderBy(child => child.CompletedAt)
                .ThenBy(child => calls.Single(call => call.Id == child.ParentStepRunId).InvocationPath, StringComparer.Ordinal).FirstOrDefault();
            if (won is null)
            {
                if (children.Any(child => !IsTerminal(child))) return null;
                if (!lastBatch) return new(Run.Id, Route: "next");
                success = false;
            }
            else
            {
                WorkflowStepRun wonCall = calls.Single(call => call.Id == won.ParentStepRunId);
                winner = wonCall.InvocationPath["root/".Length..];
                _ = await CompleteChildCallAsync(wonCall, cancellationToken);
                foreach (WorkflowRun other in children.Where(child => child.Id != won.Id && !IsTerminal(child)))
                    _ = await CancelChildAsync(other.Id, "AnySuccessCompleted", cancellationToken);
                success = true;
            }
        }
        else
        {
            if (children.Any(child => !IsTerminal(child))) return null;
            success = children.All(ChildSucceeded);
            if (success && !lastBatch) return new(Run.Id, Route: "next");
        }
        List<WorkflowStepRun> allCalls = await database.WorkflowStepRuns.AsNoTracking().Where(step => step.TenantId == Run.TenantId && step.RunId == Run.Id
            && step.NodeKey == node.Key && step.InvocationPath != "root").ToListAsync(cancellationToken);
        Guid[] allIds = allCalls.Select(call => call.Id).ToArray();
        List<WorkflowRun> allChildren = await database.WorkflowRuns.AsNoTracking().Where(child => child.TenantId == Run.TenantId && child.ParentRunId == Run.Id
            && child.ParentStepRunId.HasValue && allIds.Contains(child.ParentStepRunId.Value)).ToListAsync(cancellationToken);
        var outputs = new SortedDictionary<string, object>(StringComparer.Ordinal);
        foreach (WorkflowParallelBranch branch in parameters.Branches)
        {
            WorkflowStepRun? call = allCalls.SingleOrDefault(call => call.InvocationPath == $"root/{branch.Key}");
            WorkflowRun? child = allChildren.SingleOrDefault(child => child.ParentStepRunId == call?.Id);
            outputs.Add(branch.Key, new { childRunId = child?.Id, status = child?.Status ?? "NotStarted", outcome = child?.Outcome });
        }
        string outcome = success ? "Completed" : "NeedsManualReview";
        owner.Status = "Completed"; owner.CompletedAt = clock.UtcNow; owner.SelectedRoute = success ? parameters.CompletedRoute : parameters.ErrorRoute;
        owner.ResultJson = JsonSerializer.Serialize(new WorkflowActionResult(outcome, JsonSerializer.SerializeToElement(new { branches = outputs, winner }, WorkflowDefinitionCodec.JsonOptions)), WorkflowDefinitionCodec.JsonOptions);
        await database.SaveChangesAsync(cancellationToken);
        return new(Run.Id, owner.SelectedRoute, outcome, Failed: !success);
    });
}
