using System.Text.Json;

using Aonik.Platform.Entities.Integrations;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Platform.Services.Integrations;

/// <summary>Uses the ordinary connector policy/credential path; Agents independently owns every dispatch claim.</summary>
internal sealed partial class ConnectorMutationService(ConnectorAuthorization authorization, ConnectorCatalog catalog,
    ConnectorCredentialService credentials, IEnumerable<IConnectorActionProvider> providers, IEnumerable<IConnectorOAuthProvider> oauthProviders,
    IConnectorMutationAuthority authority, IWorkflowConnectorContextReader workflowContext, IAuditLogWriter audit) : IConnectorMutationService
{
    public async Task<JsonElement> PrepareMessageAsync(Guid connectionId, GmailPrepareMessageRequest request, CancellationToken cancellationToken = default)
    {
        Guid userId = await authorization.RequireUserAsync("Integration.Use", cancellationToken);
        return await PrepareAsync(connectionId, request, new(new("User", userId, userId)), cancellationToken);
    }

    public async Task<ConnectorMutationPreview> PreviewAsync(Guid connectionId, ConnectorActionRef action, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        Guid userId = await authorization.RequireUserAsync("Integration.Use", cancellationToken);
        arguments = GmailActionContracts.Normalize(action, arguments);
        await authorization.RequireUseAsync(connectionId, catalog.Resolve(action), new(new("User", userId, userId)), cancellationToken);
        ConnectorMutationPreview? existing = await authority.FindInvocationAsync(connectionId, action, arguments, cancellationToken);
        if (existing is not null) return existing;
        ToolApprovalPreview preview = await PreviewCoreAsync(connectionId, action, arguments, new(new("User", userId, userId)), cancellationToken);
        return new(preview, ConnectorInvocationIdentity.Key(authorization.TenantId, userId, connectionId, action, GmailActionContracts.IdempotencyKey(arguments)));
    }

    public async Task<ToolApprovalPreview> PreviewWorkflowAsync(Guid runId, Guid stepRunId, Guid connectionId, ConnectorActionRef action, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        arguments = GmailActionContracts.Normalize(action, arguments);
        ConnectorUseContext context = await workflowContext.RequirePreparationAsync(runId, stepRunId, connectionId, action, arguments, cancellationToken);
        return await PreviewCoreAsync(connectionId, action, arguments, context, cancellationToken);
    }

    public async Task<JsonElement> ExecuteAsync(Guid connectionId, ConnectorActionRef action, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        Guid userId = await authorization.RequireUserAsync("Integration.Use", cancellationToken);
        if (GmailActionPolicies.Approval(action.Key)?.Tier != ToolApprovalTier.Medium) throw ConnectorAuthorization.Access("DurableProposalRequired");
        return await ExecuteCoreAsync(connectionId, action, arguments, new(new("User", userId, userId)), null, cancellationToken);
    }

    public async Task<JsonElement> ExecuteWorkflowAsync(Guid runId, Guid stepRunId, Guid connectionId, ConnectorActionRef action, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        arguments = GmailActionContracts.Normalize(action, arguments);
        ConnectorUseContext context = await workflowContext.RequireAsync(runId, stepRunId, connectionId, action, arguments, cancellationToken);
        if (action.Key == GmailActionKeys.PrepareMessage) return await PrepareAsync(connectionId, ConnectorJson.Read<GmailPrepareMessageRequest>(arguments), context, cancellationToken);
        if (GmailActionPolicies.Approval(action.Key)?.Tier != ToolApprovalTier.Medium) throw ConnectorAuthorization.Access("DurableProposalRequired");
        return await ExecuteCoreAsync(connectionId, action, arguments, context, null, cancellationToken);
    }

    public async Task<JsonElement> ExecuteProposalAsync(Guid proposalId, CancellationToken cancellationToken = default)
    {
        ConnectorApprovedProposal proposal = await authority.RequireProposalAsync(proposalId, cancellationToken);
        return await ExecuteCoreAsync(proposal.ConnectionId, proposal.Action, proposal.Arguments, proposal.Context, proposalId, cancellationToken);
    }

    public async Task<ToolApprovalPreview> RequireReviewAsync(Guid approvalRequestId, CancellationToken cancellationToken = default)
    {
        Guid userId = await authorization.RequireUserAsync("Integration.Use", cancellationToken);
        ConnectorApprovedProposal binding = await authority.RequireReviewAsync(approvalRequestId, cancellationToken);
        await authorization.RequireUseAsync(binding.ConnectionId, catalog.Resolve(binding.Action), new(new("User", userId, userId)), cancellationToken);
        ConnectorConnection connection = await authorization.RequireUseAsync(binding.ConnectionId, catalog.Resolve(binding.Action), binding.Context, cancellationToken);
        ConnectorMutationArguments.RequireReviewedAccount(binding.Preview, binding.ConnectionId, connection.ProviderAccountId);
        return binding.Preview ?? throw ConnectorAuthorization.Access("ActionPreviewUnavailable");
    }

    private async Task<JsonElement> PrepareAsync(Guid connectionId, GmailPrepareMessageRequest request, ConnectorUseContext context, CancellationToken cancellationToken)
    {
        var action = new ConnectorActionRef(GmailActionKeys.PrepareMessage, 1);
        JsonElement arguments = GmailActionContracts.Normalize(action, ConnectorJson.Element(request));
        await authorization.RequireUseAsync(connectionId, catalog.Resolve(new(request.TargetAction, 1)), context, cancellationToken);
        if (request.SourceMessageId is not null) await authorization.RequireUseAsync(connectionId, catalog.Resolve(new(GmailActionKeys.GetMessage, 1)), context, cancellationToken);
        if (request.AttachmentReferences?.Count > 0) await authorization.RequireUseAsync(connectionId, catalog.Resolve(new(GmailActionKeys.GetAttachment, 1)), context, cancellationToken);
        ConnectorProviderCall call = await CallAsync(connectionId, action, arguments, context, Guid.NewGuid(), cancellationToken);
        JsonElement result = await Provider(action).InvokeAsync(call, cancellationToken);
        await authorization.RequireUseAsync(connectionId, catalog.Resolve(action), context, cancellationToken);
        await RecordAsync("Connector.MessagePrepared", connectionId, action, call.OperationId, context, null, cancellationToken);
        return result;
    }

    private async Task<ToolApprovalPreview> PreviewCoreAsync(Guid connectionId, ConnectorActionRef action, JsonElement arguments, ConnectorUseContext context, CancellationToken cancellationToken)
    {
        if (!GmailActionPolicies.IsMutation(action.Key)) throw new ArgumentException("Only classified connector mutations require an effect preview.");
        ConnectorProviderCall call = await CallAsync(connectionId, action, arguments, context, Guid.NewGuid(), cancellationToken);
        ToolApprovalPreview preview = await Provider(action).PreviewAsync(call, cancellationToken) ?? throw ConnectorAuthorization.Access("ActionPreviewUnavailable");
        await authorization.RequireUseAsync(connectionId, catalog.Resolve(action), context, cancellationToken);
        return preview;
    }

    private async Task<JsonElement> ExecuteCoreAsync(Guid connectionId, ConnectorActionRef action, JsonElement arguments, ConnectorUseContext context, Guid? proposalId, CancellationToken cancellationToken)
    {
        arguments = GmailActionContracts.Normalize(action, arguments);
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); budget.CancelAfter(TimeSpan.FromSeconds(60));
        ConnectorProviderCall call = await CallAsync(connectionId, action, arguments, context, Guid.Empty, budget.Token);
        ConnectorMutationClaim claim = await authority.ClaimAsync(new(connectionId, call.ProviderAccountId, action, arguments, context.RunId, context.StepRunId, proposalId), budget.Token);
        if (claim.Status == "Completed" && claim.Result is { } recorded) return recorded;
        if (claim.Status != "Claimed") throw ConnectorAuthorization.Access("ActionOutcomeUnknown");
        call = call with { OperationId = claim.OperationId };
        try
        {
            await authorization.RequireUseAsync(connectionId, catalog.Resolve(action), claim.Context, budget.Token);
            JsonElement result = await Provider(action).InvokeAsync(call, budget.Token);
            // Preserve a confirmed receipt even when the caller disconnects immediately after provider success.
            await authority.CompleteAsync(claim.ApprovalRequestId, claim.LeaseId, result, CancellationToken.None);
            await RecordAsync("Connector.MutationCompleted", connectionId, action, claim.OperationId, claim.Context, null, CancellationToken.None);
            await authorization.RequireUseAsync(connectionId, catalog.Resolve(action), claim.Context, budget.Token);
            return result;
        }
        catch (Exception exception)
        {
            string code = exception is ConnectorProviderException provider ? provider.Code : exception is ConnectorAccessException access ? access.Code : "ActionOutcomeUnknown";
            await authority.HoldAsync(claim.ApprovalRequestId, claim.LeaseId, code, CancellationToken.None);
            await RecordAsync("Connector.MutationHeld", connectionId, action, claim.OperationId, claim.Context, code, CancellationToken.None);
            if (code == "ReauthorizationRequired") await credentials.MarkReauthorizationAsync(connectionId, CancellationToken.None);
            throw;
        }
    }

    private async Task<ConnectorProviderCall> CallAsync(Guid connectionId, ConnectorActionRef action, JsonElement arguments, ConnectorUseContext context, Guid operationId, CancellationToken cancellationToken)
    {
        ConnectorActionDescriptor descriptor = catalog.Resolve(action);
        IConnectorActionProvider provider = Provider(action); provider.ValidateArguments(action, arguments);
        ConnectorConnection connection = await authorization.RequireUseAsync(connectionId, descriptor, context, cancellationToken);
        IConnectorOAuthProvider oauth = oauthProviders.SingleOrDefault(provider => provider.ConnectorKey == descriptor.ConnectorKey && provider.IsConfigured) ?? throw ConnectorAuthorization.Access("ConnectorUnavailable");
        string token = await credentials.AccessTokenAsync(connectionId, cancellationToken);
        ConnectorAccountIdentity identity = await oauth.GetIdentityAsync(token, cancellationToken);
        if (identity.Id != connection.ProviderAccountId) throw ConnectorAuthorization.Access("ProviderAccountChanged");
        await authorization.RequireUseAsync(connectionId, descriptor, context, cancellationToken);
        return new(connectionId, identity.Id, token, action, arguments, operationId, identity.Label);
    }
    private IConnectorActionProvider Provider(ConnectorActionRef action) => providers.SingleOrDefault(provider => provider.ConnectorKey == catalog.Resolve(action).ConnectorKey)
        ?? throw ConnectorAuthorization.Access("ConnectorUnavailable");
    private Task RecordAsync(string name, Guid connectionId, ConnectorActionRef action, Guid operationId, ConnectorUseContext context, string? code, CancellationToken cancellationToken)
        => audit.LogActorAsync(name, "ConnectorConnection", connectionId, authorization.TenantId, context.Actor.Type, context.Actor.Id,
            context.RunId?.ToString("D"), ConnectorJson.Serialize(new { action, operationId, context.RunId, context.StepRunId, code }), cancellationToken);
}
