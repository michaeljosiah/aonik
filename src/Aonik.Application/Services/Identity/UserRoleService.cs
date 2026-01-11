using System.Text.Json;
using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.Identity;
using Aonik.Application.Services.Compliance;
using Aonik.Domain.Identity.Entities;
using Aonik.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Services.Identity;

public class UserRoleService : IUserRoleService
{
    private readonly IAonikDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly IClock _clock;
    private readonly ICurrentUserProvider _currentUserProvider;

    public UserRoleService(
        IAonikDbContext dbContext,
        ITenantProvider tenantProvider,
        IAuditLogWriter auditLogWriter,
        IClock clock,
        ICurrentUserProvider currentUserProvider)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _auditLogWriter = auditLogWriter;
        _clock = clock;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<UserRoleResponse> GetUserRolesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var internalUserId = await GetUserInternalIdAsync(userId, tenantId, cancellationToken);

        return await BuildUserRoleResponseAsync(userId, internalUserId, cancellationToken);
    }

    public async Task<UserRoleResponse> AssignRoleAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var internalUserId = await GetUserInternalIdAsync(userId, tenantId, cancellationToken);
        var role = await EnsureRoleInTenantAsync(roleId, tenantId, cancellationToken);

        var existing = await _dbContext.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == internalUserId && ur.RoleId == roleId, cancellationToken);

        if (existing != null)
        {
            return await BuildUserRoleResponseAsync(userId, internalUserId, cancellationToken);
        }

        var now = _clock.UtcNow;
        var currentUserId = _currentUserProvider.GetCurrentUserId();

        var userRole = new UserRole
        {
            UserId = internalUserId,
            RoleId = roleId,
            CreatedAt = now,
            CreatedBy = currentUserId
        };

        _dbContext.UserRoles.Add(userRole);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.LogAsync(
            "UserRoleAssigned",
            "UserRole",
            userRole.Id,
            JsonSerializer.Serialize(new { userId, roleId, role.Name }),
            cancellationToken);

        return await BuildUserRoleResponseAsync(userId, internalUserId, cancellationToken);
    }

    public async Task<UserRoleResponse> RemoveRoleAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var internalUserId = await GetUserInternalIdAsync(userId, tenantId, cancellationToken);
        var role = await EnsureRoleInTenantAsync(roleId, tenantId, cancellationToken);

        var userRole = await _dbContext.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == internalUserId && ur.RoleId == roleId, cancellationToken);

        if (userRole == null)
        {
            return await BuildUserRoleResponseAsync(userId, internalUserId, cancellationToken);
        }

        _dbContext.UserRoles.Remove(userRole);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.LogAsync(
            "UserRoleRemoved",
            "UserRole",
            userRole.Id,
            JsonSerializer.Serialize(new { userId, roleId, role.Name }),
            cancellationToken);

        return await BuildUserRoleResponseAsync(userId, internalUserId, cancellationToken);
    }

    private async Task<Guid> GetUserInternalIdAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken)
    {
        var internalUserId = await _dbContext.Users
            .Where(u => u.UserId == userId && u.TenantId == tenantId)
            .Select(u => u.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (internalUserId == Guid.Empty)
        {
            throw new InvalidOperationException($"User {userId} not found in tenant {tenantId}");
        }

        return internalUserId;
    }

    private async Task<Role> EnsureRoleInTenantAsync(Guid roleId, Guid tenantId, CancellationToken cancellationToken)
    {
        var role = await _dbContext.Roles
            .FirstOrDefaultAsync(r => r.RoleId == roleId && r.TenantId == tenantId, cancellationToken);

        if (role == null)
        {
            throw new InvalidOperationException($"Role {roleId} not found in tenant {tenantId}");
        }

        return role;
    }

    private async Task<UserRoleResponse> BuildUserRoleResponseAsync(
        Guid userId,
        Guid internalUserId,
        CancellationToken cancellationToken)
    {
        var roles = await _dbContext.UserRoles
            .Where(ur => ur.UserId == internalUserId)
            .Include(ur => ur.Role)
            .Select(ur => new RoleSummary(ur.Role.RoleId, ur.Role.Name))
            .OrderBy(role => role.Name)
            .ToListAsync(cancellationToken);

        return new UserRoleResponse(userId, roles);
    }
}
