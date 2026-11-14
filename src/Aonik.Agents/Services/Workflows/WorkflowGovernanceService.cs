using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Framework;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;

namespace Aonik.Agents.Services.Workflows;

internal sealed class WorkflowGovernanceService(
    AgentsDbContext database, WorkflowAuthorization authorization,
    WorkflowGovernancePreparation preparation, WorkflowApprovalAuthority authority,
    IToolApprovalService approvals, IClock clock) : IWorkflowGovernanceService
{
    public async Task<WorkflowGovernanceRequestResult> RequestAsync(
        WorkflowGovernanceCommand command, bool retiring, CancellationToken cancellationToken = default)
    {
        PreparedWorkflowGovernance prepared = await preparation.PrepareAsync(command, retiring, cancellationToken);
        var context = new ToolGateContext(retiring ? "aonik_request_retire_workflow" : "aonik_request_publish_workflow",
            WorkflowToolApprovalManifest.PublicationOptions(retiring),
            new Dictionary<string, object?> { ["command"] = JsonSerializer.SerializeToElement(prepared.Command, WorkflowDefinitionCodec.JsonOptions) },
            prepared.Preview);
        ToolGateOutcome outcome = await approvals.GateAsync(context, cancellationToken);
        if (outcome.Decision != ToolGateDecision.Queued || !outcome.ProposalId.HasValue || !outcome.ApprovalRequestId.HasValue)
            throw new InvalidOperationException("The governance request could not be queued for approval.");
        return new(outcome.ProposalId.Value, outcome.ApprovalRequestId.Value, prepared.Preview);
    }

    internal async Task<ProposalHandlerResult> ApplyAsync(Guid proposalId, bool retiring, CancellationToken cancellationToken)
    {
        Guid actorId = await authorization.RequireAsync("Workflow.Publish", cancellationToken);
        Proposal proposal = await database.Proposals.SingleOrDefaultAsync(
            proposal => proposal.TenantId == authorization.TenantId && proposal.Id == proposalId, cancellationToken)
            ?? throw new KeyNotFoundException("Proposal not found.");
        string expectedType = retiring ? WorkflowToolApprovalManifest.RetireType : WorkflowToolApprovalManifest.PublishType;
        if (proposal.ProposalType == expectedType && proposal.Status == ProposalStatus.Applied && proposal.ExecutionResultJson is { } appliedJson
            && await authority.CanDecideProposalAsync(proposal, cancellationToken))
            return JsonSerializer.Deserialize<ProposalHandlerResult>(appliedJson, WorkflowDefinitionCodec.JsonOptions)
                ?? throw new InvalidOperationException("The recorded governance result is invalid.");
        if (proposal.ProposalType != expectedType || proposal.Status != ProposalStatus.Approved
            || proposal.ApprovedByUserId != actorId || !proposal.ApprovedAt.HasValue
            || !await authority.CanDecideProposalAsync(proposal, cancellationToken))
            throw new PermissionDeniedException("Workflow.Publish", "An authorized human must approve this exact governance proposal.");
        ToolApprovalRequest? request = await database.ToolApprovalRequests.AsNoTracking()
            .SingleOrDefaultAsync(request => request.TenantId == proposal.TenantId && request.ProposalId == proposal.Id, cancellationToken);
        if (request is null || request.RequestingUserId is not { } originator || request.ExpiresAt <= clock.UtcNow
            || request.Status is ToolApprovalRequestStatus.Rejected or ToolApprovalRequestStatus.Expired
            || !await authority.IsPublisherAsync(proposal.TenantId, originator, cancellationToken))
            return new(false, Message: "The original request expired or its author no longer has publication authority.");
        JsonElement payload = JsonSerializer.Deserialize<JsonElement>(proposal.PayloadJson);
        WorkflowGovernanceCommand command = payload.GetProperty("command").Deserialize<WorkflowGovernanceCommand>(WorkflowDefinitionCodec.JsonOptions)
            ?? throw new ArgumentException("Invalid workflow governance payload.");
        PreparedWorkflowGovernance prepared = await preparation.PrepareAsync(command, retiring, cancellationToken);
        Workflow workflow = await database.Workflows.SingleAsync(workflow => workflow.Id == prepared.WorkflowId && workflow.TenantId == proposal.TenantId, cancellationToken);
        WorkflowService.CheckVersion(workflow, command.ExpectedVersion);
        if (workflow.PublishedVersionId != command.ExpectedPublishedVersionId)
            throw new WorkflowConflictException("The published pointer changed before approval could apply.");
        if (retiring)
        {
            workflow.State = WorkflowStates.Retired;
            workflow.RetiredAt = clock.UtcNow;
            workflow.RetiredByUserId = actorId;
        }
        else
        {
            WorkflowVersion? target = prepared.TargetVersionId is { } targetId
                ? await database.WorkflowVersions.SingleAsync(version => version.Id == targetId && version.TenantId == proposal.TenantId, cancellationToken)
                : null;
            if (target is null)
            {
                string tag = await WorkflowService.NextAvailableVersionAsync(database, workflow, cancellationToken);
                string json = WorkflowDefinitionCodec.Serialize(prepared.Definition!);
                target = new WorkflowVersion
                {
                    TenantId = workflow.TenantId, WorkflowId = workflow.Id, Tag = tag,
                    DefinitionJson = json, DefinitionHash = WorkflowDefinitionCodec.Hash(json), SchemaVersion = prepared.Definition!.SchemaVersion,
                    AuthorName = originator.ToString("D"), Message = "Published from a validated legacy draft.",
                };
                database.WorkflowVersions.Add(target);
                workflow.Version = tag;
            }
            bool firstPublication = target.PublishedAt is null;
            target.PublishedAt ??= clock.UtcNow;
            target.PublishedByUserId ??= actorId;
            if (firstPublication && proposal.AiRunId != Guid.Empty) target.AiRunId ??= proposal.AiRunId;
            workflow.PublishedVersionId = target.Id;
            workflow.State = WorkflowStates.Active;
            workflow.RetiredAt = null;
            workflow.RetiredByUserId = null;
        }
        var result = new ProposalHandlerResult(true, retiring ? "Workflow" : "WorkflowVersion", retiring ? workflow.Id : workflow.PublishedVersionId);
        // These records share the Agents context: persist governance and its execution receipt atomically.
        proposal.Status = ProposalStatus.Applied;
        proposal.ExecutionResultJson = JsonSerializer.Serialize(result, WorkflowDefinitionCodec.JsonOptions);
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new WorkflowConflictException("The workflow changed while approval was applying."); }
        return result;
    }
}
