using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Events;
using Aonik.SharedKernel.Events.Integration;

namespace Aonik.Agents.Services.Workflows;

/// <summary>Remove copied source previews and cancel remaining work through the existing document erasure outbox.</summary>
internal sealed class FinancialSourceErasedHandler(AgentsDbContext database, ITenantProvider tenantProvider, IClock clock)
    : IEventHandler<DocumentDeletedEvent>
{
    public async Task HandleAsync(DocumentDeletedEvent message, CancellationToken cancellationToken = default)
    {
        if (message.TenantId != tenantProvider.GetCurrentTenantId() || message.DocumentId == Guid.Empty)
            throw new PermissionDeniedException("Document.Delete");
        Guid[] roots = await database.WorkflowRuns.AsNoTracking().Where(run => run.TenantId == message.TenantId
            && run.Mode == "Live" && run.SubjectType == "Document" && run.SubjectId == message.DocumentId)
            .Select(run => run.ExecutionRootRunId ?? run.Id).Distinct().ToArrayAsync(cancellationToken);
        if (roots.Length == 0) return;
        var runs = await database.WorkflowRuns.Where(run => run.TenantId == message.TenantId
            && (roots.Contains(run.Id) || (run.ExecutionRootRunId != null && roots.Contains(run.ExecutionRootRunId.Value))))
            .ToListAsync(cancellationToken);
        foreach (var root in runs.Where(run => roots.Contains(run.Id) && run.Status is not (WorkflowRunStatuses.Completed or WorkflowRunStatuses.Failed or WorkflowRunStatuses.Cancelled)))
        {
            root.Status = WorkflowRunStatuses.Cancelled; root.Outcome = "Cancelled"; root.ErrorCode = "FinancialSourceErased";
            root.CompletedAt = clock.UtcNow; root.Continuation++;
            await WorkflowRunCancellation.StageAsync(database, root, "FinancialSourceErased", clock.UtcNow, cancellationToken);
        }
        Guid[] ids = runs.Select(run => run.Id).ToArray();
        var stepIds = database.WorkflowStepRuns.Where(step => step.TenantId == message.TenantId && ids.Contains(step.RunId)).Select(step => step.Id);
        var requests = await database.ToolApprovalRequests.Where(request => request.TenantId == message.TenantId
            && request.WorkflowStepRunId != null && stepIds.Contains(request.WorkflowStepRunId.Value)).ToListAsync(cancellationToken);
        foreach (var request in requests) request.PreviewJson = null;
        await database.SaveChangesAsync(cancellationToken);
    }
}
