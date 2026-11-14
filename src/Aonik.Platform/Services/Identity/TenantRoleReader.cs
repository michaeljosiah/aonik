using Microsoft.EntityFrameworkCore;

using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;

namespace Aonik.Platform.Services.Identity;

internal sealed class TenantRoleReader(PlatformDbContext database, ITenantProvider tenantProvider) : ITenantRoleReader
{
    public Task<bool> ExistsAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken = default)
        => tenantProvider.TryGetCurrentTenantId(out Guid current) && current == tenantId
            ? database.Roles.AsNoTracking().AnyAsync(role => role.TenantId == tenantId && role.Id == roleId, cancellationToken)
            : Task.FromResult(false);

    public Task<bool> IsMemberAsync(Guid tenantId, Guid userId, Guid roleId, CancellationToken cancellationToken = default)
        => tenantProvider.TryGetCurrentTenantId(out Guid current) && current == tenantId
            ? database.UserRoles.AsNoTracking().AnyAsync(link => link.UserId == userId && link.RoleId == roleId
                && link.User.TenantId == tenantId && link.User.Status == "Active" && link.Role.TenantId == tenantId, cancellationToken)
            : Task.FromResult(false);
}
