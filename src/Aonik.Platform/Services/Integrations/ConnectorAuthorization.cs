using Microsoft.EntityFrameworkCore;

using Aonik.Platform.Entities.Integrations;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Platform.Services.Integrations;

internal sealed class ConnectorAuthorization(PlatformDbContext database, ITenantProvider tenantProvider, ICurrentUserProvider currentUser,
    IPermissionService permissions, IUserDirectoryReader users, ITenantRoleReader roles, ConnectorPolicyService policies, IClock clock)
{
    public Guid TenantId => tenantProvider.TryGetCurrentTenantId(out Guid id) && id != Guid.Empty
        ? id : throw new PermissionDeniedException("Integration.Read", "A tenant context is required.");

    public async Task<Guid> RequireUserAsync(string permission, CancellationToken cancellationToken = default)
    {
        if (!currentUser.TryGetCurrentUserId(out Guid userId)) throw new PermissionDeniedException(permission);
        await RequireActorAsync(new("User", userId, userId), permission, cancellationToken);
        return userId;
    }

    public async Task RequireActorAsync(WorkflowActor actor, string permission, CancellationToken cancellationToken = default)
    {
        if (actor.Type != "User" || actor.Id == Guid.Empty || !await IsActiveAsync(actor.Id, cancellationToken)
            || !await permissions.HasPermissionAsync(actor.Id, permission, cancellationToken)) throw new PermissionDeniedException(permission);
    }

    public async Task<bool> CanManageAsync(Guid userId, CancellationToken cancellationToken = default)
        => await IsActiveAsync(userId, cancellationToken) && await permissions.HasPermissionAsync(userId, "Integration.Manage", cancellationToken);

    public async Task<ConnectorConnection> RequireUseAsync(Guid connectionId, ConnectorActionDescriptor action, ConnectorUseContext context,
        CancellationToken cancellationToken = default)
    {
        await RequireActorAsync(context.Actor, "Integration.Use", cancellationToken);
        ConnectorConnection connection = await database.ConnectorConnections.AsNoTracking().SingleOrDefaultAsync(connection => connection.TenantId == TenantId
            && connection.Id == connectionId && connection.ConnectorKey == action.ConnectorKey, cancellationToken)
            ?? throw Access("NoAuthorizedConnection");
        if (!await HasActorGrantAsync(connection, context.Actor, new(action.Key, action.Version), cancellationToken))
            throw Access(connection.Ownership == "User" && connection.OwnerUserId == context.Actor.Id ? "ConnectionGrantRequired" : "NoAuthorizedConnection");
        if (connection.Ownership == "User" && (connection.OwnerUserId is not { } owner || !await IsActiveAsync(owner, cancellationToken)))
            throw Access("NoAuthorizedConnection");
        ConnectorPolicy policy = await policies.GetAsync(action.ConnectorKey, cancellationToken);
        if (policy.Code != "Ready") throw Access(policy.Code);
        if (!policy.AllowedActions.Contains(new(action.Key, action.Version)) || (connection.Ownership == "User" && !policy.AllowUserConnections))
            throw Access("ForbiddenByPolicy");
        if (context.WorkflowId is { } workflowId)
        {
            await RequireActorAsync(context.Actor, "Workflow.Run", cancellationToken);
            await RequireActorAsync(context.Actor, "Workflow.Read", cancellationToken);
            if (!policy.AllowWorkflowConnections || context.PinnedGrantId is null || context.AllowedActions?.Contains(new(action.Key, action.Version)) != true)
                throw Access("WorkflowBindingRequired");
            ConnectorConnectionGrant? workflowGrant = await database.ConnectorConnectionGrants.AsNoTracking().SingleOrDefaultAsync(grant => grant.TenantId == TenantId
                && grant.Id == context.PinnedGrantId && grant.ConnectionId == connection.Id && grant.GranteeType == "Workflow" && grant.GranteeId == workflowId
                && grant.RevokedAt == null && (grant.ExpiresAt == null || grant.ExpiresAt > clock.UtcNow), cancellationToken);
            if (workflowGrant is null || !Allows(workflowGrant, new(action.Key, action.Version))) throw Access("ConnectionGrantRequired");
        }
        if (connection.Status == "ReauthorizationRequired") throw Access("ReauthorizationRequired");
        if (connection.Status != "Active") throw Access("NoAuthorizedConnection");
        IReadOnlyList<string> granted = ConnectorJson.Read<List<string>>(connection.GrantedScopesJson);
        if (action.RequiredScopes.Any(scope => !granted.Contains(scope, StringComparer.Ordinal))) throw Access("InsufficientScopes");
        return connection;
    }

    public async Task<bool> HasActorGrantAsync(ConnectorConnection connection, WorkflowActor actor, ConnectorActionRef action,
        CancellationToken cancellationToken = default)
    {
        List<ConnectorConnectionGrant> grants = await database.ConnectorConnectionGrants.AsNoTracking().Where(grant => grant.TenantId == TenantId
            && grant.ConnectionId == connection.Id && grant.RevokedAt == null && (grant.ExpiresAt == null || grant.ExpiresAt > clock.UtcNow))
            .ToListAsync(cancellationToken);
        foreach (ConnectorConnectionGrant grant in grants.Where(grant => Allows(grant, action)))
        {
            if (grant.GranteeType == "User" && actor.Type == "User" && grant.GranteeId == actor.Id) return true;
            if (grant.GranteeType == "Role" && actor.Type == "User" && await roles.IsMemberAsync(TenantId, actor.Id, grant.GranteeId, cancellationToken)) return true;
        }
        return false;
    }

    public async Task<bool> IsActiveAsync(Guid userId, CancellationToken cancellationToken = default)
        => (await users.GetByIdsAsync(TenantId, [userId], cancellationToken)).Any(user => user.UserId == userId && user.Status == "Active");
    internal static bool Allows(ConnectorConnectionGrant grant, ConnectorActionRef action)
    {
        try { return ConnectorJson.Read<List<ConnectorActionRef>>(grant.AllowedActionsJson).Contains(action); }
        catch (Exception exception) when (exception is System.Text.Json.JsonException or ArgumentException) { return false; }
    }
    internal static ConnectorAccessException Access(string code) => new(code, code switch
    {
        "ConnectorUnavailable" => "The connector or its OAuth setup is not provisioned.",
        "TenantNotEnabled" => "The connector has not been enabled for this tenant.",
        "ForbiddenByPolicy" => "Current platform or tenant policy denies this connector use.",
        "ReauthorizationRequired" => "The account owner must reconnect this account.",
        "InsufficientScopes" => "The account needs consent for the required scopes.",
        "ConnectionGrantRequired" => "An explicit use grant is required.",
        "WorkflowBindingRequired" => "The published workflow needs an exact authorized connection binding.",
        _ => "No authorized connection satisfies this action.",
    });
}
