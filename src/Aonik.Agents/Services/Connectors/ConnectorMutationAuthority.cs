using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;
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
    ChatRoomInvocationGuard? roomContext = null, IChatRoomApprovalAuthority? roomAuthority = null) : IConnectorMutationAuthority
{
    private Guid TenantId => tenantProvider.GetCurrentTenantId();

    public async Task<ConnectorMutationPreview?> FindInvocationAsync(Guid connectionId, ConnectorActionRef action, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        Guid userId = await RequireUserAsync(cancellationToken);
        string key = ConnectorInvocationIdentity.Key(TenantId, userId, connectionId, action, GmailActionContracts.IdempotencyKey(arguments));
        ToolApprovalRequest? request = await database.ToolApprovalRequests.AsNoTracking().SingleOrDefaultAsync(request => request.TenantId == TenantId && request.InvocationKey == key, cancellationToken);
        if (request is null) return null;
        if (request.RequestingUserId != userId || request.ThreadId != roomContext?.RoomId?.ToString() || request.WorkflowStepRunId.HasValue
            || request.ToolName != GmailActionPolicies.ToolNames[action.Key] || request.ArgsHash != ConnectorMutationArguments.Hash(connectionId, arguments)) throw Denied();
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
        ToolApprovalOptions? policy = GmailActionPolicies.Approval(binding.Action.Key);
        if (policy?.Tier != ToolApprovalTier.High || policy.ProposalType != proposal.ProposalType || proposal.RiskTier != "High"
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
        if (input.Action.Version != 1 || !GmailActionPolicies.IsMutation(input.Action.Key)) throw Denied();
        JsonElement arguments = GmailActionContracts.Normalize(input.Action, input.Arguments);
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
            string key = ConnectorInvocationIdentity.Key(TenantId, userId, input.ConnectionId, input.Action, GmailActionContracts.IdempotencyKey(arguments));
            request = await database.ToolApprovalRequests.SingleOrDefaultAsync(request => request.TenantId == TenantId && request.InvocationKey == key, cancellationToken) ?? throw Denied();
            if (request.WorkflowStepRunId.HasValue || request.RequestingUserId != userId || request.DecidedByUserId != userId
                || request.ThreadId != roomContext?.RoomId?.ToString() || request.ArgsHash != ConnectorMutationArguments.Hash(input.ConnectionId, arguments)) throw Denied();
            await RequireRoomAsync(request, cancellationToken);
            context = new(new("User", userId, userId));
        }
        await database.Entry(request).ReloadAsync(cancellationToken);
        ConnectorApprovedProposal exact = await BindingAsync(request, cancellationToken);
        if (exact.ConnectionId != input.ConnectionId || exact.Action != input.Action || Hash(exact.Arguments) != Hash(arguments)) throw Denied();
        ConnectorMutationArguments.RequireReviewedAccount(request.PreviewJson is { } preview ? ConnectorJson.Read<ToolApprovalPreview>(preview) : null, input.ConnectionId, input.ProviderAccountId);
        if (request.ExecutionJson is { } previous)
        {
            ConnectorMutationJournal journal = Journal(previous);
            if (journal.ConnectionId != input.ConnectionId || journal.ProviderAccountId != input.ProviderAccountId
                || journal.Action != input.Action || journal.ArgumentsHash != Hash(arguments)) throw Denied();
            return new(request.Id, await OperationIdAsync(request, cancellationToken), journal.LeaseId, journal.Status, journal.Context, journal.Result);
        }
        if (request.ExpiresAt <= clock.UtcNow || request.ProposalId is null && (request.Status != ToolApprovalRequestStatus.Approved
            || request.ConsumedAt is null || request.DecidedAt is null || request.DecidedAt >= request.ExpiresAt)) throw Denied();
        if (GmailActionPolicies.Approval(input.Action.Key)?.Tier == ToolApprovalTier.High && input.ProposalId is null) throw Denied();
        Guid leaseId = Guid.NewGuid();
        request.ExecutionJson = ConnectorJson.Serialize(new ConnectorMutationJournal(1, "InFlight", input.ConnectionId, input.ProviderAccountId,
            input.Action, context, Hash(arguments), clock.UtcNow, leaseId, clock.UtcNow.AddMinutes(2)));
        await SaveClaimAsync(cancellationToken);
        return new(request.Id, await OperationIdAsync(request, cancellationToken), leaseId, "Claimed", context);
    }

    private async Task<ConnectorApprovedProposal> BindingAsync(ToolApprovalRequest request, CancellationToken cancellationToken)
    {
        string key = GmailActionPolicies.ActionForTool(request.ToolName) ?? throw Denied();
        if (!GmailActionPolicies.IsMutation(key) || request.RiskTier != GmailActionPolicies.Approval(key)!.Tier.ToString()) throw Denied();
        var action = new ConnectorActionRef(key, 1);
        if (request.WorkflowStepRunId is not { } stepId)
        {
            (Guid connectionId, JsonElement arguments) = ConnectorMutationArguments.ReadTool(request.ArgumentsRedactedJson, action);
            if (request.RequestingUserId is not { } actor || request.ArgsHash != ConnectorMutationArguments.Hash(connectionId, arguments)
                || request.InvocationKey != ConnectorInvocationIdentity.Key(TenantId, actor, connectionId, action, GmailActionContracts.IdempotencyKey(arguments))) throw Denied();
            return new(connectionId, action, arguments, new(new("User", actor, actor)));
        }
        WorkflowStepRun step = await database.WorkflowStepRuns.AsNoTracking().SingleOrDefaultAsync(step => step.TenantId == TenantId && step.Id == stepId, cancellationToken) ?? throw Denied();
        WorkflowRun run = await database.WorkflowRuns.AsNoTracking().SingleAsync(run => run.TenantId == TenantId && run.Id == step.RunId, cancellationToken);
        WorkflowVersion version = await database.WorkflowVersions.AsNoTracking().SingleAsync(version => version.TenantId == TenantId && version.Id == run.WorkflowVersionId, cancellationToken);
        WorkflowToolParameters parameters = WorkflowRunService.ReadDefinition(version).Nodes.Single(node => node.Key == step.NodeKey).Parameters
            .Deserialize<WorkflowToolParameters>(WorkflowDefinitionCodec.JsonOptions) ?? throw Denied();
        JsonElement args = GmailActionContracts.Normalize(action, ConnectorJson.Read<JsonElement>(request.ArgumentsRedactedJson));
        if (step.ApprovalRequestId != request.Id || step.ProposalId != request.ProposalId || parameters.ActionKey != key || parameters.ContractVersion != 1
            || step.ActionKey != key || step.ActionVersion != 1 || version.DefinitionHash != run.DefinitionHash
            || request.ArgsHash != step.ArgumentsHash || request.ArgsHash != Hash(args) || parameters.Connection is not { } binding
            || binding.AllowedActions?.Contains(action) != true || step.ArgumentsJson is null || Hash(ConnectorJson.Read<JsonElement>(step.ArgumentsJson)) != Hash(args)) throw Denied();
        return new(binding.ConnectionId, action, args, new(WorkflowExecutionAuthorization.Actor(run), run.WorkflowId, run.WorkflowVersionId,
            run.Id, step.Id, binding.WorkflowGrantId, binding.AllowedActions));
    }

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
