using Microsoft.EntityFrameworkCore;
using System.Text.Json;

using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.SharedKernel.Persistence;
using Aonik.Platform.Persistence;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Entities.Settings;
using Aonik.Platform.Services.Identity;

namespace Aonik.Platform.Services.Seeding.Phases;

/// <summary>
/// Configures the tenant home base (UK/GBP) and seeds tenant-country /
/// tenant-currency coverage for the Africa corridors.
/// Called at Phase 10 (UK home base) and Phase 11 (coverage) of the
/// cross-border demo seed pipeline.
/// </summary>
internal sealed class CrossBorderTenantSeedPhase
{
    private readonly PlatformDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ICurrentUserProvider _currentUserProvider;

    public CrossBorderTenantSeedPhase(
        PlatformDbContext dbContext,
        IClock clock,
        ICurrentUserProvider currentUserProvider)
    {
        _dbContext = dbContext;
        _clock = clock;
        _currentUserProvider = currentUserProvider;
    }

    public async Task EnsureUkHomeBaseAsync(
        Guid tenantId,
        List<string> operations,
        CancellationToken cancellationToken)
    {
        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(item => item.Id == tenantId, cancellationToken);

        if (tenant == null)
        {
            throw new InvalidOperationException($"Tenant {tenantId} not found.");
        }

        var now = _clock.UtcNow;
        var userId = _currentUserProvider.GetCurrentUserId();

        tenant.Country = "GB";
        tenant.DefaultCurrency = "GBP";
        tenant.City ??= "London";
        tenant.StateProvince ??= "England";
        tenant.AddressLine1 ??= "25 Finsbury Circus";

        var supportedCountries = ParseSupportedCountries(tenant.SupportedCountriesJson);
        supportedCountries.Add("GB");
        supportedCountries.Add("NG");
        supportedCountries.Add("GH");
        supportedCountries.Add("KE");
        supportedCountries.Add("ZA");

        tenant.SupportedCountriesJson = TenantCountryCodeSerializer.Serialize(supportedCountries);
        tenant.AllowedOriginCountriesJson = TenantCountryCodeSerializer.Serialize(supportedCountries);
        tenant.AllowedDestinationCountriesJson = TenantCountryCodeSerializer.Serialize(supportedCountries);
        tenant.UpdatedAt = now;
        tenant.UpdatedBy = userId;

        await _dbContext.SaveChangesAsync(cancellationToken);
        operations.Add("Configured tenant home base to UK (GBP) for Africa billing and remittance");
    }

