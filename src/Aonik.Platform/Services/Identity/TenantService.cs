using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Observability;
using Aonik.Platform.Persistence;
using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Services;
using Aonik.Platform.Services.Compliance;
using Aonik.Platform.Contracts.Services.Compliance;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Entities.ReferenceData;
using Aonik.SharedKernel.Abstractions;


namespace Aonik.Platform.Services.Identity;

internal class TenantService : AdminServiceBase, ITenantService
{
    private sealed record TenantCountrySettings(
        string[] SupportedCountries,
        string[] AllowedOriginCountries,
        string[] AllowedDestinationCountries);

    private readonly PlatformDbContext _dbContext;
    private readonly ITenantProvisioner _provisioner;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly IClock _clock;
    private readonly ICorrelationContext _correlationContext;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrencyMetadataProvider _currencyMetadataProvider;
    private readonly IPendingTenantUserProvisioner _pendingUserProvisioner;

    public TenantService(
        PlatformDbContext dbContext,
        ITenantProvisioner provisioner,
        IAuditLogWriter auditLogWriter,
        IClock clock,
        ICurrentUserProvider currentUserProvider,
        ICorrelationContext correlationContext,
        ITenantContext tenantContext,
        IPermissionService permissionService,
        ICurrencyMetadataProvider currencyMetadataProvider,
        IPendingTenantUserProvisioner pendingUserProvisioner)
        : base(currentUserProvider, permissionService)
    {
        _dbContext = dbContext;
        _provisioner = provisioner;
        _auditLogWriter = auditLogWriter;
        _clock = clock;
        _correlationContext = correlationContext;
        _tenantContext = tenantContext;
        _currencyMetadataProvider = currencyMetadataProvider;
        _pendingUserProvisioner = pendingUserProvisioner;
    }


    public async Task<TenantResponse> CreateTenantAsync(CreateTenantRequest request, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Tenants.Write", cancellationToken);
        // Validate the owner email before any DB work — a tenant
        // without a pre-provisioned owner is the security gap that
        // motivated this whole change. We refuse to create the tenant
        // at all rather than leave it open to "first random login wins".
        var normalizedOwnerEmail = ValidateOwnerEmail(request.OwnerEmail);
        var normalizedCurrency = NormalizeCurrencyCode(request.DefaultCurrency);
        var countrySettings = await ResolveTenantCountrySettingsForCreateAsync(request, cancellationToken);
        var normalizedCurrencies = await ValidateAndNormalizeCurrencyCodesAsync(
            request.SupportedCurrencies is { Length: > 0 } ? request.SupportedCurrencies : new[] { normalizedCurrency },
            cancellationToken);
        ValidateEnvironment(request.Environment);
        ValidateCurrency(normalizedCurrency);
        await ValidateCurrencyInCurrenciesTableAsync(normalizedCurrency, cancellationToken);

        var existingTenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Name == request.Name, cancellationToken);

        if (existingTenant != null)
            throw new InvalidOperationException($"Tenant with name '{request.Name}' already exists");

