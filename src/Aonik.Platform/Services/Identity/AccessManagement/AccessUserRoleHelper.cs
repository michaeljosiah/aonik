using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Compliance;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Compliance;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Observability;

namespace Aonik.Platform.Services.Identity.AccessManagement;

/// <summary>
/// Synchronizes a user's role assignments. Validates that all
/// requested role IDs belong to the current tenant before mutating,
/// then writes per-role audit entries for adds and removes.
/// </summary>
internal sealed class AccessUserRoleHelper
{
    private readonly PlatformDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly ICorrelationContext _correlationContext;

    public AccessUserRoleHelper(
        PlatformDbContext dbContext,
        IClock clock,
        ICurrentUserProvider currentUserProvider,
        IAuditLogWriter auditLogWriter,
        ICorrelationContext correlationContext)
    {
        _dbContext = dbContext;
        _clock = clock;
        _currentUserProvider = currentUserProvider;
        _auditLogWriter = auditLogWriter;
        _correlationContext = correlationContext;
    }

    public async Task UpdateUserRolesAsync(
        Guid tenantId,
        Guid userId,
        UpdateUserRolesRequest request,
        CancellationToken cancellationToken)
    {
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
}