    public async Task<(IReadOnlyList<Guid> CountryIds, IReadOnlyList<Guid> CurrencyIds)> SeedCrossBorderTenantCoverageAsync(
        Guid tenantId,
        List<string> operations,
        CancellationToken cancellationToken)
    {
        var countryCodes = new[] { "GB", "NG", "GH", "KE", "ZA" };
        var currencyCodes = new[] { "GBP", "NGN", "GHS", "KES", "ZAR", "USD" };

        var countries = await _dbContext.Countries
            .Where(country => countryCodes.Contains(country.IsoAlpha2))
            .Where(country => country.IsActive)
            .ToListAsync(cancellationToken);

        var currencies = await _dbContext.Currencies
            .Where(currency => currencyCodes.Contains(currency.Code))
            .Where(currency => currency.IsActive)
            .ToListAsync(cancellationToken);

        var missingCountries = countryCodes
            .Where(code => countries.All(country => !string.Equals(country.IsoAlpha2, code, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (missingCountries.Count > 0)
        {
            throw new InvalidOperationException($"Missing reference countries: {string.Join(", ", missingCountries)}.");
        }

        var missingCurrencies = currencyCodes
            .Where(code => currencies.All(currency => !string.Equals(currency.Code, code, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (missingCurrencies.Count > 0)
        {
            throw new InvalidOperationException($"Missing reference currencies: {string.Join(", ", missingCurrencies)}.");
        }

        var existingCountryIds = await _dbContext.TenantCountries
            .Where(item => item.TenantId == tenantId)
            .Select(item => item.CountryId)
            .ToListAsync(cancellationToken);
        var existingCurrencyIds = await _dbContext.TenantCurrencies
            .Where(item => item.TenantId == tenantId)
            .Select(item => item.CurrencyId)
            .ToListAsync(cancellationToken);

        var existingCountrySet = existingCountryIds.ToHashSet();
        var existingCurrencySet = existingCurrencyIds.ToHashSet();

        var now = _clock.UtcNow;
        var userId = _currentUserProvider.GetCurrentUserId();

        foreach (var country in countries)
        {
            if (existingCountrySet.Contains(country.Id))
            {
                continue;
            }

            _dbContext.TenantCountries.Add(new TenantCountry
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CountryId = country.Id,
                CreatedAt = now,
                CreatedBy = userId
            });
        }

        foreach (var currency in currencies)
        {
            if (existingCurrencySet.Contains(currency.Id))
            {
                continue;
            }

            _dbContext.TenantCurrencies.Add(new TenantCurrency
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CurrencyId = currency.Id,
                CreatedAt = now,
                CreatedBy = userId
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        operations.Add("Seeded UK-to-Africa tenant countries and currencies");

        return (
            countries.Select(country => country.Id).ToList(),
            currencies.Select(currency => currency.Id).ToList());
    }

    public async Task ReverseTenantCoverageAsync(
        Guid tenantId,
        List<string> operations,
        CancellationToken cancellationToken)
    {
        using var crossBorderDocument = await TryParseSettingDocumentAsync(tenantId, cancellationToken);
        var countryIds = ReadGuidArray(crossBorderDocument, "CrossBorder", "CountryIds");
        var currencyIds = ReadGuidArray(crossBorderDocument, "CrossBorder", "CurrencyIds");

        if (countryIds.Count == 0 && currencyIds.Count == 0)
        {
            return;
        }

        var countryCount = await _dbContext.TenantCountries
            .IncludeSoftDeleted()
            .Where(item => item.TenantId == tenantId && countryIds.Contains(item.CountryId))
            .ExecuteDeleteAsync(cancellationToken);

        var currencyCount = await _dbContext.TenantCurrencies
            .IncludeSoftDeleted()
            .Where(item => item.TenantId == tenantId && currencyIds.Contains(item.CurrencyId))
            .ExecuteDeleteAsync(cancellationToken);

        if (countryCount > 0 || currencyCount > 0)
        {
            operations.Add($"Removed {countryCount} tenant countries and {currencyCount} tenant currencies added for demo coverage");
        }
    }

    private async Task<JsonDocument?> TryParseSettingDocumentAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var setting = await _dbContext.Settings
            .FirstOrDefaultAsync(item => !item.IsDeleted
                                         && item.Scope == SettingScope.Tenant
                                         && item.TenantId == tenantId
                                         && item.Key == CrossBorderDemoSeedKey,
                cancellationToken);

        if (setting == null || string.IsNullOrWhiteSpace(setting.Value))
        {
            return null;
        }

        return JsonDocument.Parse(setting.Value);
    }

    private const string CrossBorderDemoSeedKey = "DemoSeed.CrossBorderPayments";

    private static HashSet<string> ParseSupportedCountries(string? supportedCountriesJson)
    {
        if (string.IsNullOrWhiteSpace(supportedCountriesJson))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var items = JsonSerializer.Deserialize<List<string>>(supportedCountriesJson);
            return items == null
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(items.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim().ToUpperInvariant()), StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static IReadOnlyList<Guid> ReadGuidArray(JsonDocument? document, string sectionName, string propertyName)
    {
        if (document == null)
        {
            return Array.Empty<Guid>();
        }

        if (!document.RootElement.TryGetProperty(sectionName, out var section))
        {
            return Array.Empty<Guid>();
        }

        if (!section.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<Guid>();
        }

        var ids = new List<Guid>();
        foreach (var item in property.EnumerateArray())
        {
            if (item.TryGetGuid(out var id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }
}
