using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Entities;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;

namespace Aonik.Agents.Services.Workflows;

internal static class WorkflowRunCancellation
{
    public static async Task StageAsync(AgentsDbContext database, WorkflowRun run, string reason, DateTime now, CancellationToken cancellationToken)
    {
        List<WorkflowStepRun> waiting = await database.WorkflowStepRuns.Where(step => step.TenantId == run.TenantId && step.RunId == run.Id
            && step.Status != "Completed" && step.Status != "Executing").ToListAsync(cancellationToken);
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
