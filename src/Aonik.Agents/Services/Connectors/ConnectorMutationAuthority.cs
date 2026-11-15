using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;
using Aonik.Agents.Services.Connectors.Catalog;
using Aonik.Agents.Services.Workflows;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;

namespace Aonik.Agents.Services.Connectors;

/// <summary>The existing approval row is the one durable dispatch claim and provider receipt journal.</summary>
internal sealed partial class ConnectorMutationAuthority(AgentsDbContext database, ITenantProvider tenantProvider,
    ICurrentUserProvider currentUser, IUserDirectoryReader users, IPermissionService permissions, IClock clock,
    WorkflowInvocationContext workflowContext, IWorkflowConnectorContextReader workflowConnections,
    WorkflowReviewerAuthorization reviewers, WorkflowExecutionAuthorization execution,
    ChatRoomInvocationGuard? roomContext = null, IChatRoomApprovalAuthority? roomAuthority = null,
    CuratedConnectorCatalog? curated = null) : IConnectorMutationAuthority
{
    private Guid TenantId => tenantProvider.GetCurrentTenantId();

    public async Task<ConnectorMutationPreview?> FindInvocationAsync(Guid connectionId, ConnectorActionRef action, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        Guid userId = await RequireUserAsync(cancellationToken);
        MutationContract contract = RequireContract(action);
        arguments = Normalize(contract, arguments);
        string key = ConnectorInvocationIdentity.Key(TenantId, userId, connectionId, action, ConnectorInvocationIdentity.IdempotencyKey(arguments));
        ToolApprovalRequest? request = await database.ToolApprovalRequests.AsNoTracking().SingleOrDefaultAsync(request => request.TenantId == TenantId && request.InvocationKey == key, cancellationToken);
        if (request is null) return null;
        if (request.RequestingUserId != userId || request.ThreadId != roomContext?.RoomId?.ToString() || request.WorkflowStepRunId.HasValue
            || request.ToolName != contract.ToolName || request.ArgsHash != ConnectorMutationArguments.Hash(connectionId, arguments)) throw Denied();
        await RequireRoomAsync(request, cancellationToken);
        ToolApprovalPreview preview = request.PreviewJson is not null ? ConnectorJson.Read<ToolApprovalPreview>(request.PreviewJson)
            : throw Denied();
        return new(preview, key);
    }

    public async Task<ConnectorApprovedProposal> RequireReviewAsync(Guid approvalRequestId, CancellationToken cancellationToken = default)
    {
        ToolApprovalRequest request = await RequestAsync(approvalRequestId, cancellationToken);
        if (request.Status != ToolApprovalRequestStatus.Pending || request.ExpiresAt <= clock.UtcNow) throw Denied();
        Guid userId = await RequireUserAsync(cancellationToken);
        if (request.WorkflowStepRunId.HasValue) await reviewers.RequireAsync(request, userId, cancellationToken);
        else
        {
            if (request.RequestingUserId != userId) throw Denied();
            await RequireRoomAsync(request, cancellationToken);
        }
        ConnectorApprovedProposal binding = await BindingAsync(request, cancellationToken);
        return binding with { Preview = request.PreviewJson is { } preview ? ConnectorJson.Read<ToolApprovalPreview>(preview) : throw Denied() };
    }

    public async Task<ConnectorApprovedProposal> RequireProposalAsync(Guid proposalId, CancellationToken cancellationToken = default)
    {
        Proposal proposal = await database.Proposals.AsNoTracking().SingleOrDefaultAsync(proposal => proposal.TenantId == TenantId && proposal.Id == proposalId, cancellationToken) ?? throw Denied();
        ToolApprovalRequest request = await database.ToolApprovalRequests.SingleOrDefaultAsync(request => request.TenantId == TenantId && request.ProposalId == proposalId, cancellationToken) ?? throw Denied();
        ConnectorApprovedProposal binding = await BindingAsync(request, cancellationToken);
        ToolApprovalOptions policy = RequireContract(binding.Action).Approval;
        if (policy.Tier != ToolApprovalTier.High || policy.ProposalType != proposal.ProposalType || proposal.RiskTier != "High"
            || proposal.Status is not (ProposalStatus.Approved or ProposalStatus.Applied) || proposal.ApprovedByUserId is not { } reviewer
            || proposal.ApprovedAt is not { } approved || approved >= request.ExpiresAt || approved < request.RequestedAt
            || proposal.WorkflowStepRunId != request.WorkflowStepRunId || proposal.PayloadJson != request.ArgumentsRedactedJson
            || request.Status is ToolApprovalRequestStatus.Rejected or ToolApprovalRequestStatus.Cancelled or ToolApprovalRequestStatus.Expired)
            throw Denied();
        await RequireUserAsync(reviewer, cancellationToken);
        if (request.WorkflowStepRunId.HasValue) await reviewers.RequireAsync(request, reviewer, cancellationToken);
        else
        {
            if (request.RequestingUserId != reviewer) throw Denied();
            await RequireRoomAsync(request, cancellationToken);
        }
        return binding;
    }

    public async Task<ConnectorMutationClaim> ClaimAsync(ConnectorMutationExecutionRequest input, CancellationToken cancellationToken = default)
    {
        MutationContract contract = RequireContract(input.Action);
        JsonElement arguments = Normalize(contract, input.Arguments);
        ToolApprovalRequest request;
        ConnectorUseContext context;
        if (input.ProposalId is { } proposalId)
        {
            ConnectorApprovedProposal approved = await RequireProposalAsync(proposalId, cancellationToken);
            request = await database.ToolApprovalRequests.SingleAsync(request => request.TenantId == TenantId && request.ProposalId == proposalId, cancellationToken);
            if (approved.ConnectionId != input.ConnectionId || approved.Action != input.Action || Hash(approved.Arguments) != Hash(arguments)) throw Denied();
            context = approved.Context;
        }
        else if (input.RunId is { } runId && input.StepRunId is { } stepId)
        {
            context = await workflowConnections.RequireAsync(runId, stepId, input.ConnectionId, input.Action, arguments, cancellationToken);
            request = await database.ToolApprovalRequests.SingleOrDefaultAsync(request => request.TenantId == TenantId && request.WorkflowStepRunId == stepId, cancellationToken) ?? throw Denied();
            if (request.DecidedByUserId is not { } reviewer) throw Denied();
            await reviewers.RequireAsync(request, reviewer, cancellationToken);
        }
        else
        {
            Guid userId = await RequireUserAsync(cancellationToken);
            string key = ConnectorInvocationIdentity.Key(TenantId, userId, input.ConnectionId, input.Action, ConnectorInvocationIdentity.IdempotencyKey(arguments));
            request = await database.ToolApprovalRequests.SingleOrDefaultAsync(request => request.TenantId == TenantId && request.InvocationKey == key, cancellationToken) ?? throw Denied();
            if (request.WorkflowStepRunId.HasValue || request.RequestingUserId != userId || request.DecidedByUserId != userId
                || request.ThreadId != roomContext?.RoomId?.ToString() || request.ArgsHash != ConnectorMutationArguments.Hash(input.ConnectionId, arguments)) throw Denied();
            await RequireRoomAsync(request, cancellationToken);
            context = new(new("User", userId, userId));
        }
        await database.Entry(request).ReloadAsync(cancellationToken);
        ConnectorApprovedProposal exact = await BindingAsync(request, cancellationToken);
        if (exact.ConnectionId != input.ConnectionId || exact.Action != input.Action || Hash(exact.Arguments) != Hash(arguments)) throw Denied();
        ToolApprovalPreview? reviewed = request.PreviewJson is { } preview ? ConnectorJson.Read<ToolApprovalPreview>(preview) : null;
        ConnectorMutationArguments.RequireReviewedAccount(reviewed, input.ConnectionId, input.ProviderAccountId);
        if (contract.Curated is not null)
            ConnectorMutationArguments.RequireReviewedServiceAccount(reviewed, input.Artifact, input.ServiceCredentialVersion);
        if (request.ExecutionJson is { } previous)
        {
            ConnectorMutationJournal journal = Journal(previous);
            if (journal.ConnectionId != input.ConnectionId || journal.ProviderAccountId != input.ProviderAccountId
                || journal.Action != input.Action || journal.ArgumentsHash != Hash(arguments)) throw Denied();
            return new(request.Id, await OperationIdAsync(request, cancellationToken), journal.LeaseId, journal.Status, journal.Context, journal.Result);
        }
        if (request.ExpiresAt <= clock.UtcNow || request.ProposalId is null && (request.Status != ToolApprovalRequestStatus.Approved
            || request.ConsumedAt is null || request.DecidedAt is null || request.DecidedAt >= request.ExpiresAt)) throw Denied();
        if (contract.Approval.Tier == ToolApprovalTier.High && input.ProposalId is null) throw Denied();
        Guid leaseId = Guid.NewGuid();
        request.ExecutionJson = ConnectorJson.Serialize(new ConnectorMutationJournal(1, "InFlight", input.ConnectionId, input.ProviderAccountId,
            input.Action, context, Hash(arguments), clock.UtcNow, leaseId, clock.UtcNow.AddMinutes(2)));
        await SaveClaimAsync(cancellationToken);
        return new(request.Id, await OperationIdAsync(request, cancellationToken), leaseId, "Claimed", context);
    }

    private async Task<ConnectorApprovedProposal> BindingAsync(ToolApprovalRequest request, CancellationToken cancellationToken)
    {
        MutationContract contract = RequireContract(request.ToolName);
        if (request.RiskTier != contract.Approval.Tier.ToString()) throw Denied();
        ConnectorActionRef action = contract.Action;
        if (request.WorkflowStepRunId is not { } stepId)
        {
            (Guid connectionId, JsonElement payload) = ConnectorMutationArguments.ReadTool(request.ArgumentsRedactedJson);
            JsonElement arguments = Normalize(contract, payload);
            if (request.RequestingUserId is not { } actor || request.ArgsHash != ConnectorMutationArguments.Hash(connectionId, arguments)
                || request.InvocationKey != ConnectorInvocationIdentity.Key(TenantId, actor, connectionId, action, ConnectorInvocationIdentity.IdempotencyKey(arguments))) throw Denied();
            return new(connectionId, action, arguments, new(new("User", actor, actor)));
        }
        WorkflowStepRun step = await database.WorkflowStepRuns.AsNoTracking().SingleOrDefaultAsync(step => step.TenantId == TenantId && step.Id == stepId, cancellationToken) ?? throw Denied();
        WorkflowRun run = await database.WorkflowRuns.AsNoTracking().SingleAsync(run => run.TenantId == TenantId && run.Id == step.RunId, cancellationToken);
        WorkflowVersion version = await database.WorkflowVersions.AsNoTracking().SingleAsync(version => version.TenantId == TenantId && version.Id == run.WorkflowVersionId, cancellationToken);
        WorkflowToolParameters parameters = WorkflowRunService.ReadDefinition(version).Nodes.Single(node => node.Key == step.NodeKey).Parameters
            .Deserialize<WorkflowToolParameters>(WorkflowDefinitionCodec.JsonOptions) ?? throw Denied();
        JsonElement args = Normalize(contract, ConnectorJson.Read<JsonElement>(request.ArgumentsRedactedJson));
        if (step.ApprovalRequestId != request.Id || step.ProposalId != request.ProposalId || parameters.ActionKey != action.Key || parameters.ContractVersion != action.Version
            || step.ActionKey != action.Key || step.ActionVersion != action.Version || version.DefinitionHash != run.DefinitionHash
            || request.ArgsHash != step.ArgumentsHash || request.ArgsHash != Hash(args) || parameters.Connection is not { } binding
            || binding.AllowedActions?.Contains(action) != true || step.ArgumentsJson is null || Hash(ConnectorJson.Read<JsonElement>(step.ArgumentsJson)) != Hash(args)) throw Denied();
        return new(binding.ConnectionId, action, args, new(WorkflowExecutionAuthorization.Actor(run), run.WorkflowId, run.WorkflowVersionId,
            run.Id, step.Id, binding.WorkflowGrantId, binding.AllowedActions, binding.Artifact));
    }

    private MutationContract RequireContract(ConnectorActionRef action)
    {
        if (action.Version == 1 && GmailActionPolicies.IsMutation(action.Key))
            return new(action, GmailActionPolicies.ToolNames[action.Key], GmailActionPolicies.Approval(action.Key)!);
        ConnectorPackageActionContract? contract = curated?.FindAction(action);
        if (contract?.Descriptor.Approval is not { Tier: ToolApprovalTier.High } approval) throw Denied();
        return new(action, contract.Descriptor.ToolName, approval, contract);
    }

    private MutationContract RequireContract(string toolName)
    {
        if (GmailActionPolicies.ActionForTool(toolName) is { } gmail) return RequireContract(new ConnectorActionRef(gmail, 1));
        ConnectorActionDescriptor descriptor = curated?.FindAction(toolName)?.Descriptor ?? throw Denied();
        return RequireContract(new ConnectorActionRef(descriptor.Key, descriptor.Version));
    }

    private static JsonElement Normalize(MutationContract contract, JsonElement arguments)
    {
        if (contract.Curated is null) return GmailActionContracts.Normalize(contract.Action, arguments);
        contract.Curated.RequireInput(arguments);
        return arguments;
    }

    private sealed record MutationContract(ConnectorActionRef Action, string ToolName, ToolApprovalOptions Approval, ConnectorPackageActionContract? Curated = null);

    private async Task<ToolApprovalRequest> RequestAsync(Guid id, CancellationToken cancellationToken)
        => await database.ToolApprovalRequests.SingleOrDefaultAsync(request => request.TenantId == TenantId && request.Id == id, cancellationToken) ?? throw Denied();
    private async Task<Guid> RequireUserAsync(CancellationToken cancellationToken)
    {
        if (!currentUser.TryGetCurrentUserId(out Guid userId)) throw Denied();
        await RequireUserAsync(userId, cancellationToken); return userId;
    }
    private async Task RequireUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!(await users.GetByIdsAsync(TenantId, [userId], cancellationToken)).Any(user => user.UserId == userId && user.Status == "Active")
            || !await permissions.HasPermissionAsync(userId, "Integration.Use", cancellationToken)) throw Denied();
    }
    private async Task RequireRoomAsync(ToolApprovalRequest request, CancellationToken cancellationToken)
    { if (roomAuthority is not null && !await roomAuthority.CanDecideAsync(request.AiRunId, cancellationToken)) throw Denied(); }
    private async Task<Guid> OperationIdAsync(ToolApprovalRequest request, CancellationToken cancellationToken) => request.WorkflowStepRunId is { } id
        ? await database.WorkflowStepRuns.AsNoTracking().Where(step => step.TenantId == TenantId && step.Id == id).Select(step => step.OperationId).SingleAsync(cancellationToken) : request.Id;
    private static string Hash(JsonElement arguments) => ToolArgumentsHash.Compute(arguments.EnumerateObject().ToDictionary(property => property.Name, property => (object?)property.Value));
    private static ConnectorMutationJournal Journal(string json)
    {
        ConnectorMutationJournal journal = ConnectorJson.Read<ConnectorMutationJournal>(json);
        if (journal.SchemaVersion != 1) throw Denied(); return journal;
    }
    private async Task SaveClaimAsync(CancellationToken cancellationToken)
    {
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new WorkflowConflictException("Another invocation owns this provider operation. Reload its durable status."); }
    }
    private static PermissionDeniedException Denied() => new("Integration.Use", "The exact connector action requires its current durable approval and execution authority.");
}
