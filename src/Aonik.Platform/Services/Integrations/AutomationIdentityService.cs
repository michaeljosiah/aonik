using Microsoft.EntityFrameworkCore;

using Aonik.Platform.Entities.Integrations;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Platform.Services.Integrations;

internal sealed class AutomationIdentityService(PlatformDbContext database, ConnectorAuthorization authorization,
    IWorkflowAutomationDefinitionReader workflows, IPermissionService permissions, IAuditLogWriter audit, IClock clock)
{
    public async Task<IReadOnlyList<AutomationIdentityDetail>> ListAsync(CancellationToken cancellationToken = default)
    {
        await RequireManagerAsync(cancellationToken);
        return (await database.AutomationIdentities.AsNoTracking().Where(identity => identity.TenantId == authorization.TenantId)
            .OrderBy(identity => identity.Name).Take(200).ToListAsync(cancellationToken)).Select(Detail).ToList();
    }

    public async Task<AutomationIdentityDetail?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await RequireManagerAsync(cancellationToken);
        AutomationIdentity? identity = await database.AutomationIdentities.AsNoTracking().SingleOrDefaultAsync(identity => identity.TenantId == authorization.TenantId && identity.Id == id, cancellationToken);
        return identity is null ? null : Detail(identity);
    }

    public async Task<AutomationIdentityUsage> UsageAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _ = await GetAsync(id, cancellationToken) ?? throw new KeyNotFoundException("Automation identity not found.");
        return await workflows.GetUsageAsync(id, cancellationToken);
    }

    public async Task<AutomationWorkflowCapability> CapabilitiesAsync(Guid workflowVersionId, CancellationToken cancellationToken = default)
    {
        Guid managerId = await RequireManagerAsync(cancellationToken);
        return await workflows.RequireAsync(workflowVersionId, managerId, cancellationToken);
    }

    public async Task<AutomationIdentityDetail> SaveAsync(Guid? id, AutomationIdentityCommand command, CancellationToken cancellationToken = default)
    {
        Guid managerId = await RequireManagerAsync(cancellationToken);
        HashSet<string> allowedPermissions = await ValidateAsync(command, managerId, cancellationToken);
        AutomationIdentity identity;
        if (id is { } existingId)
        {
            identity = await database.AutomationIdentities.SingleOrDefaultAsync(identity => identity.TenantId == authorization.TenantId && identity.Id == existingId, cancellationToken)
                ?? throw new KeyNotFoundException("Automation identity not found.");
            ConnectorGrantService.Version(identity.RowVersion, command.ExpectedVersion);
            if (identity.Status != "Active") throw new ConnectorAccessException("Conflict", "A disabled automation identity cannot be reactivated by editing grants.");
        }
        else
        {
            if (command.ExpectedVersion is not null) throw new ArgumentException("A new identity cannot replace an existing revision.");
            identity = new() { TenantId = authorization.TenantId };
        }
        await audit.LogAsync("AutomationIdentity.ChangeRequested", "AutomationIdentity", identity.Id, authorization.TenantId, managerId, null,
            ConnectorJson.Serialize(new { before = id.HasValue ? Detail(identity) : null, after = command }), cancellationToken);
        if (id is null) database.AutomationIdentities.Add(identity);
        identity.Name = command.Name.Trim(); identity.AccountableUserId = command.AccountableUserId; identity.ExpiresAt = command.ExpiresAt;
        identity.ConfiguredByUserId = managerId;
        identity.GrantsJson = ConnectorJson.Serialize(command.Grants.OrderBy(grant => grant.WorkflowVersionId).ToList());
        identity.PermissionsJson = ConnectorJson.Serialize(allowedPermissions.Order(StringComparer.Ordinal).ToList());
        await SaveChangesAsync(cancellationToken);
        return Detail(identity);
    }

    public async Task<AutomationIdentityDetail> DisableAsync(Guid id, ConnectionRequestCommand command, CancellationToken cancellationToken = default)
    {
        Guid managerId = await RequireManagerAsync(cancellationToken);
        AutomationIdentity identity = await database.AutomationIdentities.SingleOrDefaultAsync(identity => identity.TenantId == authorization.TenantId && identity.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Automation identity not found.");
        ConnectorGrantService.Version(identity.RowVersion, command.ExpectedVersion);
        await audit.LogAsync("AutomationIdentity.DisableRequested", "AutomationIdentity", id, authorization.TenantId, managerId, null,
            ConnectorJson.Serialize(new { before = Detail(identity), usage = await workflows.GetUsageAsync(id, cancellationToken) }), cancellationToken);
        identity.Status = "Disabled"; identity.DisabledAt = clock.UtcNow; identity.DisabledByUserId = managerId;
        await SaveChangesAsync(cancellationToken);
        return Detail(identity);
    }

    private async Task<Guid> RequireManagerAsync(CancellationToken cancellationToken)
    {
        Guid userId = await authorization.RequireUserAsync("Integration.Manage", cancellationToken);
        await authorization.RequireActorAsync(new("User", userId, userId), "Workflow.Write", cancellationToken);
        return userId;
    }

    private async Task<HashSet<string>> ValidateAsync(AutomationIdentityCommand command, Guid managerId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Name) || command.Name.Trim().Length > 160 || command.AccountableUserId == Guid.Empty
            || !await authorization.IsActiveAsync(command.AccountableUserId, cancellationToken)) throw new ArgumentException("Provide a name and an active accountable tenant administrator.");
        if (!await authorization.CanManageAsync(command.AccountableUserId, cancellationToken)
            || !await permissions.HasPermissionAsync(command.AccountableUserId, "Workflow.Write", cancellationToken)) throw new PermissionDeniedException("Integration.Manage");
        if (command.ExpiresAt is { } expiry && (expiry.Kind != DateTimeKind.Utc || expiry <= clock.UtcNow)) throw new ArgumentException("Expiry must be a future UTC timestamp.");
        if (command.Grants is null || command.Grants.Count > 20 || command.Grants.Select(grant => grant.WorkflowVersionId).Distinct().Count() != command.Grants.Count)
            throw new ArgumentException("Provide at most twenty distinct workflow version grants.");
        HashSet<string> result = ["Workflow.Read", "Workflow.Run", "Invoice.Read"];
        foreach (AutomationWorkflowGrant grant in command.Grants)
        {
            if (grant.WorkflowVersionId == Guid.Empty || grant.SubjectType != "Invoice" || grant.SubjectIds is null || grant.SubjectIds.Count > 1000
                || grant.SubjectIds.Any(id => id == Guid.Empty) || grant.SubjectIds.Distinct().Count() != grant.SubjectIds.Count
                || (grant.AllSubjects ? grant.SubjectIds.Count != 0 : grant.SubjectIds.Count == 0)
                || grant.Actions is null || grant.Actions.Count > 100 || grant.Actions.Distinct().Count() != grant.Actions.Count)
                throw new ArgumentException("Select a published invoice workflow, explicit subject scope and unique action versions.");
            AutomationWorkflowCapability capability = await workflows.RequireAsync(grant.WorkflowVersionId, managerId, cancellationToken);
            foreach (AutomationActionRef action in grant.Actions)
            {
                AutomationActionCapability descriptor = capability.Actions.SingleOrDefault(item => item.Key == action.Key && item.Version == action.Version)
                    ?? throw new ArgumentException("An automation action must belong to its exact published workflow version.");
                result.Add(descriptor.RequiredPermission);
                if (descriptor.Key.StartsWith("gmail.", StringComparison.Ordinal)) result.UnionWith(["Integration.Read", "Integration.Use"]);
            }
        }
        foreach (string permission in result)
            if (!await permissions.HasPermissionAsync(managerId, permission, cancellationToken)) throw new PermissionDeniedException(permission, "An administrator cannot delegate a permission they do not hold.");
        return result;
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new ConnectorAccessException("Conflict", "The automation identity changed. Review its current grants before saving."); }
    }
    private static AutomationIdentityDetail Detail(AutomationIdentity identity) => new(identity.Id, identity.Name, identity.AccountableUserId, identity.Status,
        identity.ExpiresAt, ConnectorJson.Read<List<AutomationWorkflowGrant>>(identity.GrantsJson), Convert.ToBase64String(identity.RowVersion), identity.ConfiguredByUserId);
}
