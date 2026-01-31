using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Observability;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.Identity;
using Aonik.Application.Services.Compliance;
using Aonik.Application.Services.Identity.Provisioning;
using Aonik.Application.Services.Pricing;
using Aonik.Domain.Identity.Entities;
using Aonik.Domain.ReferenceData.Entities;
using Aonik.SharedKernel.Abstractions;


namespace Aonik.Application.Services.Identity;

public class TenantService : ITenantService
{
    private readonly IAonikDbContext _dbContext;
    private readonly ITenantProvisioner _provisioner;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly IClock _clock;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ICorrelationContext _correlationContext;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissionService;
    private readonly ICurrencyMetadataProvider _currencyMetadataProvider;

    public TenantService(
        IAonikDbContext dbContext,
        ITenantProvisioner provisioner,
        IAuditLogWriter auditLogWriter,
        IClock clock,
        ICurrentUserProvider currentUserProvider,
        ICorrelationContext correlationContext,
        ITenantContext tenantContext,
        IPermissionService permissionService,
        ICurrencyMetadataProvider currencyMetadataProvider)
    {
        _dbContext = dbContext;
        _provisioner = provisioner;
        _auditLogWriter = auditLogWriter;
        _clock = clock;
        _currentUserProvider = currentUserProvider;
        _correlationContext = correlationContext;
        _tenantContext = tenantContext;
        _permissionService = permissionService;
        _currencyMetadataProvider = currencyMetadataProvider;
    }


    public async Task<TenantResponse> CreateTenantAsync(CreateTenantRequest request, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Tenants.Write", cancellationToken);
        var normalizedCurrency = NormalizeCurrencyCode(request.DefaultCurrency);
        var normalizedCountries = await ValidateAndNormalizeCountryCodesAsync(request.SupportedCountries, cancellationToken);
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

        var userId = _currentUserProvider.GetCurrentUserId();
        var now = _clock.UtcNow;

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Environment = request.Environment,
            DefaultCurrency = normalizedCurrency,
            SupportedCountriesJson = JsonSerializer.Serialize(normalizedCountries),
            Status = TenantStatus.Provisioning,
            CreatedAt = now,
            CreatedBy = userId
        };

        _tenantContext.TenantId = tenant.Id;
        _tenantContext.ResolutionSource = "AdminTenantAction";


        _dbContext.Tenants.Add(tenant);
        await UpsertTenantCountriesAsync(tenant.Id, normalizedCountries, cancellationToken);
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

        // Provision defaults
        await _provisioner.ProvisionTenantAsync(tenant.Id, cancellationToken);


        // Update status to Active
        tenant.Status = TenantStatus.Active;
        tenant.UpdatedAt = _clock.UtcNow;
        tenant.UpdatedBy = userId;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await MapToResponseAsync(tenant, cancellationToken);
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

        var userId = _currentUserProvider.GetCurrentUserId();

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

        if (request.SupportedCountries is { Length: > 0 })
        {
            var normalizedCountries = await ValidateAndNormalizeCountryCodesAsync(request.SupportedCountries, cancellationToken);
            tenant.SupportedCountriesJson = JsonSerializer.Serialize(normalizedCountries);

            await UpsertTenantCountriesAsync(tenant.Id, normalizedCountries, cancellationToken);
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
        tenant.UpdatedBy = _currentUserProvider.GetCurrentUserId();

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.LogAsync(
            AuditEventNames.TenantDeactivated,
            "Tenant",
            tenant.Id,
            tenant.Id,
            _currentUserProvider.GetCurrentUserId(),
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
        tenant.UpdatedBy = _currentUserProvider.GetCurrentUserId();

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.LogAsync(
            AuditEventNames.TenantActivated,
            "Tenant",
            tenant.Id,
            tenant.Id,
            _currentUserProvider.GetCurrentUserId(),
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
            supportedCountries = string.IsNullOrEmpty(tenant.SupportedCountriesJson)
                ? Array.Empty<string>()
                : JsonSerializer.Deserialize<string[]>(tenant.SupportedCountriesJson) ?? Array.Empty<string>();
        }

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

    private async Task<string[]> ValidateAndNormalizeCountryCodesAsync(string[]? countries, CancellationToken cancellationToken)
    {
        if (countries is not { Length: > 0 })
            throw new ArgumentException("At least one supported country is required", nameof(countries));

        var normalized = countries
            .Select(c => c?.Trim())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!.ToUpperInvariant())
            .ToArray();

        if (normalized.Length == 0)
            throw new ArgumentException("At least one supported country is required", nameof(countries));

        foreach (var country in normalized)
        {
            if (country.Length != 2)
                throw new ArgumentException($"Invalid country code: {country}. Must be a 2-letter ISO 3166-1 alpha-2 code", nameof(countries));
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
            throw new ArgumentException($"Unsupported country codes: {string.Join(", ", missing)}", nameof(countries));

        return distinct;
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
        var userId = _currentUserProvider.GetCurrentUserId();
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
        var userId = _currentUserProvider.GetCurrentUserId();
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
