using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Platform.Services.Identity;

internal class PermissionService : IPermissionService
{
    private readonly PlatformDbContext _dbContext;

    public PermissionService(PlatformDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> HasPermissionAsync(
        Guid userId,
        string permissionKey,
        CancellationToken ct = default)
    {
        return await GrantedPermissions(userId).AnyAsync(permission => permission.Key == permissionKey, ct);
    }

    public async Task<List<string>> GetUserPermissionsAsync(Guid userId, CancellationToken ct = default)
    {
        return await GrantedPermissions(userId).Select(permission => permission.Key)
            .Distinct()
            .ToListAsync(ct);
    }

    private IQueryable<Entities.Identity.Permission> GrantedPermissions(Guid userId) => _dbContext.UserRoles.AsNoTracking()
        .Where(link => link.UserId == userId && !link.IsDeleted && !link.User.IsDeleted)
        .SelectMany(link => link.Role.RolePermissions)
        .Where(link => !link.IsDeleted && !link.Permission.IsDeleted)
        .Select(link => link.Permission);
}
