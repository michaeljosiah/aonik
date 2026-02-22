using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Observability;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.Identity;
using Aonik.Application.Services;
using Aonik.Application.Services.Compliance;
using Aonik.Platform.Entities.Identity;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Application.Services.Identity;

public class UserRoleService : AdminServiceBase, IUserRoleService
{
    private readonly IAonikDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly IClock _clock;
    private readonly ICorrelationContext _correlationContext;

    public UserRoleService(
        IAonikDbContext dbContext,
        ITenantProvider tenantProvider,
        IAuditLogWriter auditLogWriter,
        IClock clock,
        ICurrentUserProvider currentUserProvider,
        ICorrelationContext correlationContext,
        IPermissionService permissionService)
        : base(currentUserProvider, permissionService)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _auditLogWriter = auditLogWriter;
        _clock = clock;
        _correlationContext = correlationContext;
    }

    public async Task<UserRoleResponse> GetUserRolesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Users.Read", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();
        await EnsureUserInTenantAsync(userId, tenantId, cancellationToken);

        return await BuildUserRoleResponseAsync(userId, cancellationToken);
    }

    public async Task<UserRoleResponse> AssignRoleAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Users.Manage", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        await EnsureUserInTenantAsync(userId, tenantId, cancellationToken);
        var role = await EnsureRoleInTenantAsync(roleId, tenantId, cancellationToken);

        var existing = await _dbContext.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId, cancellationToken);

        if (existing != null)
        {
            return await BuildUserRoleResponseAsync(userId, cancellationToken);
        }

        var now = _clock.UtcNow;
        var currentUserId = CurrentUserProvider.GetCurrentUserId();

        var userRole = new UserRole
        {
            UserId = userId,
            RoleId = roleId,
            CreatedAt = now,
            CreatedBy = currentUserId
        };

        _dbContext.UserRoles.Add(userRole);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.LogAsync(
            AuditEventNames.UserRoleAssigned,
            "UserRole",
            userRole.Id,
            tenantId,
            currentUserId,
            _correlationContext.CorrelationId,
            JsonSerializer.Serialize(new { userId, roleId, role.Name }),
            cancellationToken);

        return await BuildUserRoleResponseAsync(userId, cancellationToken);
    }

    public async Task<UserRoleResponse> RemoveRoleAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Users.Manage", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        await EnsureUserInTenantAsync(userId, tenantId, cancellationToken);
        var role = await EnsureRoleInTenantAsync(roleId, tenantId, cancellationToken);

        var userRole = await _dbContext.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId, cancellationToken);

        if (userRole == null)
        {
            return await BuildUserRoleResponseAsync(userId, cancellationToken);
        }

        _dbContext.UserRoles.Remove(userRole);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.LogAsync(
            AuditEventNames.UserRoleRemoved,
            "UserRole",
            userRole.Id,
            tenantId,
            CurrentUserProvider.GetCurrentUserId(),
            _correlationContext.CorrelationId,
            JsonSerializer.Serialize(new { userId, roleId, role.Name }),
            cancellationToken);

        return await BuildUserRoleResponseAsync(userId, cancellationToken);
    }

    private async Task EnsureUserInTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken)
    {
        var exists = await _dbContext.Users
            .AnyAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (!exists)
        {
            throw new InvalidOperationException($"User {userId} not found in tenant {tenantId}");
        }
    }

    private async Task<Role> EnsureRoleInTenantAsync(Guid roleId, Guid tenantId, CancellationToken cancellationToken)
    {
        var role = await _dbContext.Roles
.FirstOrDefaultAsync(r => r.Id == roleId && r.TenantId == tenantId, cancellationToken);


        if (role == null)
        {
            throw new InvalidOperationException($"Role {roleId} not found in tenant {tenantId}");
        }

        return role;
    }

    private async Task<UserRoleResponse> BuildUserRoleResponseAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var roles = await _dbContext.UserRoles
            .Where(ur => ur.UserId == userId)
            .Include(ur => ur.Role)
.Select(ur => new RoleSummary(ur.Role.Id, ur.Role.Name))

            .OrderBy(role => role.Name)
            .ToListAsync(cancellationToken);

        return new UserRoleResponse(userId, roles);
    }

}
