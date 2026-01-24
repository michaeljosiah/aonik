using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Observability;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.Identity;
using Aonik.Application.Services.Compliance;
using Aonik.Domain.Identity.Entities;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Application.Services.Identity;

public class AccessManagementService : IAccessManagementService
{
    private readonly IAonikDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IPermissionService _permissionService;
    private readonly IClock _clock;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly ICorrelationContext _correlationContext;

    public AccessManagementService(
        IAonikDbContext dbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IPermissionService permissionService,
        IClock clock,
        IAuditLogWriter auditLogWriter,
        ICorrelationContext correlationContext)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _permissionService = permissionService;
        _clock = clock;
        _auditLogWriter = auditLogWriter;
        _correlationContext = correlationContext;
    }

    public async Task<PagedResult<AccessUserSummary>> ListUsersAsync(
        ListUsersRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Users.Read", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var query = _dbContext.Users
            .AsNoTracking()
            .Where(user => user.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            query = query.Where(user => user.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(user => (user.Email ?? string.Empty).Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(user => user.Email ?? string.Empty)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(user => new AccessUserSummary(
                user.Id,
                user.Email ?? string.Empty,
                null,
                user.Status,
                user.LastLoginAt,
                _dbContext.UserRoles.Count(ur => ur.UserId == user.Id)))
            .ToListAsync(cancellationToken);

        return new PagedResult<AccessUserSummary>(
            items,
            request.PageNumber,
            request.PageSize,
            totalCount);
    }

    public async Task<AccessUserDetail?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Users.Read", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (user == null)
        {
            return null;
        }

        var roles = await _dbContext.UserRoles
            .Where(ur => ur.UserId == userId)
            .Include(ur => ur.Role)
            .OrderBy(ur => ur.Role.Name)
            .Select(ur => new RoleSummary(ur.Role.Id, ur.Role.Name))
            .ToListAsync(cancellationToken);

        var permissions = await _permissionService.GetUserPermissionsAsync(userId, cancellationToken);

        return new AccessUserDetail(
            user.Id,
            user.Email ?? string.Empty,
            null,
            user.Status,
            user.CreatedAt,
            user.LastLoginAt,
            roles,
            permissions);
    }

    public async Task InviteUserAsync(InviteUserRequest request, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Users.Invite", cancellationToken);
        throw new InvalidOperationException("User invitations are not supported yet.");
    }

    public async Task UpdateUserRolesAsync(
        Guid userId,
        UpdateUserRolesRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Users.Manage", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var userExists = await _dbContext.Users
            .AnyAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (!userExists)
        {
            throw new InvalidOperationException($"User {userId} not found in tenant {tenantId}");
        }

        var roleIds = request.RoleIds.Distinct().ToList();
        if (roleIds.Count > 0)
        {
            var rolesInTenant = await _dbContext.Roles
                .Where(role => role.TenantId == tenantId && roleIds.Contains(role.Id))
                .Select(role => role.Id)
                .ToListAsync(cancellationToken);

            if (rolesInTenant.Count != roleIds.Count)
            {
                throw new InvalidOperationException("One or more roles were not found in the tenant.");
            }
        }

        var existingRoles = await _dbContext.UserRoles
            .Where(ur => ur.UserId == userId)
            .ToListAsync(cancellationToken);

        var existingRoleIds = existingRoles.Select(ur => ur.RoleId).ToHashSet();
        var rolesToRemove = existingRoles.Where(ur => !roleIds.Contains(ur.RoleId)).ToList();
        var rolesToAdd = roleIds
            .Where(roleId => !existingRoleIds.Contains(roleId))
            .Select(roleId => new UserRole
            {
                UserId = userId,
                RoleId = roleId,
                CreatedAt = _clock.UtcNow,
                CreatedBy = _currentUserProvider.GetCurrentUserId()
            })
            .ToList();

        var roleIdsForAudit = roleIds
            .Concat(rolesToRemove.Select(role => role.RoleId))
            .Distinct()
            .ToList();

        var roleLookup = roleIdsForAudit.Count == 0
            ? new Dictionary<Guid, string>()
            : await _dbContext.Roles
                .Where(role => role.TenantId == tenantId && roleIdsForAudit.Contains(role.Id))
                .Select(role => new { role.Id, role.Name })
                .ToDictionaryAsync(role => role.Id, role => role.Name, cancellationToken);

        if (rolesToRemove.Count > 0)
        {
            _dbContext.UserRoles.RemoveRange(rolesToRemove);
        }

        if (rolesToAdd.Count > 0)
        {
            _dbContext.UserRoles.AddRange(rolesToAdd);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var currentUserId = _currentUserProvider.GetCurrentUserId();

        foreach (var role in rolesToAdd)
        {
            var roleName = roleLookup.TryGetValue(role.RoleId, out var name) ? name : string.Empty;
            await _auditLogWriter.LogAsync(
                AuditEventNames.UserRoleAssigned,
                "UserRole",
                role.Id,
                tenantId,
                currentUserId,
                _correlationContext.CorrelationId,
                JsonSerializer.Serialize(new { userId, roleId = role.RoleId, roleName }),
                cancellationToken);
        }

        foreach (var role in rolesToRemove)
        {
            var roleName = roleLookup.TryGetValue(role.RoleId, out var name) ? name : string.Empty;
            await _auditLogWriter.LogAsync(
                AuditEventNames.UserRoleRemoved,
                "UserRole",
                role.Id,
                tenantId,
                currentUserId,
                _correlationContext.CorrelationId,
                JsonSerializer.Serialize(new { userId, roleId = role.RoleId, roleName }),
                cancellationToken);
        }
    }

    public async Task ActivateUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Users.Manage", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (user == null)
        {
            throw new InvalidOperationException($"User {userId} not found in tenant {tenantId}");
        }

        if (user.Status == "Active")
        {
            return;
        }

        user.Status = "Active";
        user.UpdatedAt = _clock.UtcNow;
        user.UpdatedBy = _currentUserProvider.GetCurrentUserId();
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeactivateUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Users.Deactivate", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (user == null)
        {
            throw new InvalidOperationException($"User {userId} not found in tenant {tenantId}");
        }

        if (user.Status == "Deactivated")
        {
            return;
        }

        user.Status = "Deactivated";
        user.UpdatedAt = _clock.UtcNow;
        user.UpdatedBy = _currentUserProvider.GetCurrentUserId();
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<AccessRoleSummary>> ListRolesAsync(
        ListRolesRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Roles.Read", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var query = _dbContext.Roles
            .AsNoTracking()
            .Where(role => role.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(role => role.Name.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(role => role.Name)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(role => new AccessRoleSummary(
                role.Id,
                role.Name,
                null,
                _dbContext.RolePermissions.Count(rp => rp.RoleId == role.Id),
                _dbContext.UserRoles.Count(ur => ur.RoleId == role.Id)))
            .ToListAsync(cancellationToken);

        return new PagedResult<AccessRoleSummary>(
            items,
            request.PageNumber,
            request.PageSize,
            totalCount);
    }

    public async Task<AccessRoleDetail?> GetRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Roles.Read", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var role = await _dbContext.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == roleId && r.TenantId == tenantId, cancellationToken);

        if (role == null)
        {
            return null;
        }

        return await BuildRoleDetailAsync(role, cancellationToken);
    }

    public async Task<AccessRoleDetail> CreateRoleAsync(
        CreateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Roles.Create", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Role name is required", nameof(request.Name));
        }

        var trimmedName = request.Name.Trim();

        var exists = await _dbContext.Roles
            .AnyAsync(role => role.TenantId == tenantId && role.Name == trimmedName, cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException($"Role '{trimmedName}' already exists in tenant {tenantId}");
        }

        var userId = _currentUserProvider.GetCurrentUserId();
        var now = _clock.UtcNow;

        var role = new Role
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = trimmedName,
            CreatedAt = now,
            CreatedBy = userId
        };

        _dbContext.Roles.Add(role);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (request.PermissionKeys.Count > 0)
        {
            await UpdateRolePermissionsAsync(role.Id, new UpdateRolePermissionsRequest(request.PermissionKeys), cancellationToken);
        }

        return await BuildRoleDetailAsync(role, cancellationToken);
    }

    public async Task<AccessRoleDetail> UpdateRoleAsync(
        Guid roleId,
        UpdateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Roles.Update", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var role = await _dbContext.Roles
            .FirstOrDefaultAsync(r => r.Id == roleId && r.TenantId == tenantId, cancellationToken);

        if (role == null)
        {
            throw new InvalidOperationException($"Role {roleId} not found in tenant {tenantId}");
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            var trimmedName = request.Name.Trim();
            var exists = await _dbContext.Roles
                .AnyAsync(r => r.TenantId == tenantId && r.Name == trimmedName && r.Id != roleId, cancellationToken);

            if (exists)
            {
                throw new InvalidOperationException($"Role '{trimmedName}' already exists in tenant {tenantId}");
            }

            role.Name = trimmedName;
        }

        role.UpdatedAt = _clock.UtcNow;
        role.UpdatedBy = _currentUserProvider.GetCurrentUserId();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await BuildRoleDetailAsync(role, cancellationToken);
    }

    public async Task DeleteRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Roles.Delete", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var role = await _dbContext.Roles
            .FirstOrDefaultAsync(r => r.Id == roleId && r.TenantId == tenantId, cancellationToken);

        if (role == null)
        {
            throw new InvalidOperationException($"Role {roleId} not found in tenant {tenantId}");
        }

        var assignedUsers = await _dbContext.UserRoles
            .AnyAsync(ur => ur.RoleId == roleId, cancellationToken);

        if (assignedUsers)
        {
            throw new InvalidOperationException("Cannot delete a role that is assigned to users.");
        }

        _dbContext.Roles.Remove(role);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateRolePermissionsAsync(
        Guid roleId,
        UpdateRolePermissionsRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Roles.Update", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var role = await _dbContext.Roles
            .FirstOrDefaultAsync(r => r.Id == roleId && r.TenantId == tenantId, cancellationToken);

        if (role == null)
        {
            throw new InvalidOperationException($"Role {roleId} not found in tenant {tenantId}");
        }

        var permissionKeys = request.PermissionKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var permissions = permissionKeys.Count == 0
            ? new List<Permission>()
            : await _dbContext.Permissions
                .Where(permission => permissionKeys.Contains(permission.Key))
                .ToListAsync(cancellationToken);

        if (permissions.Count != permissionKeys.Count)
        {
            throw new InvalidOperationException("One or more permissions were not found.");
        }

        var existing = await _dbContext.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .ToListAsync(cancellationToken);

        var existingIds = existing.Select(rp => rp.PermissionId).ToHashSet();
        var targetIds = permissions.Select(permission => permission.Id).ToHashSet();

        var toRemove = existing.Where(rp => !targetIds.Contains(rp.PermissionId)).ToList();
        var toAdd = targetIds
            .Where(permissionId => !existingIds.Contains(permissionId))
            .Select(permissionId => new RolePermission
            {
                RoleId = roleId,
                PermissionId = permissionId,
                CreatedAt = _clock.UtcNow,
                CreatedBy = _currentUserProvider.GetCurrentUserId()
            })
            .ToList();

        if (toRemove.Count > 0)
        {
            _dbContext.RolePermissions.RemoveRange(toRemove);
        }

        if (toAdd.Count > 0)
        {
            _dbContext.RolePermissions.AddRange(toAdd);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<PermissionDefinition>> ListPermissionsAsync(CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Permissions.Read", cancellationToken);

        var permissions = await _dbContext.Permissions
            .AsNoTracking()
            .OrderBy(permission => permission.Key)
            .Select(permission => new PermissionDefinition(
                permission.Key,
                permission.Description,
                GetPermissionCategory(permission.Key)))
            .ToListAsync(cancellationToken);

        return permissions;
    }

    private async Task<AccessRoleDetail> BuildRoleDetailAsync(Role role, CancellationToken cancellationToken)
    {
        var permissions = await _dbContext.RolePermissions
            .AsNoTracking()
            .Where(rp => rp.RoleId == role.Id)
            .Include(rp => rp.Permission)
            .OrderBy(rp => rp.Permission.Key)
            .Select(rp => new PermissionDefinition(
                rp.Permission.Key,
                rp.Permission.Description,
                GetPermissionCategory(rp.Permission.Key)))
            .ToListAsync(cancellationToken);

        var users = await _dbContext.UserRoles
            .AsNoTracking()
            .Where(ur => ur.RoleId == role.Id)
            .Include(ur => ur.User)
            .OrderBy(ur => ur.User.Email)
            .Select(ur => new AccessUserSummary(
                ur.User.Id,
                ur.User.Email ?? string.Empty,
                null,
                ur.User.Status,
                ur.User.LastLoginAt,
                _dbContext.UserRoles.Count(userRole => userRole.UserId == ur.User.Id)))
            .ToListAsync(cancellationToken);

        return new AccessRoleDetail(
            role.Id,
            role.Name,
            null,
            permissions,
            users);
    }

    private static string GetPermissionCategory(string permissionKey)
    {
        if (string.IsNullOrWhiteSpace(permissionKey))
        {
            return "General";
        }

        var parts = permissionKey.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : "General";
    }

    private async Task EnsurePermissionAsync(string permissionKey, CancellationToken cancellationToken)
    {
        var userId = _currentUserProvider.GetCurrentUserId();
        if (!userId.HasValue)
        {
            throw new InvalidOperationException("Authenticated user is required.");
        }

        var hasPermission = await _permissionService.HasPermissionAsync(userId.Value, permissionKey, cancellationToken);
        if (!hasPermission)
        {
            throw new InvalidOperationException($"Permission {permissionKey} is required.");
        }
    }
}
