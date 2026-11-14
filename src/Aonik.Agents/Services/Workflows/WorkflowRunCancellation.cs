using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Entities;
using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;
using Aonik.Agents.Workflows.Graph;
using Aonik.SharedKernel.Events.Integration;

namespace Aonik.Agents.Services.Workflows;

internal static class WorkflowRunCancellation
{
    public static async Task StageAsync(AgentsDbContext database, WorkflowRun run, string reason, DateTime now, CancellationToken cancellationToken)
    {
        List<WorkflowRun> children = await database.WorkflowRuns.Where(child => child.TenantId == run.TenantId && child.ParentRunId == run.Id
            && child.Status != WorkflowRunStatuses.Completed && child.Status != WorkflowRunStatuses.Failed).ToListAsync(cancellationToken);
        foreach (WorkflowRun child in children)
        {
            child.Status = WorkflowRunStatuses.Cancelled; child.Outcome = "Cancelled"; child.ErrorCode = reason; child.CompletedAt ??= now;
            if (child.Depth != run.Depth + 1 || child.Depth > WorkflowControlLimits.MaximumNestingDepth) throw new WorkflowExecutionException("NestingLimitExceeded");
            await StageAsync(database, child, reason, now, cancellationToken);
            if (await database.WorkflowStepRuns.AnyAsync(step => step.TenantId == run.TenantId && step.RunId == child.Id && step.Status == "Executing", cancellationToken))
                database.EnqueueIntegrationEvent(new WorkflowContinuationRequestedEvent(Guid.NewGuid(), now, run.TenantId, child.Id, child.Continuation,
                    DateTime.SpecifyKind(child.LeaseExpiresAt is { } lease && lease > now ? lease : now, DateTimeKind.Utc)));
        }
        List<WorkflowStepRun> waiting = await database.WorkflowStepRuns.Where(step => step.TenantId == run.TenantId && step.RunId == run.Id
            && step.Status != "Completed" && step.Status != "Executing" && step.Status != "NeedsAttention").ToListAsync(cancellationToken);
        foreach (WorkflowStepRun step in waiting)
        {
            step.Status = "Cancelled";
            step.ErrorCode = reason;
            step.CompletedAt = now;
        }
        Guid[] stepIds = waiting.Select(step => step.Id).ToArray();
        List<ToolApprovalRequest> requests = await database.ToolApprovalRequests.Where(request => request.TenantId == run.TenantId
            && request.WorkflowStepRunId.HasValue && stepIds.Contains(request.WorkflowStepRunId.Value) && request.ConsumedAt == null
            && (request.Status == ToolApprovalRequestStatus.Pending || request.Status == ToolApprovalRequestStatus.Approved)).ToListAsync(cancellationToken);
        foreach (ToolApprovalRequest request in requests) request.Status = ToolApprovalRequestStatus.Cancelled;
    }
}
