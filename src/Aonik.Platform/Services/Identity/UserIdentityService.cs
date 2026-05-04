using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Observability;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Compliance;
using Aonik.Platform.Contracts.Services.Compliance;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Entities.Identity;

namespace Aonik.Platform.Services.Identity;

internal class UserIdentityService : IUserIdentityService
{
    private readonly PlatformDbContext _dbContext;
    private readonly ILogger<UserIdentityService> _logger;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly ICorrelationContext _correlationContext;

    public UserIdentityService(
        PlatformDbContext dbContext,
        ILogger<UserIdentityService> logger,
        IAuditLogWriter auditLogWriter,
        ICorrelationContext correlationContext)
    {
        _dbContext = dbContext;
        _logger = logger;
        _auditLogWriter = auditLogWriter;
        _correlationContext = correlationContext;
    }

    public async Task<User> ResolveOrCreateUserAsync(
        string externalIssuer,
        string externalSubject,
        string? externalTenantId,
        string? email,
        Guid aonikTenantId,
        CancellationToken ct = default)
    {
        var normalizedEmail = string.IsNullOrWhiteSpace(email) ? null : email.Trim();

        // Lookup existing user by external identity
        var existingUser = await _dbContext.Users
            .FirstOrDefaultAsync(u =>
                u.TenantId == aonikTenantId &&
                u.ExternalIssuer == externalIssuer &&
                u.ExternalSubject == externalSubject,
                ct);

        if (existingUser != null)
        {
            if (!string.IsNullOrEmpty(normalizedEmail) && existingUser.Email != normalizedEmail)
            {
                existingUser.Email = normalizedEmail;
                await _dbContext.SaveChangesAsync(ct);
                _logger.LogInformation("Updated email for user {UserId}", existingUser.Id);
            }

            await EnsureDefaultPersonalUserRoleAsync(existingUser.Id, aonikTenantId, ct);

            return existingUser;
        }

        // Tenant-scoped login: try to match a pre-provisioned
        // placeholder (owner OR invite) by email and link the real
        // external identity onto it. Both placeholder kinds live under
        // the same bootstrap issuer; the subject prefix distinguishes
        // them but the lookup ignores that — any pending placeholder
        // matching the email will accept the link.
        if (!string.IsNullOrWhiteSpace(normalizedEmail) && aonikTenantId != Guid.Empty)
        {
            var pendingPlaceholders = await _dbContext.Users
                .Where(u =>
                    u.TenantId == aonikTenantId &&
                    u.ExternalIssuer == BootstrapIdentityConstants.PendingOwnerIssuer &&
                    u.Email != null)
                .ToListAsync(ct);

            var placeholder = pendingPlaceholders.FirstOrDefault(user =>
                string.Equals(user.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase));

            if (placeholder != null)
            {
                var placeholderKind = placeholder.ExternalSubject.StartsWith("invite:", StringComparison.Ordinal)
                    ? "invite"
                    : "owner";

                placeholder.ExternalIssuer = externalIssuer;
                placeholder.ExternalSubject = externalSubject;
                placeholder.ExternalTenantId = externalTenantId;
                placeholder.Email = normalizedEmail;

                await _dbContext.SaveChangesAsync(ct);
                await EnsureDefaultPersonalUserRoleAsync(placeholder.Id, aonikTenantId, ct);

                _logger.LogInformation(
                    "Linked pending {PlaceholderKind} {UserId} to external identity (Issuer: {Issuer}, Subject: {Subject})",
                    placeholderKind,
                    placeholder.Id,
                    externalIssuer,
                    externalSubject);

                await _auditLogWriter.LogAsync(
                    AuditEventNames.UserIdentityLinked,
                    "User",
                    placeholder.Id,
                    aonikTenantId,
                    placeholder.Id,
                    _correlationContext.CorrelationId,
                    JsonSerializer.Serialize(new
                    {
                        placeholder.Id,
                        Email = AuditLogMasking.MaskEmail(placeholder.Email),
                        placeholder.ExternalIssuer,
                        placeholder.ExternalSubject,
                        placeholder.ExternalTenantId,
                        PlaceholderKind = placeholderKind,
                    }),
                    ct);

                return placeholder;
            }
        }

        // Host / system path (no tenant scope): retain the JIT create
        // behavior — there's no tenant boundary to protect, and the
        // caller is logging in to manage the platform itself, not a
        // specific tenant. Bootstrap relies on this path for the very
        // first owner before any tenant exists.
        if (aonikTenantId == Guid.Empty)
        {
            var platformUser = new User
            {
                Id = Guid.NewGuid(),
                TenantId = aonikTenantId,
                ExternalIssuer = externalIssuer,
                ExternalSubject = externalSubject,
                ExternalTenantId = externalTenantId,
                Email = normalizedEmail,
                Status = "Active"
            };

            _dbContext.Users.Add(platformUser);
            await _dbContext.SaveChangesAsync(ct);

            _logger.LogInformation("Created new platform user {UserId} (Issuer: {Issuer}, Subject: {Subject})",
                platformUser.Id, externalIssuer, externalSubject);

            await _auditLogWriter.LogAsync(
                AuditEventNames.UserProvisioned,
                "User",
                platformUser.Id,
                aonikTenantId,
                platformUser.Id,
                _correlationContext.CorrelationId,
                JsonSerializer.Serialize(new
                {
                    platformUser.Id,
                    Email = AuditLogMasking.MaskEmail(platformUser.Email),
                    platformUser.ExternalIssuer,
                    platformUser.ExternalSubject,
                    platformUser.ExternalTenantId
                }),
                ct);

            return platformUser;
        }

        // Tenant-scoped login but the identity has NO existing link
        // and NO matching pending placeholder. This is the security
        // gap the rewrite closes: previously we'd silently create an
        // active user in any tenant the caller selected at login,
        // letting any authenticated identity grant themselves access
        // by picking a tenant from the list. We now reject the login
        // with a clear error so the operator (or the user themselves)
        // can request an invitation through the proper channel.
        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Id == aonikTenantId, ct);

