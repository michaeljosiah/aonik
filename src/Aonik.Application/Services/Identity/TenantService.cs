using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Application.Abstractions.Observability;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.Identity;
using Aonik.Application.Services.Compliance;
using Aonik.Application.Services.Identity.Provisioning;
using Aonik.Domain.Identity.Entities;
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

    public TenantService(
        IAonikDbContext dbContext,
        ITenantProvisioner provisioner,
        IAuditLogWriter auditLogWriter,
        IClock clock,
        ICurrentUserProvider currentUserProvider,
        ICorrelationContext correlationContext)
    {
        _dbContext = dbContext;
        _provisioner = provisioner;
        _auditLogWriter = auditLogWriter;
        _clock = clock;
        _currentUserProvider = currentUserProvider;
        _correlationContext = correlationContext;
    }

    public async Task<TenantResponse> CreateTenantAsync(CreateTenantRequest request, CancellationToken cancellationToken = default)
    {
        ValidateCreateRequest(request);

        var existingTenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Name == request.Name, cancellationToken);

        if (existingTenant != null)
            throw new InvalidOperationException($"Tenant with name '{request.Name}' already exists");

        var userId = _currentUserProvider.GetCurrentUserId();
        var now = _clock.UtcNow;

        var tenant = new Tenant
        {
            TenantId = Guid.NewGuid(),
            Name = request.Name,
            Environment = request.Environment,
            DefaultCurrency = request.DefaultCurrency.ToUpperInvariant(),
            SupportedCountriesJson = JsonSerializer.Serialize(request.SupportedCountries.Select(c => c.ToUpperInvariant())),
            Status = TenantStatus.Provisioning,
            CreatedAt = now,
            CreatedBy = userId
        };

        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.LogAsync(
            AuditEventNames.TenantCreated,
            "Tenant",
            tenant.Id,
            tenant.TenantId,
            userId,
            _correlationContext.CorrelationId,
            JsonSerializer.Serialize(new { tenant.TenantId, tenant.Name, tenant.Environment }),
            cancellationToken);

        // Provision defaults
        await _provisioner.ProvisionTenantAsync(tenant.TenantId, cancellationToken);

        // Update status to Active
        tenant.Status = TenantStatus.Active;
        tenant.UpdatedAt = _clock.UtcNow;
        tenant.UpdatedBy = userId;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToResponse(tenant);
    }

    public async Task<TenantResponse?> GetTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.TenantId == tenantId, cancellationToken);

        return tenant == null ? null : MapToResponse(tenant);
    }

    public async Task<PagedResult<TenantResponse>> ListTenantsAsync(ListTenantsRequest request, CancellationToken cancellationToken = default)
    {
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

        var items = tenants.Select(MapToResponse).ToList();

        return new PagedResult<TenantResponse>(items, totalCount, request.PageNumber, request.PageSize);
    }

    public async Task<TenantResponse> UpdateTenantAsync(Guid tenantId, UpdateTenantRequest request, CancellationToken cancellationToken = default)
    {
        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.TenantId == tenantId, cancellationToken);

        if (tenant == null)
            throw new InvalidOperationException($"Tenant {tenantId} not found");

        var userId = _currentUserProvider.GetCurrentUserId();

        if (!string.IsNullOrEmpty(request.Name) && request.Name != tenant.Name)
        {
            var existingTenant = await _dbContext.Tenants
                .FirstOrDefaultAsync(t => t.Name == request.Name && t.TenantId != tenantId, cancellationToken);

            if (existingTenant != null)
                throw new InvalidOperationException($"Tenant with name '{request.Name}' already exists");

            tenant.Name = request.Name;
        }

        if (!string.IsNullOrEmpty(request.DefaultCurrency))
        {
            ValidateCurrency(request.DefaultCurrency);
            tenant.DefaultCurrency = request.DefaultCurrency.ToUpperInvariant();
        }

        if (request.SupportedCountries != null && request.SupportedCountries.Length > 0)
        {
            ValidateCountryCodes(request.SupportedCountries);
            tenant.SupportedCountriesJson = JsonSerializer.Serialize(request.SupportedCountries.Select(c => c.ToUpperInvariant()));
        }

        if (!string.IsNullOrEmpty(request.Environment))
        {
            ValidateEnvironment(request.Environment);
            tenant.Environment = request.Environment;
        }

        tenant.UpdatedAt = _clock.UtcNow;
        tenant.UpdatedBy = userId;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.LogAsync(
            AuditEventNames.TenantUpdated,
            "Tenant",
            tenant.Id,
            tenant.TenantId,
            userId,
            _correlationContext.CorrelationId,
            JsonSerializer.Serialize(request),
            cancellationToken);

        return MapToResponse(tenant);
    }

    public async Task DeactivateTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.TenantId == tenantId, cancellationToken);

        if (tenant == null)
            throw new InvalidOperationException($"Tenant {tenantId} not found");

        if (tenant.Status == TenantStatus.Deactivated)
            return;

        tenant.Status = TenantStatus.Deactivated;
        tenant.UpdatedAt = _clock.UtcNow;
        tenant.UpdatedBy = _currentUserProvider.GetCurrentUserId();

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.LogAsync(
            AuditEventNames.TenantDeactivated,
            "Tenant",
            tenant.Id,
            tenant.TenantId,
            _currentUserProvider.GetCurrentUserId(),
            _correlationContext.CorrelationId,
            JsonSerializer.Serialize(new { tenant.TenantId, tenant.Name }),
            cancellationToken);
    }

    public async Task ActivateTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.TenantId == tenantId, cancellationToken);

        if (tenant == null)
            throw new InvalidOperationException($"Tenant {tenantId} not found");

        if (tenant.Status == TenantStatus.Active)
            return;

        tenant.Status = TenantStatus.Active;
        tenant.UpdatedAt = _clock.UtcNow;
        tenant.UpdatedBy = _currentUserProvider.GetCurrentUserId();

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.LogAsync(
            AuditEventNames.TenantActivated,
            "Tenant",
            tenant.Id,
            tenant.TenantId,
            _currentUserProvider.GetCurrentUserId(),
            _correlationContext.CorrelationId,
            JsonSerializer.Serialize(new { tenant.TenantId, tenant.Name }),
            cancellationToken);
    }

    private static TenantResponse MapToResponse(Tenant tenant)
    {
        var supportedCountries = string.IsNullOrEmpty(tenant.SupportedCountriesJson)
            ? Array.Empty<string>()
            : JsonSerializer.Deserialize<string[]>(tenant.SupportedCountriesJson) ?? Array.Empty<string>();

        return new TenantResponse(
            tenant.Id,
            tenant.TenantId,
            tenant.Name,
            tenant.Environment,
            tenant.DefaultCurrency,
            supportedCountries,
            tenant.Status,
            tenant.CreatedAt,
            tenant.CreatedBy,
            tenant.UpdatedAt,
            tenant.UpdatedBy
        );
    }

    private static void ValidateCreateRequest(CreateTenantRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Tenant name is required", nameof(request.Name));

        if (string.IsNullOrWhiteSpace(request.Environment))
            throw new ArgumentException("Environment is required", nameof(request.Environment));

        if (string.IsNullOrWhiteSpace(request.DefaultCurrency))
            throw new ArgumentException("Default currency is required", nameof(request.DefaultCurrency));

        ValidateEnvironment(request.Environment);
        ValidateCurrency(request.DefaultCurrency);
        ValidateCountryCodes(request.SupportedCountries);
    }

    private static void ValidateEnvironment(string environment)
    {
        var validEnvironments = new[] { "Dev", "Test", "Staging", "Prod" };
        if (!validEnvironments.Contains(environment, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"Invalid environment. Must be one of: {string.Join(", ", validEnvironments)}", nameof(environment));
    }

    private static void ValidateCurrency(string currency)
    {
        if (currency.Length != 3)
            throw new ArgumentException("Currency must be a 3-letter ISO 4217 code", nameof(currency));

        var validCurrencies = new[] { "USD", "EUR", "GBP", "JPY", "CAD", "AUD", "CHF", "CNY", "SEK", "NZD" };
        if (!validCurrencies.Contains(currency.ToUpperInvariant()))
            throw new ArgumentException($"Unsupported currency: {currency}. Supported currencies: {string.Join(", ", validCurrencies)}", nameof(currency));
    }

    private static void ValidateCountryCodes(string[] countries)
    {
        if (countries == null || countries.Length == 0)
            throw new ArgumentException("At least one supported country is required", nameof(countries));

        foreach (var country in countries)
        {
            if (string.IsNullOrWhiteSpace(country) || country.Length != 2)
                throw new ArgumentException($"Invalid country code: {country}. Must be a 2-letter ISO 3166-1 alpha-2 code", nameof(countries));
        }
    }
}
