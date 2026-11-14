using Microsoft.EntityFrameworkCore;

using Aonik.Platform.Entities.Integrations;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Platform.Services.Integrations;

internal sealed class ConnectorGrantService(PlatformDbContext database, ConnectorAuthorization authorization, ConnectorCatalog catalog,
    ConnectorPolicyService policies, ITenantRoleReader roles, IAuditLogWriter audit, IClock clock,
    IWorkflowConnectorContextReader? workflows = null, IAutomationIdentityAuthorization? automation = null)
{
    public async Task<WorkflowConnectorBinding?> GetWorkflowBindingAsync(Guid connectionId, Guid workflowId, ConnectorActionRef action, CancellationToken cancellationToken = default)
    {
        Guid userId = await authorization.RequireUserAsync("Integration.Use", cancellationToken);
        if (workflows is null) throw ConnectorAuthorization.Access("WorkflowBindingRequired");
        await workflows.RequireGrantTargetAsync(workflowId, userId, cancellationToken);
        ConnectorConnectionGrant? grant = await database.ConnectorConnectionGrants.AsNoTracking().SingleOrDefaultAsync(grant => grant.TenantId == authorization.TenantId
            && grant.ConnectionId == connectionId && grant.GranteeType == "Workflow" && grant.GranteeId == workflowId && grant.RevokedAt == null
            && (grant.ExpiresAt == null || grant.ExpiresAt > clock.UtcNow), cancellationToken);
        if (grant is null) return null;
        if (!ConnectorJson.Read<List<ConnectorActionRef>>(grant.AllowedActionsJson).Contains(action)) throw ConnectorAuthorization.Access("ConnectionGrantRequired");
        await authorization.RequireUseAsync(connectionId, catalog.Resolve(action), new(new("User", userId, userId), workflowId,
            PinnedGrantId: grant.Id, AllowedActions: [action]), cancellationToken);
        return new(connectionId, grant.Id, [action]);
    }

    public async Task<IReadOnlyList<ConnectorGrantDetail>> ListAsync(Guid connectionId, CancellationToken cancellationToken = default)
    {
        Guid userId = await authorization.RequireUserAsync("Integration.Read", cancellationToken);
        ConnectorConnection connection = await FindConnectionAsync(connectionId, cancellationToken);
        await RequireManagerAsync(connection, userId, cancellationToken);
        return (await database.ConnectorConnectionGrants.AsNoTracking().Where(grant => grant.TenantId == authorization.TenantId
            && grant.ConnectionId == connectionId).OrderByDescending(grant => grant.CreatedAt).Take(100).ToListAsync(cancellationToken)).Select(Detail).ToList();
    }

    public async Task<ConnectorGrantDetail> IssueAsync(Guid connectionId, ConnectorGrantCommand command, CancellationToken cancellationToken = default)
    {
        Guid userId = await authorization.RequireUserAsync("Integration.Read", cancellationToken);
        ConnectorConnection connection = await FindConnectionAsync(connectionId, cancellationToken);
        await RequireManagerAsync(connection, userId, cancellationToken);
        Version(connection.RowVersion, command.ExpectedConnectionVersion);
        await ValidateTargetAsync(command.GranteeType, command.GranteeId, userId, cancellationToken);
        await ValidateActionsAsync(connection, command.Actions, cancellationToken);
        if (command.GranteeType == "Workflow" && !(await policies.GetAsync(connection.ConnectorKey, cancellationToken)).AllowWorkflowConnections)
            throw ConnectorAuthorization.Access("ForbiddenByPolicy");
        if (command.ExpiresAt.HasValue && (command.ExpiresAt <= clock.UtcNow || command.ExpiresAt.Value.Kind != DateTimeKind.Utc))
            throw new ArgumentException("Grant expiry must be a future UTC timestamp.");
        ConnectorConnectionGrant grant;
        if (command.ExistingGrantId is { } id)
        {
            grant = await database.ConnectorConnectionGrants.SingleOrDefaultAsync(grant => grant.TenantId == authorization.TenantId
                && grant.ConnectionId == connectionId && grant.Id == id, cancellationToken) ?? throw new KeyNotFoundException("Grant not found.");
            Version(grant.RowVersion, command.ExpectedGrantVersion);
            if (grant.GranteeType != command.GranteeType || grant.GranteeId != command.GranteeId)
                throw new ArgumentException("A grant's grantee cannot be replaced.");
        }
        else grant = new() { TenantId = connection.TenantId, ConnectionId = connection.Id, GranteeType = command.GranteeType, GranteeId = command.GranteeId };
        await audit.LogAsync("Connector.GrantChangeRequested", "ConnectorConnectionGrant", grant.Id, authorization.TenantId, userId, null,
            ConnectorJson.Serialize(new { connectionId, command.GranteeType, command.GranteeId, before = grant.AllowedActionsJson, after = command.Actions, command.ExpiresAt }), cancellationToken);
        if (command.ExistingGrantId is null) database.ConnectorConnectionGrants.Add(grant);
        grant.AllowedActionsJson = ConnectorJson.Serialize(command.Actions.Distinct().ToList());
        grant.IssuedByUserId = userId; grant.ExpiresAt = command.ExpiresAt; grant.RevokedAt = null; grant.RevokedByUserId = null;
        // Fence a concurrent account disconnect as well as changes to this grant.
        database.Entry(connection).Property(account => account.UpdatedAt).IsModified = true;
        await SaveAsync(cancellationToken);
        return Detail(grant);
    }

    public async Task RevokeAsync(Guid grantId, ConnectionRequestCommand command, CancellationToken cancellationToken = default)
    {
        Guid userId = await authorization.RequireUserAsync("Integration.Read", cancellationToken);
        ConnectorConnectionGrant grant = await database.ConnectorConnectionGrants.SingleOrDefaultAsync(grant => grant.TenantId == authorization.TenantId
            && grant.Id == grantId, cancellationToken) ?? throw new KeyNotFoundException("Grant not found.");
        ConnectorConnection connection = await FindConnectionAsync(grant.ConnectionId, cancellationToken);
        await RequireManagerAsync(connection, userId, cancellationToken);
        Version(grant.RowVersion, command.ExpectedVersion);
        await audit.LogAsync("Connector.GrantRevocationRequested", "ConnectorConnectionGrant", grant.Id, authorization.TenantId, userId, null, null, cancellationToken);
        grant.RevokedAt = clock.UtcNow; grant.RevokedByUserId = userId;
        await SaveAsync(cancellationToken);
    }

    internal async Task<(Guid UserGrantId, Guid? WorkflowGrantId, Guid? AutomationGrantId)> StageConsentGrantsAsync(ConnectorConnection connection,
        ConnectorConnectionRequest request, Guid consentingUserId, CancellationToken cancellationToken)
    {
        await RequireManagerAsync(connection, consentingUserId, cancellationToken);
        if (!await authorization.IsActiveAsync(request.RequesterUserId, cancellationToken)) throw new PermissionDeniedException("Integration.Use");
        List<ConnectorActionRef> actions = ConnectorJson.Read<List<ConnectorActionRef>>(request.ActionsJson);
        await ValidateActionsAsync(connection, actions, cancellationToken);
        if (request.WorkflowId.HasValue && !(await policies.GetAsync(connection.ConnectorKey, cancellationToken)).AllowWorkflowConnections)
            throw ConnectorAuthorization.Access("ForbiddenByPolicy");
        if (request.AutomationIdentityId is { } automationId)
        {
            if (automation is null || !await automation.IsActiveAsync(automationId, cancellationToken)
                || request.ReviewedByUserId is not { } manager || request.ReviewedAt is null
                || !await authorization.CanManageAsync(manager, cancellationToken)
                || (request.ReviewerRoleId is { } reviewerRole && !await roles.IsMemberAsync(request.TenantId, manager, reviewerRole, cancellationToken)))
                throw new PermissionDeniedException("Integration.Manage", "Unattended account access needs current tenant administrator approval and account-owner consent.");
            await authorization.RequireActorAsync(new("User", manager, manager), "Workflow.Write", cancellationToken);
        }
        database.Entry(connection).Property(account => account.UpdatedAt).IsModified = true;
        ConnectorConnectionGrant userGrant = await StageAsync("User", request.RequesterUserId, null);
        ConnectorConnectionGrant? workflowGrant = request.WorkflowId is { } workflowId ? await StageAsync("Workflow", workflowId, request.WorkflowGrantId) : null;
        ConnectorConnectionGrant? automationGrant = request.AutomationIdentityId is { } identityId ? await StageAsync("AutomationIdentity", identityId, null) : null;
        return (userGrant.Id, workflowGrant?.Id, automationGrant?.Id);

        async Task<ConnectorConnectionGrant> StageAsync(string type, Guid granteeId, Guid? pinnedId)
        {
            ConnectorConnectionGrant? grant = pinnedId is { } id
                ? await database.ConnectorConnectionGrants.SingleOrDefaultAsync(grant => grant.TenantId == connection.TenantId && grant.Id == id
                    && grant.ConnectionId == connection.Id && grant.GranteeType == type && grant.GranteeId == granteeId, cancellationToken)
                : await database.ConnectorConnectionGrants.SingleOrDefaultAsync(grant => grant.TenantId == connection.TenantId && grant.ConnectionId == connection.Id
                    && grant.GranteeType == type && grant.GranteeId == granteeId && grant.RevokedAt == null, cancellationToken);
            if (pinnedId.HasValue && grant is null) throw new ConnectorAccessException("Conflict", "The pinned workflow grant is unavailable.");
            if (grant is null)
            {
                grant = new() { TenantId = connection.TenantId, ConnectionId = connection.Id, GranteeType = type, GranteeId = granteeId };
                database.ConnectorConnectionGrants.Add(grant);
            }
            IReadOnlyList<ConnectorActionRef> previous = grant.RevokedAt is null ? ConnectorJson.Read<List<ConnectorActionRef>>(grant.AllowedActionsJson) : [];
            grant.AllowedActionsJson = ConnectorJson.Serialize(previous.Union(actions).OrderBy(action => action.Key, StringComparer.Ordinal).ThenBy(action => action.Version).ToList());
            grant.IssuedByUserId = type == "AutomationIdentity" ? request.ReviewedByUserId!.Value : consentingUserId;
            grant.ExpiresAt = null; grant.RevokedAt = null; grant.RevokedByUserId = null;
            return grant;
        }
    }

    internal async Task RequireManagerAsync(ConnectorConnection connection, Guid userId, CancellationToken cancellationToken)
    {
        if (connection.TenantId != authorization.TenantId || !await authorization.IsActiveAsync(userId, cancellationToken)) throw new PermissionDeniedException("Integration.Manage");
        if (connection.Ownership == "User" && connection.OwnerUserId == userId)
        { await authorization.RequireActorAsync(new("User", userId, userId), "Integration.Use", cancellationToken); return; }
        if (connection.Ownership != "Tenant" || !await authorization.CanManageAsync(userId, cancellationToken)) throw new PermissionDeniedException("Integration.Manage");
    }
    private async Task ValidateTargetAsync(string type, Guid id, Guid issuer, CancellationToken cancellationToken)
    {
        if (type == "User" && await authorization.IsActiveAsync(id, cancellationToken)) return;
        if (type == "Role" && await roles.ExistsAsync(authorization.TenantId, id, cancellationToken)) return;
        if (type == "Workflow" && workflows is not null) { await workflows.RequireGrantTargetAsync(id, issuer, cancellationToken); return; }
        if (type == "AutomationIdentity" && automation is not null && await automation.IsActiveAsync(id, cancellationToken))
        {
            // Managing an unattended actor is an additional tenant authority, including on personal accounts.
            await authorization.RequireActorAsync(new("User", issuer, issuer), "Integration.Manage", cancellationToken);
            await authorization.RequireActorAsync(new("User", issuer, issuer), "Workflow.Write", cancellationToken);
            return;
        }
        throw new ArgumentException("An active tenant user, tenant role, authorized workflow or automation identity is required.");
    }
    private async Task ValidateActionsAsync(ConnectorConnection connection, IReadOnlyList<ConnectorActionRef> actions, CancellationToken cancellationToken)
    {
        if (actions is null || actions.Count is < 1 or > 100 || actions.Distinct().Count() != actions.Count) throw new ArgumentException("Provide unique, bounded action versions.");
        ConnectorPolicy policy = await policies.GetAsync(connection.ConnectorKey, cancellationToken);
        if (policy.Code != "Ready") throw ConnectorAuthorization.Access(policy.Code);
        if (connection.Status != "Active") throw ConnectorAuthorization.Access("ReauthorizationRequired");
        if (connection.Ownership == "User" && !policy.AllowUserConnections) throw ConnectorAuthorization.Access("ForbiddenByPolicy");
        IReadOnlyList<string> scopes = ConnectorJson.Read<List<string>>(connection.GrantedScopesJson);
        foreach (ConnectorActionRef action in actions)
        {
            ConnectorActionDescriptor descriptor = catalog.Resolve(connection.ConnectorKey, action);
            if (!policy.AllowedActions.Contains(action)) throw ConnectorAuthorization.Access("ForbiddenByPolicy");
            if (descriptor.RequiredScopes.Any(scope => !scopes.Contains(scope, StringComparer.Ordinal))) throw ConnectorAuthorization.Access("InsufficientScopes");
        }
    }
    private async Task<ConnectorConnection> FindConnectionAsync(Guid id, CancellationToken cancellationToken)
        => await database.ConnectorConnections.SingleOrDefaultAsync(connection => connection.TenantId == authorization.TenantId && connection.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Connection not found.");
    internal static void Version(byte[] version, string? expected)
    { if (expected is null || Convert.ToBase64String(version) != expected) throw new ConnectorAccessException("Conflict", "The connection or grant changed. Reload it before deciding."); }
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { throw new ConnectorAccessException("Conflict", "A concurrent connection/grant change requires a fresh review."); }
    }
    private static ConnectorGrantDetail Detail(ConnectorConnectionGrant grant) => new(grant.Id, grant.ConnectionId, grant.GranteeType, grant.GranteeId,
        ConnectorJson.Read<List<ConnectorActionRef>>(grant.AllowedActionsJson), grant.ExpiresAt, grant.RevokedAt, Convert.ToBase64String(grant.RowVersion));
}
