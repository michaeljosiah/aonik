using System.Text;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Platform.Entities.Integrations;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Platform.Services.Integrations;

internal sealed class ConnectorService(PlatformDbContext database, ConnectorAuthorization authorization, ConnectorCatalog catalog,
    ConnectorPolicyService policies, ConnectorCredentialService credentials, IEnumerable<IConnectorActionProvider> providers,
    IEnumerable<IConnectorOAuthProvider> oauthProviders, IConnectorContentStore content, IAuditLogWriter audit, IClock clock,
    IWorkflowConnectorContextReader? workflowContext = null) : IConnectorService
{
    public async Task<IReadOnlyList<ConnectorDescriptor>> ListConnectorsAsync(CancellationToken cancellationToken = default)
    {
        await authorization.RequireUserAsync("Integration.Read", cancellationToken);
        return catalog.Descriptors;
    }

    public async Task<IReadOnlyList<ConnectorConnectionSummary>> ListConnectionsAsync(string connectorKey, CancellationToken cancellationToken = default)
    {
        Guid userId = await authorization.RequireUserAsync("Integration.Read", cancellationToken);
        Guid tenantId = authorization.TenantId;
        Guid[] roles = await database.UserRoles.AsNoTracking().Where(link => link.UserId == userId && link.User.TenantId == tenantId && link.Role.TenantId == tenantId)
            .Select(link => link.RoleId).ToArrayAsync(cancellationToken);
        Guid[] granted = await database.ConnectorConnectionGrants.AsNoTracking().Where(grant => grant.TenantId == tenantId && grant.RevokedAt == null
            && (grant.ExpiresAt == null || grant.ExpiresAt > clock.UtcNow) && ((grant.GranteeType == "User" && grant.GranteeId == userId)
                || (grant.GranteeType == "Role" && roles.Contains(grant.GranteeId)))).Select(grant => grant.ConnectionId).Distinct().ToArrayAsync(cancellationToken);
        return (await database.ConnectorConnections.AsNoTracking().Where(connection => connection.TenantId == tenantId && connection.ConnectorKey == connectorKey
            && (connection.OwnerUserId == userId || granted.Contains(connection.Id))).OrderBy(connection => connection.DisplayName).ThenBy(connection => connection.Id)
            .Take(100).ToListAsync(cancellationToken)).Select(Summary).ToList();
    }

    public async Task<ConnectorReadiness> GetReadinessAsync(string connectorKey, ConnectorActionRef action, Guid? connectionId = null,
        CancellationToken cancellationToken = default)
    {
        Guid userId = await authorization.RequireUserAsync("Integration.Read", cancellationToken);
        ConnectorActionDescriptor descriptor;
        try { descriptor = catalog.Resolve(connectorKey, action); }
        catch (ConnectorAccessException) { return Readiness("ConnectorUnavailable"); }
        ConnectorPolicy policy = await policies.GetAsync(connectorKey, cancellationToken);
        if (policy.Code != "Ready") return Readiness(policy.Code);
        if (!policy.AllowedActions.Contains(action)) return Readiness("ForbiddenByPolicy");
        if (oauthProviders.SingleOrDefault(provider => provider.ConnectorKey == connectorKey)?.IsConfigured != true
            || !providers.Any(provider => provider.ConnectorKey == connectorKey)) return Readiness("ConnectorUnavailable");
        var context = new ConnectorUseContext(new("User", userId, userId));
        if (connectionId.HasValue) return await ForConnectionAsync(connectionId.Value);
        foreach (Guid preferred in await policies.PreferredConnectionsAsync(userId, connectorKey, cancellationToken))
        {
            ConnectorReadiness candidate = await ForConnectionAsync(preferred);
            if (candidate.Code == "Ready") return candidate;
        }
        var ready = new List<ConnectorReadiness>();
        ConnectorReadiness? blocker = null;
        foreach (ConnectorConnectionSummary connection in await ListConnectionsAsync(connectorKey, cancellationToken))
        {
            ConnectorReadiness candidate = await ForConnectionAsync(connection.Id);
            if (candidate.Code == "Ready") ready.Add(candidate);
            else if (candidate.ConnectionId.HasValue) blocker ??= candidate;
        }
        return ready.Count == 1 ? ready[0] : ready.Count > 1 ? Readiness("ConnectionSelectionRequired") : blocker ?? Readiness("NoAuthorizedConnection");

        async Task<ConnectorReadiness> ForConnectionAsync(Guid id)
        {
            try
            {
                ConnectorConnection connection = await authorization.RequireUseAsync(id, descriptor, context, cancellationToken);
                return new("Ready", "The action may proceed under its ordinary approval policy.", connection.Id, connection.DisplayName);
            }
            catch (ConnectorAccessException exception)
            {
                bool own = await database.ConnectorConnections.AsNoTracking().AnyAsync(connection => connection.TenantId == authorization.TenantId
                    && connection.Id == id && connection.OwnerUserId == userId, cancellationToken);
                return Readiness(exception.Code) with { ConnectionId = own ? id : null };
            }
            catch (PermissionDeniedException) { return Readiness("ForbiddenByPolicy"); }
        }
    }

    public async Task<JsonElement> InvokeReadAsync(Guid connectionId, ConnectorActionRef action, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        Guid userId = await authorization.RequireUserAsync("Integration.Use", cancellationToken);
        return await InvokeAsync(connectionId, action, arguments, new(new("User", userId, userId)), cancellationToken);
    }

    public async Task ValidateWorkflowBindingAsync(Guid workflowId, WorkflowConnectorBinding binding, ConnectorActionRef action, WorkflowActor actor,
        CancellationToken cancellationToken = default)
    {
        if (workflowId == Guid.Empty || binding.ConnectionId == Guid.Empty || binding.WorkflowGrantId == Guid.Empty
            || binding.AllowedActions is null || binding.AllowedActions.Count is 0 or > 100 || !binding.AllowedActions.Contains(action))
            throw ConnectorAuthorization.Access("WorkflowBindingRequired");
        foreach (ConnectorActionRef allowed in binding.AllowedActions.Distinct())
            await authorization.RequireUseAsync(binding.ConnectionId, catalog.Resolve(allowed),
                new(actor, workflowId, PinnedGrantId: binding.WorkflowGrantId, AllowedActions: binding.AllowedActions), cancellationToken);
    }

    public async Task<JsonElement> InvokeWorkflowReadAsync(Guid runId, Guid stepRunId, Guid connectionId, ConnectorActionRef action,
        JsonElement arguments, CancellationToken cancellationToken = default)
    {
        if (workflowContext is null) throw ConnectorAuthorization.Access("WorkflowBindingRequired");
        ConnectorUseContext context = await workflowContext.RequireAsync(runId, stepRunId, connectionId, action, arguments, cancellationToken);
        JsonElement result = await InvokeAsync(connectionId, action, arguments, context, cancellationToken);
        if (action.Key == GmailActionKeys.GetMessage)
        {
            GmailMessageResult message = ConnectorJson.Read<GmailMessageResult>(result);
            string? reference = null;
            if (message.Text.Length > 0)
                reference = (await content.SaveAsync(connectionId, message.Id, null, Encoding.UTF8.GetBytes(message.Text), "message.txt", "text/plain", cancellationToken)).FileReference;
            return ConnectorJson.Element(message with { Text = "", Headers = new Dictionary<string, string>(), ContentReference = reference });
        }
        return result;
    }

    private async Task<JsonElement> InvokeAsync(Guid connectionId, ConnectorActionRef action, JsonElement arguments, ConnectorUseContext context, CancellationToken cancellationToken)
    {
        ConnectorActionDescriptor descriptor = catalog.Resolve(action);
        if (descriptor.Approval is not null) throw new PermissionDeniedException("Integration.Use", "Mutating connector actions require their enforcing approval path.");
        IConnectorActionProvider provider = providers.SingleOrDefault(provider => provider.ConnectorKey == descriptor.ConnectorKey)
            ?? throw ConnectorAuthorization.Access("ConnectorUnavailable");
        if (oauthProviders.SingleOrDefault(provider => provider.ConnectorKey == descriptor.ConnectorKey)?.IsConfigured != true)
            throw ConnectorAuthorization.Access("ConnectorUnavailable");
        if (arguments.ValueKind != JsonValueKind.Object || arguments.GetRawText().Length > 65_536) throw new ArgumentException("Provide bounded typed action arguments.");
        provider.ValidateArguments(action, arguments);
        Guid operationId = context.StepRunId ?? Guid.NewGuid();
        string argumentsHash = ToolArgumentsHash.Compute(arguments.EnumerateObject().ToDictionary(property => property.Name, property => (object?)property.Value));
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(60));
        for (int attempt = 1; ; attempt++)
        {
            ConnectorConnection connection = await authorization.RequireUseAsync(connectionId, descriptor, context, budget.Token);
            await RecordAsync("Connector.ReadRequested", connection.Id, context, action, operationId, argumentsHash, attempt, null, budget.Token);
            try
            {
                string token = await credentials.AccessTokenAsync(connectionId, budget.Token);
                connection = await authorization.RequireUseAsync(connectionId, descriptor, context, budget.Token);
                JsonElement result = await provider.InvokeAsync(new(connection.Id, connection.ProviderAccountId, token, action, arguments, operationId), budget.Token);
                await authorization.RequireUseAsync(connection.Id, descriptor, context, budget.Token);
                await RecordAsync("Connector.ReadCompleted", connection.Id, context, action, operationId, argumentsHash, attempt, null, budget.Token);
                return result;
            }
            catch (ConnectorProviderException exception)
            {
                await RecordAsync("Connector.ReadFailed", connection.Id, context, action, operationId, argumentsHash, attempt, exception.Code, CancellationToken.None);
                if (exception.Code == "ReauthorizationRequired")
                {
                    await credentials.MarkReauthorizationAsync(connection.Id, CancellationToken.None);
                    throw ConnectorAuthorization.Access("ReauthorizationRequired");
                }
                TimeSpan delay = exception.RetryAfter ?? TimeSpan.FromMilliseconds(250 * attempt);
                if (!exception.Transient || attempt >= 3 || delay > TimeSpan.FromSeconds(15)) throw;
                await Task.Delay(delay < TimeSpan.Zero ? TimeSpan.Zero : delay, budget.Token);
            }
            catch (ConnectorAccessException exception)
            {
                await RecordAsync("Connector.ReadRefused", connection.Id, context, action, operationId, argumentsHash, attempt, exception.Code, CancellationToken.None);
                throw;
            }
        }
    }

    public async Task<(ConnectorStoredContent Metadata, Stream Stream)> OpenContentAsync(string reference, CancellationToken cancellationToken = default)
    {
        Guid userId = await authorization.RequireUserAsync("Integration.Use", cancellationToken);
        ConnectorStoredContent metadata = content.Describe(reference);
        var action = new ConnectorActionRef(metadata.AttachmentId is null ? GmailActionKeys.GetMessage : GmailActionKeys.GetAttachment, 1);
        var context = new ConnectorUseContext(new("User", userId, userId));
        await authorization.RequireUseAsync(metadata.ConnectionId, catalog.Resolve(action), context, cancellationToken);
        await RecordAsync("Connector.ContentOpened", metadata.ConnectionId, context, action, Guid.NewGuid(), "", 1, null, cancellationToken);
        return (metadata, await content.OpenAsync(reference, cancellationToken));
    }

    private Task RecordAsync(string name, Guid connectionId, ConnectorUseContext context, ConnectorActionRef action, Guid operationId,
        string hash, int attempt, string? error, CancellationToken cancellationToken) => audit.LogAsync(name, "ConnectorConnection", connectionId,
            authorization.TenantId, context.Actor.Id, context.RunId?.ToString("D"), ConnectorJson.Serialize(new
            {
                action.Key, action.Version, operationId, context.Actor.Type, actorId = context.Actor.Id, context.WorkflowId,
                context.WorkflowVersionId, context.RunId, context.StepRunId, argumentsHash = hash, attempt, error,
            }), cancellationToken);

    internal static ConnectorConnectionSummary Summary(ConnectorConnection connection) => new(connection.Id, connection.ConnectorKey, connection.Ownership,
        connection.DisplayName, connection.Status, ConnectorJson.Read<List<string>>(connection.GrantedScopesJson), Convert.ToBase64String(connection.RowVersion), connection.OwnerUserId);
    internal static ConnectorReadiness Readiness(string code) => new(code, code switch
    {
        "Ready" => "The action may proceed under its ordinary approval policy.",
        "ConnectorUnavailable" => "Request platform provisioning from the tenant integration reviewer.",
        "TenantNotEnabled" => "Request tenant integration enablement.",
        "ReauthorizationRequired" or "InsufficientScopes" => "The account owner must complete renewed consent.",
        "ConnectionGrantRequired" => "Ask the account owner or integration manager to review the exact requested use.",
        "WorkflowBindingRequired" => "Select and publish an exact authorized connection binding.",
        "ConnectionSelectionRequired" => "Select an authorized connection or set an eligible default account.",
        "ForbiddenByPolicy" => "An integration administrator must review the policy restriction; a connection request cannot override it.",
        _ => "Request an authorized account and the required use grant.",
    });
}