        var tenantNameLogContext = tenant?.Name ?? aonikTenantId.ToString();

        _logger.LogWarning(
            "Tenant access denied for identity (Issuer: {Issuer}, Subject: {Subject}) — no existing link or pending invitation in tenant {Tenant}",
            externalIssuer, externalSubject, tenantNameLogContext);

        await _auditLogWriter.LogAsync(
            AuditEventNames.UserAccessDenied,
            "User",
            Guid.Empty,
            aonikTenantId,
            actorId: null,
            _correlationContext.CorrelationId,
            JsonSerializer.Serialize(new
            {
                Reason = "no_link_or_invite",
                Email = AuditLogMasking.MaskEmail(normalizedEmail),
                ExternalIssuer = externalIssuer,
                ExternalSubject = externalSubject,
            }),
            ct);

        throw new TenantAccessDeniedException(
            "You do not have access to this tenant. Ask a tenant administrator to invite you.");
    }

    private async Task EnsureDefaultPersonalUserRoleAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken ct)
    {
        if (tenantId == Guid.Empty)
        {
            return;
        }

        var personalUserRole = await _dbContext.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                role => role.TenantId == tenantId && role.Name == "PersonalUser",
                ct);

        if (personalUserRole == null)
        {
            _logger.LogWarning(
                "PersonalUser role not found for tenant {TenantId}. Cannot assign default role to user {UserId}",
                tenantId, userId);
            return;
        }

        var hasPersonalUserRole = await _dbContext.UserRoles
            .AnyAsync(
                userRole => userRole.UserId == userId && userRole.RoleId == personalUserRole.Id,
                ct);

        if (hasPersonalUserRole)
        {
            return;
        }

        _dbContext.UserRoles.Add(new UserRole
        {
            UserId = userId,
            RoleId = personalUserRole.Id,
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Assigned PersonalUser role to user {UserId} in tenant {TenantId}",
            userId, tenantId);
    }

    public async Task<IReadOnlyCollection<string>> GetRoleNamesAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var roleNames = await _dbContext.UserRoles
            .Where(userRole => userRole.UserId == userId)
            .Select(userRole => userRole.Role.Name)
            .Distinct()
            .OrderBy(roleName => roleName)
            .ToListAsync(ct);

        return roleNames;
    }
}
