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
        return await _dbContext.UserRoles
            .Where(ur => ur.UserId == userId)
            .SelectMany(ur => ur.Role.RolePermissions)
            .AnyAsync(rp => rp.Permission.Key == permissionKey, ct);
    }

    public async Task<List<string>> GetUserPermissionsAsync(Guid userId, CancellationToken ct = default)
    {
        return await _dbContext.UserRoles
            .Where(ur => ur.UserId == userId)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Key)
            .Distinct()
            .ToListAsync(ct);
    }
}
