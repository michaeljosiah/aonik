using Microsoft.EntityFrameworkCore;

using Aonik.Platform.Entities.Integrations;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Platform.Services.Integrations;

internal sealed class AutomationIdentityAuthorization(PlatformDbContext database, ITenantProvider tenantProvider, IClock clock)
    : IAutomationIdentityAuthorization
{
    public async Task<bool> IsActiveAsync(Guid identityId, CancellationToken cancellationToken = default)
        => await database.AutomationIdentities.AsNoTracking().AnyAsync(identity => identity.TenantId == tenantProvider.GetCurrentTenantId()
            && identity.Id == identityId && identity.Status == "Active" && identity.DisabledAt == null
            && (identity.ExpiresAt == null || identity.ExpiresAt > clock.UtcNow), cancellationToken);

    public async Task<Guid> RequireAsync(WorkflowActor actor, string permission, string? subjectType = null, Guid? subjectId = null,
        AutomationActionRef? action = null, CancellationToken cancellationToken = default)
    {
        if (actor.Type != "AutomationIdentity" || actor.Id == Guid.Empty || actor.InitiatingUserId.HasValue || actor.WorkflowVersionId is null)
            throw new PermissionDeniedException(permission);
        AutomationIdentity? identity = await database.AutomationIdentities.AsNoTracking().SingleOrDefaultAsync(identity => identity.TenantId == tenantProvider.GetCurrentTenantId()
            && identity.Id == actor.Id && identity.Status == "Active" && identity.DisabledAt == null
            && (identity.ExpiresAt == null || identity.ExpiresAt > clock.UtcNow), cancellationToken);
        if (identity is null) throw new PermissionDeniedException(permission);
        try
        {
            AutomationWorkflowGrant? grant = ConnectorJson.Read<List<AutomationWorkflowGrant>>(identity.GrantsJson)
                .SingleOrDefault(grant => grant.WorkflowVersionId == actor.WorkflowVersionId);
            if (grant is null || !ConnectorJson.Read<List<string>>(identity.PermissionsJson).Contains(permission, StringComparer.Ordinal)
                || (subjectType is not null && (grant.SubjectType != subjectType || subjectId is null || subjectId == Guid.Empty
                    || (!grant.AllSubjects && !grant.SubjectIds.Contains(subjectId.Value))))
                || (action is not null && !grant.Actions.Contains(action))) throw new PermissionDeniedException(permission);
        }
        catch (System.Text.Json.JsonException) { throw new PermissionDeniedException(permission); }
        return identity.ConfiguredByUserId;
    }
}
