using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Agents.Services.Workflows;

internal sealed class WorkflowAuthorization(
    ITenantProvider tenantProvider,
    ICurrentUserProvider currentUserProvider,
    IPermissionService permissionService)
{
    public Guid TenantId => tenantProvider.TryGetCurrentTenantId(out Guid id) && id != Guid.Empty
        ? id : throw new PermissionDeniedException("Workflow.Read", "A tenant context is required.");

    public async Task<Guid> RequireAsync(string permission, CancellationToken cancellationToken = default)
    {
        _ = TenantId;
        if (!currentUserProvider.TryGetCurrentUserId(out Guid userId) || userId == Guid.Empty
            || !await permissionService.HasPermissionAsync(userId, permission, cancellationToken))
        {
            throw new PermissionDeniedException(permission);
        }

        return userId;
    }
}
