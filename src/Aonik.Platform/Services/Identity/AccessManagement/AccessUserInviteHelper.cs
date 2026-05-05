using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Compliance;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Compliance;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Observability;

namespace Aonik.Platform.Services.Identity.AccessManagement;

/// <summary>
/// Handles the user-invite path: validates input, defers to the
/// pending-tenant-user provisioner, attaches roles, and writes
/// the audit-log entry. The provisioner is idempotent so re-inviting
/// the same email reuses the existing placeholder row.
/// </summary>
internal sealed class AccessUserInviteHelper
{
    private readonly PlatformDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly ICorrelationContext _correlationContext;
    private readonly IPendingTenantUserProvisioner _pendingUserProvisioner;

    public AccessUserInviteHelper(
        PlatformDbContext dbContext,
        IClock clock,
        ICurrentUserProvider currentUserProvider,
        IAuditLogWriter auditLogWriter,
        ICorrelationContext correlationContext,
        IPendingTenantUserProvisioner pendingUserProvisioner)
    {
        _dbContext = dbContext;
        _clock = clock;
        _currentUserProvider = currentUserProvider;
        _auditLogWriter = auditLogWriter;
        _correlationContext = correlationContext;
        _pendingUserProvisioner = pendingUserProvisioner;
    }

    public async Task<InviteUserResponse> InviteUserAsync(
        Guid tenantId,
        InviteUserRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            throw new ArgumentException("Email is required.", nameof(request));

        var trimmedEmail = request.Email.Trim();
        if (!trimmedEmail.Contains('@') || trimmedEmail.IndexOf('@') == 0 || trimmedEmail.IndexOf('@') == trimmedEmail.Length - 1)
            throw new ArgumentException("Email must be a valid email address.", nameof(request));

        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("Cannot invite a user without a tenant context.");

        // Validate role IDs early so we don't create a placeholder if
        // the request references a role that doesn't belong to the
        // tenant. This prevents privilege-escalation attempts where
        // an admin in tenant A tries to attach a role from tenant B.
        var requestedRoleIds = request.RoleIds?.Where(id => id != Guid.Empty).Distinct().ToList()
            ?? new List<Guid>();
        if (requestedRoleIds.Count > 0)
        {
            var validRoleIds = await _dbContext.Roles
                .AsNoTracking()
                .Where(r => r.TenantId == tenantId && requestedRoleIds.Contains(r.Id))
                .Select(r => r.Id)
                .ToListAsync(cancellationToken);

            var unknown = requestedRoleIds.Except(validRoleIds).ToArray();
            if (unknown.Length > 0)
            {
                throw new ArgumentException(
                    $"One or more roles are not part of this tenant: {string.Join(", ", unknown)}",
                    nameof(request));
            }
        }

        // Create (or reuse) the pending placeholder. The provisioner
        // is idempotent — re-inviting the same email returns the
        // existing row, so we can safely (re-)apply roles below.
        var placeholder = await _pendingUserProvisioner.ProvisionPendingInviteAsync(
            tenantId,
            trimmedEmail,
            request.DisplayName,
            cancellationToken);

        // Refuse to attach invite roles to a user that has ALREADY
        // linked an external identity. That would be a sneaky way to
        // alter another user's role set without going through the
        // proper Users.Manage flow. Updating roles on a real user
        // must go through UpdateUserRolesAsync.
        var placeholderUser = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == placeholder.UserId, cancellationToken);
        if (placeholderUser != null
            && !BootstrapIdentityConstants.IsPendingPlaceholderIssuer(placeholderUser.ExternalIssuer))
        {
            throw new InvalidOperationException(
                $"User '{trimmedEmail}' is already linked in this tenant; use Users.Manage to update their roles.");
        }

        var assignedRoleIds = new List<Guid>();
        foreach (var roleId in requestedRoleIds)
        {
            var alreadyAssigned = await _dbContext.UserRoles
                .AnyAsync(ur => ur.UserId == placeholder.UserId && ur.RoleId == roleId, cancellationToken);
            if (alreadyAssigned)
            {
                assignedRoleIds.Add(roleId);
                continue;
            }

            _dbContext.UserRoles.Add(new UserRole
            {
                Id = Guid.NewGuid(),
                UserId = placeholder.UserId,
                RoleId = roleId,
                CreatedAt = _clock.UtcNow,
            });
            assignedRoleIds.Add(roleId);
        }

        if (assignedRoleIds.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var actorId = _currentUserProvider.GetCurrentUserId();
        await _auditLogWriter.LogAsync(
            AuditEventNames.UserInvited,
            "User",
            placeholder.UserId,
            tenantId,
            actorId,
            _correlationContext.CorrelationId,
            JsonSerializer.Serialize(new
            {
                placeholder.UserId,
                Email = AuditLogMasking.MaskEmail(trimmedEmail),
                placeholder.WasCreated,
                AssignedRoleIds = assignedRoleIds,
            }),
            cancellationToken);

        return new InviteUserResponse(
            placeholder.UserId,
            tenantId,
            trimmedEmail,
            request.DisplayName,
            assignedRoleIds);
    }
}
