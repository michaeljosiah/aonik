using Microsoft.EntityFrameworkCore;

using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Platform.Services.Identity.AccessManagement;

/// <summary>
/// Tenant-scoped role CRUD plus role/permission-mapping and the
/// shared "build role detail" projection used by Get / Create /
/// Update endpoints. Permission catalogue listing also lives here.
/// </summary>
internal sealed class AccessRoleHelper
{
    private readonly PlatformDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ICurrentUserProvider _currentUserProvider;

    public AccessRoleHelper(
        PlatformDbContext dbContext,
        IClock clock,
        ICurrentUserProvider currentUserProvider)
    {
        _dbContext = dbContext;
        _clock = clock;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<PagedResult<AccessRoleSummary>> ListRolesAsync(
        Guid tenantId,
        ListRolesRequest request,
        CancellationToken cancellationToken)
    {
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

    public async Task<AccessRoleDetail?> GetRoleAsync(
        Guid tenantId,
        Guid roleId,
        CancellationToken cancellationToken)
    {
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
        Guid tenantId,
        CreateRoleRequest request,
        CancellationToken cancellationToken)
    {
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
            await UpdateRolePermissionsAsync(
                tenantId,
                role.Id,
                new UpdateRolePermissionsRequest(request.PermissionKeys),
                cancellationToken);
        }

        return await BuildRoleDetailAsync(role, cancellationToken);
    }

    public async Task<AccessRoleDetail> UpdateRoleAsync(
        Guid tenantId,
        Guid roleId,
        UpdateRoleRequest request,
        CancellationToken cancellationToken)
    {
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

    public async Task DeleteRoleAsync(
        Guid tenantId,
        Guid roleId,
        CancellationToken cancellationToken)
    {
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
        Guid tenantId,
        Guid roleId,
        UpdateRolePermissionsRequest request,
        CancellationToken cancellationToken)
    {
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

    public async Task<List<PermissionDefinition>> ListPermissionsAsync(CancellationToken cancellationToken)
    {
        var permissions = await _dbContext.Permissions
            .AsNoTracking()
            .OrderBy(permission => permission.Key)
            .Select(permission => new PermissionDefinition(
                permission.Key,
                permission.Description,
                AccessPermissionCategoryHelper.GetPermissionCategory(permission.Key)))
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
                AccessPermissionCategoryHelper.GetPermissionCategory(rp.Permission.Key)))
            .ToListAsync(cancellationToken);

        var users = await _dbContext.UserRoles
            .AsNoTracking()
            .Where(ur => ur.RoleId == role.Id)
            .Include(ur => ur.User)
            .OrderBy(ur => ur.User.Email)
            .Select(ur => new
            {
                ur.User.Id,
                Email = ur.User.Email ?? string.Empty,
                ur.User.Status,
                ur.User.LastLoginAt,
                RoleCount = _dbContext.UserRoles.Count(userRole => userRole.UserId == ur.User.Id),
                PartyInfo = _dbContext.UserParties
                    .Where(link => link.UserId == ur.User.Id && link.TenantId == role.TenantId)
                    .Join(_dbContext.Parties,
                        link => link.PartyId,
                        party => party.Id,
                        (link, party) => new
                        {
                            PartyId = (Guid?)party.Id,
                            party.DisplayName,
                            party.PartyType,
                            link.LinkType,
                            link.CreatedAt,
                            PersonProfile = _dbContext.PersonProfiles
                                .Where(pp => pp.PartyId == party.Id)
                                .Select(pp => new
                                {
                                    pp.PhotoUrl,
                                    pp.PhotoUrlSmall,
                                    pp.PhotoUrlTiny
                                })
                                .FirstOrDefault()
                        })
                    .OrderBy(link => link.CreatedAt)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var userSummaries = users.Select(user => new AccessUserSummary(
            user.Id,
            user.Email,
            null,
            user.Status,
            user.LastLoginAt,
            user.RoleCount,
            user.PartyInfo?.PartyId,
            user.PartyInfo?.DisplayName,
            user.PartyInfo?.PartyType,
            user.PartyInfo?.LinkType,
            user.PartyInfo?.PersonProfile?.PhotoUrl,
            user.PartyInfo?.PersonProfile?.PhotoUrlSmall,
            user.PartyInfo?.PersonProfile?.PhotoUrlTiny)).ToList();

        return new AccessRoleDetail(
            role.Id,
            role.Name,
            null,
            permissions,
            userSummaries);
    }
}
