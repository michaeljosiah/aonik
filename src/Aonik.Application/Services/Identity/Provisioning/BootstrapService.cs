using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Observability;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.Identity;
using Aonik.Application.Services.Compliance;
using Aonik.Domain.Identity.Entities;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Application.Services.Identity.Provisioning;

public class BootstrapService : IBootstrapService
{
    private readonly IAonikDbContext _dbContext;
    private readonly ITenantProvisioner _tenantProvisioner;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly IClock _clock;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly BootstrapOptions _options;
    private readonly ICorrelationContext _correlationContext;

    public BootstrapService(
        IAonikDbContext dbContext,
        ITenantProvisioner tenantProvisioner,
        ITenantContext tenantContext,
        IAuditLogWriter auditLogWriter,
        IClock clock,
        ICurrentUserProvider currentUserProvider,
        ICurrentUserContext currentUserContext,
        IOptions<BootstrapOptions> options,
        ICorrelationContext correlationContext)
    {
        _dbContext = dbContext;
        _tenantProvisioner = tenantProvisioner;
        _tenantContext = tenantContext;
        _auditLogWriter = auditLogWriter;
        _clock = clock;
        _currentUserProvider = currentUserProvider;
        _currentUserContext = currentUserContext;
        _options = options.Value;
        _correlationContext = correlationContext;
    }

    public async Task<BootstrapTenantResult> BootstrapAsync(
        BootstrapUserContext userContext,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userContext.ExternalIssuer))
            throw new InvalidOperationException("External issuer is required for bootstrap.");

        if (string.IsNullOrWhiteSpace(userContext.ExternalSubject))
            throw new InvalidOperationException("External subject is required for bootstrap.");

        var now = _clock.UtcNow;
        var tenantResult = await ResolveOrCreateTenantAsync(now, cancellationToken);
        var tenant = tenantResult.Tenant;

        _tenantContext.TenantId = tenant.TenantId;
        _tenantContext.ResolutionSource = "Bootstrap";

        var userResult = await ResolveOrCreateUserAsync(tenant, userContext, cancellationToken);
        var tenantAdminAssigned = await EnsureTenantAdminRoleAsync(tenant, userResult.UserId, cancellationToken);

        return new BootstrapTenantResult(
            tenant.TenantId,
            tenant.Name,
            tenantResult.TenantCreated,
            userResult.UserId,
            userResult.UserCreated,
            tenantAdminAssigned);
    }

    private async Task<(Tenant Tenant, bool TenantCreated)> ResolveOrCreateTenantAsync(
        DateTime now,
        CancellationToken cancellationToken)
    {
        var tenant = await _dbContext.Tenants
            .OrderBy(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (tenant != null)
        {
            return (tenant, false);
        }

        var currentUserId = _currentUserProvider.GetCurrentUserId();
        var tenantName = string.IsNullOrWhiteSpace(_options.TenantName) ? "Aonik Dev Tenant" : _options.TenantName;
        var environmentName = string.IsNullOrWhiteSpace(_options.Environment) ? "Development" : _options.Environment;
        var currency = string.IsNullOrWhiteSpace(_options.DefaultCurrency) ? "USD" : _options.DefaultCurrency;
        var supportedCountries = _options.SupportedCountries is { Length: > 0 }
            ? _options.SupportedCountries
            : ["US"];

        tenant = new Tenant
        {
            TenantId = Guid.NewGuid(),
            Name = tenantName,
            Environment = environmentName,
            DefaultCurrency = currency.ToUpperInvariant(),
            SupportedCountriesJson = JsonSerializer.Serialize(supportedCountries.Select(c => c.ToUpperInvariant())),
            Status = TenantStatus.Provisioning,
            CreatedAt = now,
            CreatedBy = currentUserId
        };

        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _tenantContext.TenantId = tenant.TenantId;
        _tenantContext.ResolutionSource = "Bootstrap";

        await _auditLogWriter.LogAsync(
            AuditEventNames.TenantBootstrapCreated,
            "Tenant",
            tenant.Id,
            tenant.TenantId,
            currentUserId,
            _correlationContext.CorrelationId,
            JsonSerializer.Serialize(new { tenant.TenantId, tenant.Name, tenant.Environment }),
            cancellationToken);

        await _tenantProvisioner.ProvisionTenantAsync(tenant.TenantId, cancellationToken);

        tenant.Status = TenantStatus.Active;
        tenant.UpdatedAt = _clock.UtcNow;
        tenant.UpdatedBy = currentUserId;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return (tenant, true);
    }

    private async Task<(Guid UserId, bool UserCreated)> ResolveOrCreateUserAsync(
        Tenant tenant,
        BootstrapUserContext userContext,
        CancellationToken cancellationToken)
    {
        var existingUserId = _currentUserProvider.GetCurrentUserId();
        User? user = null;
        if (existingUserId.HasValue)
        {
            user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Id == existingUserId.Value, cancellationToken);
        }

        user ??= await _dbContext.Users
            .FirstOrDefaultAsync(u =>
                u.TenantId == tenant.TenantId &&
                u.ExternalIssuer == userContext.ExternalIssuer &&
                u.ExternalSubject == userContext.ExternalSubject,
                cancellationToken);

        if (user != null)
        {
            return (user.Id, false);
        }

        var newUser = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.TenantId,
            ExternalIssuer = userContext.ExternalIssuer,
            ExternalSubject = userContext.ExternalSubject,
            ExternalTenantId = userContext.ExternalTenantId,
            Email = userContext.Email,
            Status = "Active"
        };

        _dbContext.Users.Add(newUser);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _currentUserContext.UserId = newUser.Id;
        _currentUserContext.TenantId ??= tenant.TenantId;

        await _auditLogWriter.LogAsync(
            AuditEventNames.UserProvisioned,
            "User",
            newUser.Id,
            tenant.TenantId,
            newUser.Id,
            _correlationContext.CorrelationId,
            JsonSerializer.Serialize(new { newUser.Id, Email = AuditLogMasking.MaskEmail(newUser.Email) }),
            cancellationToken);

        return (newUser.Id, true);
    }

    private async Task<bool> EnsureTenantAdminRoleAsync(
        Tenant tenant,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var tenantAdminRole = await _dbContext.Roles
            .FirstOrDefaultAsync(r => r.TenantId == tenant.TenantId && r.Name == "TenantAdmin", cancellationToken);

        if (tenantAdminRole == null)
        {
            tenantAdminRole = new Role
            {
                RoleId = Guid.NewGuid(),
                TenantId = tenant.TenantId,
                Name = "TenantAdmin"
            };

            _dbContext.Roles.Add(tenantAdminRole);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var existingUserRole = await _dbContext.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == tenantAdminRole.RoleId, cancellationToken);

        if (existingUserRole != null)
        {
            return false;
        }

        var userRole = new UserRole
        {
            UserId = userId,
            RoleId = tenantAdminRole.RoleId
        };

        _dbContext.UserRoles.Add(userRole);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.LogAsync(
            AuditEventNames.UserRoleAssigned,
            "UserRole",
            userRole.Id,
            tenant.TenantId,
            _currentUserProvider.GetCurrentUserId() ?? userId,
            _correlationContext.CorrelationId,
            JsonSerializer.Serialize(new { userId, tenantAdminRole.RoleId, tenantAdminRole.Name }),
            cancellationToken);

        return true;
    }
}