        var userId = CurrentUserProvider.GetCurrentUserId();
        var now = _clock.UtcNow;

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Environment = request.Environment,
            DefaultCurrency = normalizedCurrency,
            SupportedCountriesJson = TenantCountryCodeSerializer.Serialize(countrySettings.SupportedCountries),
            AllowedOriginCountriesJson = TenantCountryCodeSerializer.Serialize(countrySettings.AllowedOriginCountries),
            AllowedDestinationCountriesJson = TenantCountryCodeSerializer.Serialize(countrySettings.AllowedDestinationCountries),
            Status = TenantStatus.Provisioning,
            CreatedAt = now,
            CreatedBy = userId
        };

        _tenantContext.TenantId = tenant.Id;
        _tenantContext.ResolutionSource = "AdminTenantAction";


        _dbContext.Tenants.Add(tenant);
        await UpsertTenantCountriesAsync(tenant.Id, countrySettings.SupportedCountries, cancellationToken);
        await UpsertTenantCurrenciesAsync(tenant.Id, normalizedCurrencies, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.LogAsync(
            AuditEventNames.TenantCreated,
            "Tenant",
            tenant.Id,
            tenant.Id,
            userId,
            _correlationContext.CorrelationId,
            JsonSerializer.Serialize(new { tenant.Id, tenant.Name, tenant.Environment }),
            cancellationToken);

        // Provision defaults (roles, permissions, etc.) — must run
        // BEFORE we provision the owner because the owner needs the
        // TenantAdmin role to exist.
        await _provisioner.ProvisionTenantAsync(tenant.Id, cancellationToken);

        // Provision the initial pending owner. This creates a User +
        // Party + UserParty + PersonProfile placeholder identified by
        // OwnerEmail; the first IdP login that matches the email will
        // link onto this row instead of creating a new account.
        var pendingOwner = await _pendingUserProvisioner.ProvisionPendingOwnerAsync(
            tenant.Id,
            normalizedOwnerEmail,
            request.OwnerDisplayName,
            cancellationToken);

        // Assign TenantAdmin so the owner lands with full tenant
        // privileges on first login. We deliberately do NOT assign
        // PlatformAdmin here — that's reserved for the host bootstrap
        // path; ordinary admin-created tenants are scoped to their
        // own tenant.
        await EnsureTenantAdminRoleAsync(tenant.Id, pendingOwner.UserId, cancellationToken);

        // Update status to Active
        tenant.Status = TenantStatus.Active;
        tenant.UpdatedAt = _clock.UtcNow;
        tenant.UpdatedBy = userId;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await MapToResponseAsync(tenant, cancellationToken);
    }

    private static string ValidateOwnerEmail(string ownerEmail)
    {
        if (string.IsNullOrWhiteSpace(ownerEmail))
            throw new ArgumentException("Owner email is required to create a tenant.", nameof(CreateTenantRequest.OwnerEmail));

        var trimmed = ownerEmail.Trim();
        // Lightweight format check — full RFC 5322 validation lives at
        // the API layer; this guards against trivially-malformed input
        // making it into the placeholder row.
        if (!trimmed.Contains('@') || trimmed.IndexOf('@') == 0 || trimmed.IndexOf('@') == trimmed.Length - 1)
            throw new ArgumentException("Owner email must be a valid email address.", nameof(CreateTenantRequest.OwnerEmail));

        return trimmed;
    }

    private async Task EnsureTenantAdminRoleAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var tenantAdminRole = await _dbContext.Roles
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Name == "TenantAdmin", cancellationToken);

        if (tenantAdminRole == null)
        {
            // Should never happen if ProvisionTenantAsync ran, but we
            // refuse to silently leave the owner without TenantAdmin —
            // a tenant with no admin is exactly the failure mode this
            // whole feature exists to prevent.
            throw new InvalidOperationException(
                $"TenantAdmin role was not provisioned for tenant {tenantId}; cannot assign initial owner.");
        }

        var alreadyAssigned = await _dbContext.UserRoles
            .AnyAsync(ur => ur.UserId == userId && ur.RoleId == tenantAdminRole.Id, cancellationToken);
        if (alreadyAssigned) return;

        var userRole = new UserRole
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RoleId = tenantAdminRole.Id,
        };

        _dbContext.UserRoles.Add(userRole);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.LogAsync(
            AuditEventNames.UserRoleAssigned,
            "UserRole",
            userRole.Id,
            tenantId,
            CurrentUserProvider.GetCurrentUserId() ?? userId,
            _correlationContext.CorrelationId,
            JsonSerializer.Serialize(new { userId, RoleId = tenantAdminRole.Id, tenantAdminRole.Name }),
            cancellationToken);
    }

    public async Task<TenantResponse?> GetTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Tenants.Read", cancellationToken);
        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

        return tenant == null ? null : await MapToResponseAsync(tenant, cancellationToken);
    }

    public async Task<PagedResult<TenantResponse>> ListTenantsAsync(
        ListTenantsRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Tenants.Read", cancellationToken);
        var query = _dbContext.Tenants.AsQueryable();

        if (!string.IsNullOrEmpty(request.Environment))
            query = query.Where(t => t.Environment == request.Environment);

        if (!string.IsNullOrEmpty(request.Status))
            query = query.Where(t => t.Status == request.Status);

        if (!string.IsNullOrEmpty(request.NameFilter))
            query = query.Where(t => t.Name.Contains(request.NameFilter));

        var totalCount = await query.CountAsync(cancellationToken);

        var tenants = await query
            .OrderBy(t => t.Name)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var items = new List<TenantResponse>(tenants.Count);
        foreach (var tenant in tenants)
            items.Add(await MapToResponseAsync(tenant, cancellationToken));

        return new PagedResult<TenantResponse>(
            items,
            request.PageNumber,
            request.PageSize,
            totalCount);
    }

    public async Task<TenantResponse> UpdateTenantAsync(
        Guid tenantId,
        UpdateTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Tenants.Write", cancellationToken);
        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

        if (tenant == null)
            throw new InvalidOperationException($"Tenant {tenantId} not found");

        _tenantContext.TenantId = tenant.Id;
        _tenantContext.ResolutionSource = "AdminTenantAction";

        var userId = CurrentUserProvider.GetCurrentUserId();

        var currentSupportedCountries = await GetTenantSupportedCountryCodesAsync(tenant.Id, cancellationToken);
        if (currentSupportedCountries.Length == 0)
        {
            currentSupportedCountries = TenantCountryCodeSerializer.Deserialize(tenant.SupportedCountriesJson);
        }

        var currentAllowedOriginCountries = TenantCountryCodeSerializer.ResolveWithFallback(
            tenant.AllowedOriginCountriesJson,
            currentSupportedCountries);
        var currentAllowedDestinationCountries = TenantCountryCodeSerializer.ResolveWithFallback(
            tenant.AllowedDestinationCountriesJson,
            currentSupportedCountries);

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            var existingTenant = await _dbContext.Tenants
                .FirstOrDefaultAsync(t => t.Name == request.Name && t.Id != tenantId, cancellationToken);

            if (existingTenant != null)
                throw new InvalidOperationException($"Tenant name '{request.Name}' already exists");

            tenant.Name = request.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.DefaultCurrency))
        {
            var normalizedCurrency = NormalizeCurrencyCode(request.DefaultCurrency);
            ValidateCurrency(normalizedCurrency);
            await ValidateCurrencyInCurrenciesTableAsync(normalizedCurrency, cancellationToken);
            tenant.DefaultCurrency = normalizedCurrency;
        }

        if (request.SupportedCurrencies is { Length: > 0 })
        {
            var normalizedCurrencies = await ValidateAndNormalizeCurrencyCodesAsync(request.SupportedCurrencies, cancellationToken);
            await UpsertTenantCurrenciesAsync(tenant.Id, normalizedCurrencies, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(request.DefaultCurrency))
        {
            // Ensure supported currencies stays aligned when only defaultCurrency changes.
            await UpsertTenantCurrenciesAsync(tenant.Id, new[] { tenant.DefaultCurrency }, cancellationToken);
        }

        if (request.SupportedCountries != null ||
            request.AllowedOriginCountries != null ||
            request.AllowedDestinationCountries != null)
        {
            var countrySettings = await ResolveTenantCountrySettingsForUpdateAsync(
                currentSupportedCountries,
                currentAllowedOriginCountries,
                currentAllowedDestinationCountries,
                request,
                cancellationToken);

            tenant.SupportedCountriesJson = TenantCountryCodeSerializer.Serialize(countrySettings.SupportedCountries);
            tenant.AllowedOriginCountriesJson = TenantCountryCodeSerializer.Serialize(countrySettings.AllowedOriginCountries);
            tenant.AllowedDestinationCountriesJson = TenantCountryCodeSerializer.Serialize(countrySettings.AllowedDestinationCountries);

            await UpsertTenantCountriesAsync(tenant.Id, countrySettings.SupportedCountries, cancellationToken);
        }

        if (!string.IsNullOrEmpty(request.Environment))
        {
            ValidateEnvironment(request.Environment);
            tenant.Environment = request.Environment;
        }

        // Company Setup fields
        if (request.LogoUrl != null)
            tenant.LogoUrl = request.LogoUrl;
        if (request.Industry != null)
            tenant.Industry = request.Industry;
        if (request.CompanySize != null)
            tenant.CompanySize = request.CompanySize;
        if (request.Website != null)
            tenant.Website = request.Website;

        // Contact fields
        if (request.ContactEmail != null)
            tenant.ContactEmail = request.ContactEmail;
        if (request.ContactMobile != null)
            tenant.ContactMobile = request.ContactMobile;

        // Address fields
        if (request.AddressLine1 != null)
            tenant.AddressLine1 = request.AddressLine1;
        if (request.AddressLine2 != null)
            tenant.AddressLine2 = request.AddressLine2;
        if (request.City != null)
            tenant.City = request.City;
        if (request.StateProvince != null)
            tenant.StateProvince = request.StateProvince;
        if (request.PostalCode != null)
            tenant.PostalCode = request.PostalCode;
        if (request.Country != null)
            tenant.Country = request.Country;

        // Setup tracking
        if (request.IsSetupComplete.HasValue)
            tenant.IsSetupComplete = request.IsSetupComplete.Value;
        if (request.SetupStep.HasValue)
            tenant.SetupStep = request.SetupStep.Value;

        tenant.UpdatedAt = _clock.UtcNow;
        tenant.UpdatedBy = userId;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.LogAsync(
            AuditEventNames.TenantUpdated,
            "Tenant",
            tenant.Id,
            tenant.Id,
            userId,
            _correlationContext.CorrelationId,
            JsonSerializer.Serialize(request),
            cancellationToken);

        return await MapToResponseAsync(tenant, cancellationToken);
    }

    public async Task DeactivateTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Tenants.Write", cancellationToken);
        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

        if (tenant == null)
            throw new InvalidOperationException($"Tenant {tenantId} not found");

        if (tenant.Status == TenantStatus.Deactivated)
            return;

        _tenantContext.TenantId = tenant.Id;
        _tenantContext.ResolutionSource = "AdminTenantAction";

        tenant.Status = TenantStatus.Deactivated;
        tenant.UpdatedAt = _clock.UtcNow;
        tenant.UpdatedBy = CurrentUserProvider.GetCurrentUserId();

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.LogAsync(
            AuditEventNames.TenantDeactivated,
            "Tenant",
            tenant.Id,
            tenant.Id,
            CurrentUserProvider.GetCurrentUserId(),
            _correlationContext.CorrelationId,
            JsonSerializer.Serialize(new { tenant.Id, tenant.Name }),
            cancellationToken);
    }

    public async Task ActivateTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Tenants.Write", cancellationToken);
        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

        if (tenant == null)
            throw new InvalidOperationException($"Tenant {tenantId} not found");

        if (tenant.Status == TenantStatus.Active)
            return;

        _tenantContext.TenantId = tenant.Id;
        _tenantContext.ResolutionSource = "AdminTenantAction";

        tenant.Status = TenantStatus.Active;
        tenant.UpdatedAt = _clock.UtcNow;
        tenant.UpdatedBy = CurrentUserProvider.GetCurrentUserId();

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.LogAsync(
            AuditEventNames.TenantActivated,
            "Tenant",
            tenant.Id,
            tenant.Id,
            CurrentUserProvider.GetCurrentUserId(),
            _correlationContext.CorrelationId,
            JsonSerializer.Serialize(new { tenant.Id, tenant.Name }),
            cancellationToken);
    }

    public async Task<TenantListForLoginResponse> ListTenantsForLoginAsync(CancellationToken cancellationToken = default)
    {
        // Public endpoint - no authentication/permission check required
        // Only return active tenants with minimal info
        var tenants = await _dbContext.Tenants
            .AsNoTracking()
            .Where(t => t.Status == TenantStatus.Active)
            .OrderBy(t => t.Name)
            .Select(t => new TenantListItemForLogin(
                t.Id,
                t.Name,
                t.Subdomain,
                t.Environment))
            .ToListAsync(cancellationToken);

        return new TenantListForLoginResponse(tenants);
    }


    private async Task<TenantResponse> MapToResponseAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        var supportedCountries = await GetTenantSupportedCountryCodesAsync(tenant.Id, cancellationToken);
        var supportedCurrencies = await GetTenantSupportedCurrencyCodesAsync(tenant.Id, cancellationToken);

        if (supportedCountries.Length == 0)
        {
            supportedCountries = TenantCountryCodeSerializer.Deserialize(tenant.SupportedCountriesJson);
        }

        var allowedOriginCountries = TenantCountryCodeSerializer.ResolveWithFallback(
            tenant.AllowedOriginCountriesJson,
            supportedCountries);
        var allowedDestinationCountries = TenantCountryCodeSerializer.ResolveWithFallback(
            tenant.AllowedDestinationCountriesJson,
            supportedCountries);

        if (supportedCurrencies.Length == 0)
        {
            supportedCurrencies = new[] { tenant.DefaultCurrency };
        }

        return new TenantResponse(
            tenant.Id,
            tenant.Id,
            tenant.Name,
            tenant.Environment,
            tenant.DefaultCurrency,
            supportedCountries,
            allowedOriginCountries,
            allowedDestinationCountries,
            supportedCurrencies,
            tenant.Status,
            tenant.CreatedAt,
            tenant.CreatedBy,
            tenant.UpdatedAt,
            tenant.UpdatedBy,
            // Company Setup fields
            tenant.LogoUrl,
            tenant.Industry,
            tenant.CompanySize,
            tenant.Website,
            // Contact fields
            tenant.ContactEmail,
            tenant.ContactMobile,
            // Address fields
            tenant.AddressLine1,
            tenant.AddressLine2,
            tenant.City,
            tenant.StateProvince,
            tenant.PostalCode,
            tenant.Country,
            // Setup tracking
            tenant.IsSetupComplete,
            tenant.SetupStep);
    }

    private static string NormalizeCurrencyCode(string currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency is required", nameof(currency));

        return currency.Trim().ToUpperInvariant();
    }

    private static void ValidateEnvironment(string environment)
    {
        var validEnvironments = new[] { "Dev", "Test", "Staging", "Prod" };
        if (!validEnvironments.Contains(environment, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"Invalid environment. Must be one of: {string.Join(", ", validEnvironments)}", nameof(environment));
    }

    private void ValidateCurrency(string currency)
    {
        if (currency.Length != 3)
            throw new ArgumentException("Currency must be a 3-letter ISO 4217 code", nameof(currency));

        if (!_currencyMetadataProvider.TryGetCurrency(currency, out _))
            throw new ArgumentException($"Unsupported currency: {currency}", nameof(currency));
    }

    private async Task ValidateCurrencyInCurrenciesTableAsync(string currency, CancellationToken cancellationToken)
    {
        var exists = await _dbContext.Currencies
            .AsNoTracking()
            .AnyAsync(
                x => x.TenantId == null && x.IsActive && x.Code == currency,
                cancellationToken);

        if (!exists)
            throw new ArgumentException($"Currency is not configured in currencies: {currency}", nameof(currency));
    }

    private async Task<string[]> ValidateAndNormalizeCurrencyCodesAsync(string[]? currencies, CancellationToken cancellationToken)
    {
        if (currencies is not { Length: > 0 })
            throw new ArgumentException("At least one supported currency is required", nameof(currencies));

        var normalized = currencies
            .Select(c => c?.Trim())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!.ToUpperInvariant())
            .ToArray();

        if (normalized.Length == 0)
            throw new ArgumentException("At least one supported currency is required", nameof(currencies));

        foreach (var currency in normalized)
        {
            ValidateCurrency(currency);
        }

        var distinct = normalized.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        var found = await _dbContext.Currencies
            .AsNoTracking()
            .Where(x => x.TenantId == null && x.IsActive && distinct.Contains(x.Code))
            .Select(x => x.Code)
            .ToListAsync(cancellationToken);

        var missing = distinct
            .Except(found, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (missing.Length > 0)
            throw new ArgumentException($"Currency is not configured in currencies: {string.Join(", ", missing)}", nameof(currencies));

        return distinct;
    }

    private async Task<string[]> ValidateAndNormalizeCountryCodesAsync(
        string[]? countries,
        string parameterName,
        bool requireAtLeastOne,
        CancellationToken cancellationToken)
    {
        if (countries == null || countries.Length == 0)
        {
            if (requireAtLeastOne)
            {
                throw new ArgumentException("At least one supported country is required", parameterName);
            }

            return Array.Empty<string>();
        }

        var normalized = countries
            .Select(c => c?.Trim())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!.ToUpperInvariant())
            .ToArray();

        if (normalized.Length == 0)
        {
            if (requireAtLeastOne)
            {
                throw new ArgumentException("At least one supported country is required", parameterName);
            }

            return Array.Empty<string>();
        }

        foreach (var country in normalized)
        {
            if (country.Length != 2)
                throw new ArgumentException($"Invalid country code: {country}. Must be a 2-letter ISO 3166-1 alpha-2 code", parameterName);
        }

        var distinct = normalized.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        var found = await _dbContext.Countries
            .AsNoTracking()
            .Where(x => x.TenantId == null && x.IsActive && distinct.Contains(x.IsoAlpha2))
            .Select(x => x.IsoAlpha2)
            .ToListAsync(cancellationToken);

        var missing = distinct
            .Except(found, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (missing.Length > 0)
            throw new ArgumentException($"Unsupported country codes: {string.Join(", ", missing)}", parameterName);

        return distinct;
    }

    private async Task<TenantCountrySettings> ResolveTenantCountrySettingsForCreateAsync(
        CreateTenantRequest request,
        CancellationToken cancellationToken)
    {
        var supportedCountries = await ValidateAndNormalizeCountryCodesAsync(
            request.SupportedCountries,
            nameof(CreateTenantRequest.SupportedCountries),
            requireAtLeastOne: true,
            cancellationToken);

        var allowedOriginCountries = request.AllowedOriginCountries == null
            ? supportedCountries
            : await ValidateAndNormalizeCountryCodesAsync(
                request.AllowedOriginCountries,
                nameof(CreateTenantRequest.AllowedOriginCountries),
                requireAtLeastOne: false,
                cancellationToken);

        var allowedDestinationCountries = request.AllowedDestinationCountries == null
            ? supportedCountries
            : await ValidateAndNormalizeCountryCodesAsync(
                request.AllowedDestinationCountries,
                nameof(CreateTenantRequest.AllowedDestinationCountries),
                requireAtLeastOne: false,
                cancellationToken);

        EnsureCountrySubset(allowedOriginCountries, supportedCountries, nameof(CreateTenantRequest.AllowedOriginCountries));
        EnsureCountrySubset(allowedDestinationCountries, supportedCountries, nameof(CreateTenantRequest.AllowedDestinationCountries));

        return new TenantCountrySettings(
            supportedCountries,
            allowedOriginCountries,
            allowedDestinationCountries);
    }

    private async Task<TenantCountrySettings> ResolveTenantCountrySettingsForUpdateAsync(
        string[] currentSupportedCountries,
        string[] currentAllowedOriginCountries,
        string[] currentAllowedDestinationCountries,
        UpdateTenantRequest request,
        CancellationToken cancellationToken)
    {
        var supportedCountries = request.SupportedCountries == null
            ? currentSupportedCountries
            : await ValidateAndNormalizeCountryCodesAsync(
                request.SupportedCountries,
                nameof(UpdateTenantRequest.SupportedCountries),
                requireAtLeastOne: true,
                cancellationToken);

        var allowedOriginCountries = request.AllowedOriginCountries == null
            ? currentAllowedOriginCountries
            : await ValidateAndNormalizeCountryCodesAsync(
                request.AllowedOriginCountries,
                nameof(UpdateTenantRequest.AllowedOriginCountries),
                requireAtLeastOne: false,
                cancellationToken);

        var allowedDestinationCountries = request.AllowedDestinationCountries == null
            ? currentAllowedDestinationCountries
            : await ValidateAndNormalizeCountryCodesAsync(
                request.AllowedDestinationCountries,
                nameof(UpdateTenantRequest.AllowedDestinationCountries),
                requireAtLeastOne: false,
                cancellationToken);

        if (request.SupportedCountries != null && request.AllowedOriginCountries == null)
        {
            allowedOriginCountries = IntersectCountryCodes(allowedOriginCountries, supportedCountries);
        }

        if (request.SupportedCountries != null && request.AllowedDestinationCountries == null)
        {
            allowedDestinationCountries = IntersectCountryCodes(allowedDestinationCountries, supportedCountries);
        }

        EnsureCountrySubset(allowedOriginCountries, supportedCountries, nameof(UpdateTenantRequest.AllowedOriginCountries));
        EnsureCountrySubset(allowedDestinationCountries, supportedCountries, nameof(UpdateTenantRequest.AllowedDestinationCountries));

        return new TenantCountrySettings(
            supportedCountries,
            allowedOriginCountries,
            allowedDestinationCountries);
    }

    private static void EnsureCountrySubset(string[] subsetCountries, string[] supportedCountries, string parameterName)
    {
        var supported = new HashSet<string>(supportedCountries, StringComparer.OrdinalIgnoreCase);
        var invalid = subsetCountries
            .Where(code => !supported.Contains(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (invalid.Length > 0)
        {
            throw new ArgumentException(
                $"Countries must be a subset of supported countries: {string.Join(", ", invalid)}",
                parameterName);
        }
    }

    private static string[] IntersectCountryCodes(string[] sourceCountries, string[] supportedCountries)
    {
        var supported = new HashSet<string>(supportedCountries, StringComparer.OrdinalIgnoreCase);
        return sourceCountries
            .Where(code => supported.Contains(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task UpsertTenantCountriesAsync(Guid tenantId, string[] countryCodes, CancellationToken cancellationToken)
    {
        var countryIds = await _dbContext.Countries
            .AsNoTracking()
            .Where(x => x.TenantId == null && x.IsActive && countryCodes.Contains(x.IsoAlpha2))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (countryIds.Count == 0)
            return;

        var existing = await _dbContext.TenantCountries
            .Where(x => x.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
            _dbContext.TenantCountries.RemoveRange(existing);

        var now = _clock.UtcNow;
        var userId = CurrentUserProvider.GetCurrentUserId();
        var items = countryIds.Select(countryId => new TenantCountry
        {
            TenantId = tenantId,
            CountryId = countryId,
            CreatedAt = now,
            CreatedBy = userId
        });

        await _dbContext.TenantCountries.AddRangeAsync(items, cancellationToken);
    }

    private async Task UpsertTenantCurrenciesAsync(Guid tenantId, string[] currencyCodes, CancellationToken cancellationToken)
    {
        var currencyIds = await _dbContext.Currencies
            .AsNoTracking()
            .Where(x => x.TenantId == null && x.IsActive && currencyCodes.Contains(x.Code))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (currencyIds.Count == 0)
            return;

        var existing = await _dbContext.TenantCurrencies
            .Where(x => x.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
            _dbContext.TenantCurrencies.RemoveRange(existing);

        var now = _clock.UtcNow;
        var userId = CurrentUserProvider.GetCurrentUserId();
        var items = currencyIds.Select(currencyId => new TenantCurrency
        {
            TenantId = tenantId,
            CurrencyId = currencyId,
            CreatedAt = now,
            CreatedBy = userId
        });

        await _dbContext.TenantCurrencies.AddRangeAsync(items, cancellationToken);
    }

    private async Task<string[]> GetTenantSupportedCountryCodesAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var codes = await _dbContext.TenantCountries
            .AsNoTracking()
            .Where(link => link.TenantId == tenantId)
            .Join(_dbContext.Countries.AsNoTracking(),
                link => link.CountryId,
                country => country.Id,
                (link, country) => country.IsoAlpha2)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        return codes.ToArray();
    }

    private async Task<string[]> GetTenantSupportedCurrencyCodesAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var codes = await _dbContext.TenantCurrencies
            .AsNoTracking()
            .Where(link => link.TenantId == tenantId)
            .Join(_dbContext.Currencies.AsNoTracking(),
                link => link.CurrencyId,
                currency => currency.Id,
                (link, currency) => currency.Code)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        return codes.ToArray();
    }

}
