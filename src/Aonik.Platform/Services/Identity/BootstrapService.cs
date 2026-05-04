using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Observability;
using Aonik.Platform.Persistence;
using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Services.Compliance;
using Aonik.Platform.Contracts.Services.Compliance;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Entities.Identity;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Platform.Services.Identity;

internal class BootstrapService : IBootstrapService
{
    private static readonly SemaphoreSlim BootstrapGate = new(1, 1);

    private readonly PlatformDbContext _dbContext;
    private readonly IBootstrapTenantProvisioner _tenantProvisioner;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly IClock _clock;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly BootstrapOptions _options;
    private readonly ICorrelationContext _correlationContext;
    private readonly IPendingTenantUserProvisioner _pendingUserProvisioner;

    public BootstrapService(
        PlatformDbContext dbContext,
        IBootstrapTenantProvisioner tenantProvisioner,
        ITenantContext tenantContext,
        IAuditLogWriter auditLogWriter,
        IClock clock,
        ICurrentUserProvider currentUserProvider,
        ICurrentUserContext currentUserContext,
        IOptions<BootstrapOptions> options,
        ICorrelationContext correlationContext,
        IPendingTenantUserProvisioner pendingUserProvisioner)
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
        _pendingUserProvisioner = pendingUserProvisioner;
    }

    public async Task<BootstrapTenantResult> BootstrapAsync(
        BootstrapOwnerContext ownerContext,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ownerContext.Email))
            throw new InvalidOperationException("Owner email is required for bootstrap.");

        var normalizedOwnerEmail = ownerContext.Email.Trim();

        await BootstrapGate.WaitAsync(cancellationToken);

        try
        {
            var now = _clock.UtcNow;
            var tenantResult = await CreateTenantAsync(now, cancellationToken);
            var tenant = tenantResult.Tenant;

            _tenantContext.TenantId = tenant.Id;
            _tenantContext.ResolutionSource = "Bootstrap";

            await _tenantProvisioner.ProvisionTenantAsync(tenant.Id, cancellationToken);

            tenant.Status = TenantStatus.Active;
            tenant.UpdatedAt = _clock.UtcNow;
            tenant.UpdatedBy = _currentUserProvider.GetCurrentUserId();
            await _dbContext.SaveChangesAsync(cancellationToken);

            var userResult = await ResolveOrCreateUserAsync(tenant, ownerContext, cancellationToken);
            var platformAdminAssigned = await EnsurePlatformAdminRoleAsync(tenant, userResult.UserId, cancellationToken);
            var tenantAdminAssigned = await EnsureTenantAdminRoleAsync(tenant, userResult.UserId, cancellationToken);

            return new BootstrapTenantResult(
                tenant.Id,
                tenant.Name,
                tenantResult.TenantCreated,
                userResult.UserId,
                userResult.UserCreated,
                platformAdminAssigned,
                tenantAdminAssigned,
                normalizedOwnerEmail,
                true);
        }
        finally
        {
            BootstrapGate.Release();
        }
    }

    private async Task<(Tenant Tenant, bool TenantCreated)> CreateTenantAsync(
        DateTime now,
        CancellationToken cancellationToken)
    {
        var hasTenants = await _dbContext.Tenants.AnyAsync(cancellationToken);
        if (hasTenants)
        {
            throw new InvalidOperationException("Bootstrap has already completed because at least one tenant exists.");
        }

        var currentUserId = _currentUserProvider.GetCurrentUserId();
        var tenantName = string.IsNullOrWhiteSpace(_options.TenantName) ? "Aonik Dev Tenant" : _options.TenantName;
        var environmentName = string.IsNullOrWhiteSpace(_options.Environment) ? "Development" : _options.Environment;
        var currency = string.IsNullOrWhiteSpace(_options.DefaultCurrency) ? "USD" : _options.DefaultCurrency;
        var supportedCountries = _options.SupportedCountries is { Length: > 0 }
            ? _options.SupportedCountries
            : ["US"];

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = tenantName,
            Environment = environmentName,
            DefaultCurrency = currency.ToUpperInvariant(),
            SupportedCountriesJson = TenantCountryCodeSerializer.Serialize(supportedCountries),
            AllowedOriginCountriesJson = TenantCountryCodeSerializer.Serialize(supportedCountries),
            AllowedDestinationCountriesJson = TenantCountryCodeSerializer.Serialize(supportedCountries),
            Status = TenantStatus.Provisioning,
            CreatedAt = now,
            CreatedBy = currentUserId
        };

        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _tenantContext.TenantId = tenant.Id;
        _tenantContext.ResolutionSource = "Bootstrap";

        await _auditLogWriter.LogAsync(
            AuditEventNames.TenantBootstrapCreated,
            "Tenant",
            tenant.Id,
            tenant.Id,
            currentUserId,
            _correlationContext.CorrelationId,
            JsonSerializer.Serialize(new { tenant.Id, tenant.Name, tenant.Environment }),
            cancellationToken);

        return (tenant, true);
    }



    private async Task<(Guid UserId, bool UserCreated)> ResolveOrCreateUserAsync(
        Tenant tenant,
        BootstrapOwnerContext ownerContext,
        CancellationToken cancellationToken)
    {
        // Delegate to the shared provisioner so bootstrap and admin
        // tenant-creation use exactly the same User+Party+UserParty+
        // PersonProfile shape. We still seed the current user context
        // afterwards so audit chains downstream of bootstrap have an
        // actor to attribute writes to.
        var result = await _pendingUserProvisioner.ProvisionPendingOwnerAsync(
            tenant.Id,
            ownerContext.Email,
            ownerContext.DisplayName,
            cancellationToken);

        if (result.WasCreated)
        {
            _currentUserContext.UserId = result.UserId;
            _currentUserContext.TenantId ??= tenant.Id;
        }

        return (result.UserId, result.WasCreated);
    }

    private async Task<bool> EnsurePlatformAdminRoleAsync(
        Tenant tenant,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var platformAdminRole = await _dbContext.Roles
            .FirstOrDefaultAsync(r => r.TenantId == Guid.Empty && r.Name == "PlatformAdmin", cancellationToken);

        if (platformAdminRole == null)
        {
            platformAdminRole = new Role
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.Empty,
                Name = "PlatformAdmin"
            };

            _dbContext.Roles.Add(platformAdminRole);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return await EnsureRoleAssignedAsync(tenant, userId, platformAdminRole, cancellationToken);
    }

    private async Task<bool> EnsureTenantAdminRoleAsync(
        Tenant tenant,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var tenantAdminRole = await _dbContext.Roles
            .FirstOrDefaultAsync(r => r.TenantId == tenant.Id && r.Name == "TenantAdmin", cancellationToken);

        if (tenantAdminRole == null)
        {
            return false;
        }

        return await EnsureRoleAssignedAsync(tenant, userId, tenantAdminRole, cancellationToken);
    }

    private async Task<bool> EnsureRoleAssignedAsync(
        Tenant tenant,
        Guid userId,
        Role role,
        CancellationToken cancellationToken)
    {
        var existingUserRole = await _dbContext.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == role.Id, cancellationToken);

        if (existingUserRole != null)
        {
            return false;
        }

        var userRole = new UserRole
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RoleId = role.Id
        };

        _dbContext.UserRoles.Add(userRole);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.LogAsync(
            AuditEventNames.UserRoleAssigned,
            "UserRole",
            userRole.Id,
            tenant.Id,
            _currentUserProvider.GetCurrentUserId() ?? userId,
            _correlationContext.CorrelationId,
            JsonSerializer.Serialize(new { userId, RoleId = role.Id, role.Name }),
            cancellationToken);

        return true;
    }

}
