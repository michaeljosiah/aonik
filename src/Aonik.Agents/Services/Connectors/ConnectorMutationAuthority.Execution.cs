using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Entities;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Framework;
using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Agents.Services.Connectors;

internal sealed partial class ConnectorMutationAuthority
{
    public async Task CompleteAsync(Guid approvalRequestId, Guid leaseId, JsonElement result, CancellationToken cancellationToken = default)
    {
        ToolApprovalRequest request = await RequestAsync(approvalRequestId, cancellationToken);
        await database.Entry(request).ReloadAsync(cancellationToken);
        ConnectorMutationJournal journal = Journal(request.ExecutionJson ?? throw Denied());
        await ValidateReceiptAsync(request, journal, result, cancellationToken);
        if (journal.Status == "Completed") return;
        if (journal.LeaseId != leaseId || journal.Status != "InFlight") throw Denied();
        request.ExecutionJson = ConnectorJson.Serialize(journal with { Status = "Completed", Result = result, CompletedAtUtc = clock.UtcNow, ErrorCode = null });
        await SaveClaimAsync(cancellationToken);
    }

    public async Task HoldAsync(Guid approvalRequestId, Guid leaseId, string code, CancellationToken cancellationToken = default)
    {
        ToolApprovalRequest request = await RequestAsync(approvalRequestId, cancellationToken);
        await database.Entry(request).ReloadAsync(cancellationToken);
        ConnectorMutationJournal journal = Journal(request.ExecutionJson ?? throw Denied());
        if (journal.Status == "Completed") return;
        if (journal.LeaseId != leaseId || journal.Status != "InFlight") throw Denied();
        request.ExecutionJson = ConnectorJson.Serialize(journal with { Status = "NeedsAttention", ErrorCode = code, LeaseExpiresAtUtc = clock.UtcNow });
        await SaveClaimAsync(cancellationToken);
    }

    public async Task<ConnectorMutationStatus?> GetStatusAsync(Guid approvalRequestId, CancellationToken cancellationToken = default)
    {
        ToolApprovalRequest? request = await database.ToolApprovalRequests.AsNoTracking().SingleOrDefaultAsync(request => request.TenantId == TenantId && request.Id == approvalRequestId, cancellationToken);
        if (request is null || !ConnectorToolApprovalManifest.IsConnectorMutation(request.ToolName)) return null;
        await RequireStatusAccessAsync(request, cancellationToken);
        ConnectorApprovedProposal binding = await BindingAsync(request, cancellationToken);
        ConnectorMutationJournal? journal = request.ExecutionJson is null ? null : Journal(request.ExecutionJson);
        return new(request.Id, request.ProposalId, binding.ConnectionId, binding.Action,
            journal is null ? request.Status.ToString() : journal.Status == "InFlight" && journal.LeaseExpiresAtUtc <= clock.UtcNow ? "NeedsAttention" : journal.Status,
            journal?.Result, journal?.ErrorCode);
    }

    public async Task<ConnectorMutationExecutionRequest> RequireReconciliationAsync(Guid approvalRequestId, CancellationToken cancellationToken = default)
    {
        ToolApprovalRequest request = await RequestAsync(approvalRequestId, cancellationToken);
        await RequireStatusAccessAsync(request, cancellationToken);
        ConnectorMutationJournal journal = Journal(request.ExecutionJson ?? throw Denied());
        if (journal.Status == "InFlight" && journal.LeaseExpiresAtUtc > clock.UtcNow) throw Denied();
        ConnectorApprovedProposal binding = await BindingAsync(request, cancellationToken);
        if (journal.ConnectionId != binding.ConnectionId || journal.Action != binding.Action || journal.ArgumentsHash != Hash(binding.Arguments)) throw Denied();
        if (binding.Context.RunId is { } runId)
        {
            WorkflowRun run = await database.WorkflowRuns.AsNoTracking().SingleAsync(run => run.TenantId == TenantId && run.Id == runId, cancellationToken);
            await execution.RequireRunAsync(run, cancellationToken);
            await execution.RequireEnabledAsync(cancellationToken);
        }
        return new(binding.ConnectionId, journal.ProviderAccountId, binding.Action, binding.Arguments,
            binding.Context.RunId, binding.Context.StepRunId, request.ProposalId, binding.Context, await OperationIdAsync(request, cancellationToken));
    }

    public async Task RecordReconciliationAsync(Guid approvalRequestId, JsonElement result, CancellationToken cancellationToken = default)
    {
        await RequireReconciliationAsync(approvalRequestId, cancellationToken);
        ToolApprovalRequest request = await RequestAsync(approvalRequestId, cancellationToken);
        await database.Entry(request).ReloadAsync(cancellationToken);
        ConnectorMutationJournal journal = Journal(request.ExecutionJson ?? throw Denied());
        await ValidateReceiptAsync(request, journal, result, cancellationToken);
        if (journal.Status == "Completed") return;
        if (RequireContract(journal.Action).Curated is not null || !ConnectorJson.Read<GmailMutationReceipt>(result).Reconciled) throw Denied();
        request.ExecutionJson = ConnectorJson.Serialize(journal with { Status = "Completed", Result = result, CompletedAtUtc = clock.UtcNow, ErrorCode = null });
        await SaveClaimAsync(cancellationToken);
    }

    private async Task RequireStatusAccessAsync(ToolApprovalRequest request, CancellationToken cancellationToken)
    {
        if (request.WorkflowStepRunId is { } stepId && workflowContext.Run is { } ambientRun && workflowContext.Step?.Id == stepId && workflowContext.LeaseId != Guid.Empty)
        {
            WorkflowRun run = await database.WorkflowRuns.AsNoTracking().SingleOrDefaultAsync(run => run.TenantId == TenantId && run.Id == ambientRun.Id, cancellationToken) ?? throw Denied();
            if (run.Status is not (WorkflowRunStatuses.Running or WorkflowRunStatuses.WaitingForApproval) || run.PendingStepRunId != stepId || run.LeaseId != workflowContext.LeaseId || run.LeaseExpiresAt <= clock.UtcNow) throw Denied();
            await execution.RequireRunAsync(run, cancellationToken); return;
        }
        Guid userId = await RequireUserAsync(cancellationToken);
        if (request.RequestingUserId != userId && request.DecidedByUserId != userId) throw Denied();
        if (request.WorkflowStepRunId.HasValue && !await permissions.HasPermissionAsync(userId, "Workflow.Read", cancellationToken)) throw Denied();
        if (!request.WorkflowStepRunId.HasValue) await RequireRoomAsync(request, cancellationToken);
    }

    private async Task ValidateReceiptAsync(ToolApprovalRequest request, ConnectorMutationJournal journal, JsonElement result, CancellationToken cancellationToken)
    {
        if (RequireContract(journal.Action).Curated is { } contract)
        {
            contract.RequireOutput(result);
            return;
        }
        if (result.GetRawText().Length > 32_768) throw Denied();
        GmailMutationReceipt receipt = ConnectorJson.Read<GmailMutationReceipt>(result);
        if (receipt.ActionKey != journal.Action.Key || receipt.OperationId != await OperationIdAsync(request, cancellationToken) || receipt.Status != "Completed") throw Denied();
    }
}
