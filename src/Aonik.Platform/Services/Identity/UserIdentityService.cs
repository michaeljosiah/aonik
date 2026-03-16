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
        // Lookup existing user by external identity
        var existingUser = await _dbContext.Users
            .FirstOrDefaultAsync(u =>
                u.TenantId == aonikTenantId &&
                u.ExternalIssuer == externalIssuer &&
                u.ExternalSubject == externalSubject,
                ct);

        if (existingUser != null)
        {
            if (!string.IsNullOrEmpty(email) && existingUser.Email != email)
            {
                existingUser.Email = email;
                await _dbContext.SaveChangesAsync(ct);
                _logger.LogInformation("Updated email for user {UserId}", existingUser.Id);
            }

            await EnsureDefaultPersonalUserRoleAsync(existingUser.Id, aonikTenantId, ct);

            return existingUser;
        }

        if (aonikTenantId == Guid.Empty)
        {
            var platformUser = new User
            {
                Id = Guid.NewGuid(),
                TenantId = aonikTenantId,
                ExternalIssuer = externalIssuer,
                ExternalSubject = externalSubject,
                ExternalTenantId = externalTenantId,
                Email = email,
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

        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Id == aonikTenantId, ct);


        if (tenant == null)
        {
            _logger.LogError("Attempted to create user in non-existent tenant {TenantId}", aonikTenantId);
            throw new InvalidOperationException($"Tenant {aonikTenantId} does not exist");
        }

        if (tenant.Status != "Active")
        {
            _logger.LogWarning("Attempted to create user in non-active tenant {TenantId} (Status: {Status})",
                aonikTenantId, tenant.Status);
            throw new InvalidOperationException($"Tenant {aonikTenantId} is not active (Status: {tenant.Status})");
        }

        // Create new user (JIT provisioning)
        var newUser = new User
        {
            Id = Guid.NewGuid(),
            TenantId = aonikTenantId,
            ExternalIssuer = externalIssuer,
            ExternalSubject = externalSubject,
            ExternalTenantId = externalTenantId,
            Email = email, // Nullable - only if present
            Status = "Active"
        };

        _dbContext.Users.Add(newUser);
        await _dbContext.SaveChangesAsync(ct);

        await EnsureDefaultPersonalUserRoleAsync(newUser.Id, aonikTenantId, ct);

        _logger.LogInformation("Created new user {UserId} via JIT provisioning (Issuer: {Issuer}, Subject: {Subject})",
            newUser.Id, externalIssuer, externalSubject);

        await _auditLogWriter.LogAsync(
            AuditEventNames.UserProvisioned,
            "User",
            newUser.Id,
            aonikTenantId,
            newUser.Id,
            _correlationContext.CorrelationId,
            JsonSerializer.Serialize(new
            {
                newUser.Id,
                Email = AuditLogMasking.MaskEmail(newUser.Email),
                newUser.ExternalIssuer,
                newUser.ExternalSubject,
                newUser.ExternalTenantId
            }),
            ct);

        return newUser;
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
