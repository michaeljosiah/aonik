using System.Text.Json;
using Microsoft.EntityFrameworkCore;

using Aonik.Application.Abstractions.Persistence;
using Aonik.Platform.Contracts.Services.ReferenceData;
using Aonik.Application.Models.Catalog;
using Aonik.Application.Services;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Application.Services.Catalog;

public class CatalogService : AdminServiceBase, ICatalogService
{
    private readonly IAonikDbContext _dbContext;

    public CatalogService(
        IAonikDbContext dbContext,
        IPermissionService permissionService,
        ICurrentUserProvider currentUserProvider)
        : base(currentUserProvider, permissionService)
    {
        _dbContext = dbContext;
    }

    public async Task<CatalogCountryResponse> GetCountriesAsync(
        CatalogCountryListRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync(cancellationToken);
        var countries = await _dbContext.Countries
            .AsNoTracking()
            .Where(country => country.IsActive)
            .OrderBy(country => country.Name)
            .Select(country => new CatalogCountryItem(country.IsoAlpha2, country.Name))
            .ToListAsync(cancellationToken);

        var capabilityType = NormalizeCapabilityType(request.CapabilityType);
        var shouldFilterByServices = request.OnlyServiceCountries || !string.IsNullOrWhiteSpace(capabilityType);

        if (shouldFilterByServices && countries.Count > 0)
        {
            var serviceCountriesQuery = _dbContext.CatalogBillers
                .AsNoTracking()
                .Where(biller => biller.IsActive)
                .Join(_dbContext.CatalogBillerServices.AsNoTracking().Where(service => service.IsActive),
                    biller => biller.Id,
                    service => service.BillerId,
                    (biller, service) => new { biller.CountryCode, service.ServiceCode });

            if (!string.IsNullOrWhiteSpace(capabilityType))
            {
                var prefix = capabilityType + "%";
                serviceCountriesQuery = serviceCountriesQuery
                    .Where(item => item.ServiceCode != "" && EF.Functions.Like(item.ServiceCode, prefix));
            }

            var activeCountries = await serviceCountriesQuery
                .Select(item => item.CountryCode)
                .Distinct()
                .ToListAsync(cancellationToken);

            var allowed = new HashSet<string>(activeCountries, StringComparer.OrdinalIgnoreCase);
            countries = countries.Where(item => allowed.Contains(item.CountryCode)).ToList();
        }

        return new CatalogCountryResponse(countries);
    }

    public async Task<CatalogCurrencyResponse> GetCurrenciesAsync(
        CatalogCurrencyListRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync(cancellationToken);

        var normalizedCountryCode = NormalizeCountryCode(request.CountryCode);
        string? defaultCurrencyCode = null;
        HashSet<string>? countryCurrencyCodes = null;

        if (!string.IsNullOrWhiteSpace(normalizedCountryCode))
        {
            var countryId = await _dbContext.Countries
                .AsNoTracking()
                .Where(country => country.IsoAlpha2 == normalizedCountryCode)
                .Select(country => (Guid?)country.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (countryId.HasValue)
            {
                var countryCurrencies = await _dbContext.CountryCurrencies
                    .AsNoTracking()
                    .Where(mapping => mapping.CountryId == countryId.Value)
                    .Select(mapping => new { mapping.CurrencyCode, mapping.IsDefault })
                    .ToListAsync(cancellationToken);

                if (countryCurrencies.Count > 0)
                {
                    countryCurrencyCodes = new HashSet<string>(
                        countryCurrencies.Select(mapping => mapping.CurrencyCode),
                        StringComparer.OrdinalIgnoreCase);

                    defaultCurrencyCode = countryCurrencies
                        .FirstOrDefault(mapping => mapping.IsDefault)?.CurrencyCode;
                }
            }
        }

        var query = _dbContext.Currencies
            .AsNoTracking()
            .Where(currency => currency.TenantId == null);

        if (!request.IncludeInactive)
        {
            query = query.Where(currency => currency.IsActive);
        }

        var currencies = await query
            .OrderBy(currency => currency.SortOrder)
            .ThenBy(currency => currency.Name)
            .Select(currency => new CatalogCurrencyItem(currency.Code, currency.Name))
            .ToListAsync(cancellationToken);

        if (countryCurrencyCodes is { Count: > 0 })
        {
            var availableCodes = new HashSet<string>(
                currencies.Select(currency => currency.Code),
                StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(defaultCurrencyCode) && !availableCodes.Contains(defaultCurrencyCode))
            {
                defaultCurrencyCode = null;
            }

            currencies = currencies
                .Select((currency, index) => new { currency, index })
                .OrderBy(item =>
                    string.Equals(item.currency.Code, defaultCurrencyCode, StringComparison.OrdinalIgnoreCase) ? 0 :
                    countryCurrencyCodes.Contains(item.currency.Code) ? 1 : 2)
                .ThenBy(item => item.index)
                .Select(item => item.currency)
                .ToList();
        }
        else
        {
            defaultCurrencyCode = null;
        }

        return new CatalogCurrencyResponse(currencies, defaultCurrencyCode);
    }

    public async Task<CatalogBillerCategoryResponse> GetCategoriesAsync(
        CatalogCategoryListRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync(cancellationToken);
        var query = _dbContext.CatalogBillerCategories
            .AsNoTracking()
            .Where(category => category.IsActive);

        if (!string.IsNullOrWhiteSpace(request.CountryCode))
        {
            var countryCode = request.CountryCode.Trim().ToUpperInvariant();
            query = query.Where(category => category.CountryCode == countryCode);
        }

        var categories = await query
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Name)
            .Select(category => new CatalogBillerCategoryItem(
                category.Id,
                category.Name,
                category.Description,
                category.IconUrl,
                category.CountryCode))
            .ToListAsync(cancellationToken);

        return new CatalogBillerCategoryResponse(categories);
    }

    public async Task<CatalogBillerResponse> GetBillersAsync(
        CatalogBillerListRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync(cancellationToken);
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var query = _dbContext.CatalogBillers
            .AsNoTracking()
            .Where(biller => biller.IsActive);

        if (!string.IsNullOrWhiteSpace(request.CountryCode))
        {
            var countryCode = request.CountryCode.Trim().ToUpperInvariant();
            query = query.Where(biller => biller.CountryCode == countryCode);
        }

        if (request.CategoryId.HasValue)
        {
            query = query.Where(biller => biller.CategoryId == request.CategoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(biller => biller.Name.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var billers = await query
            .OrderBy(biller => biller.SortOrder)
            .ThenBy(biller => biller.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(biller => new CatalogBillerSummaryItem(
                biller.Id,
                biller.Name,
                biller.LogoUrl,
                biller.CountryCode,
                biller.CategoryId,
                biller.CorrespondentPartnerId,
                biller.IsActive,
                biller.IsFeatured))
            .ToListAsync(cancellationToken);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        var metadata = new CatalogPaginationMetadata(page, pageSize, totalCount, totalPages);

        return new CatalogBillerResponse(billers, metadata);
    }

    public async Task<CatalogBillerDetailResponse?> GetBillerDetailAsync(Guid billerId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync(cancellationToken);
        var biller = await _dbContext.CatalogBillers
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == billerId, cancellationToken);

        if (biller == null)
        {
            return null;
        }

        var serviceCount = await _dbContext.CatalogBillerServices
            .AsNoTracking()
            .CountAsync(service => service.BillerId == billerId && service.IsActive, cancellationToken);

        return new CatalogBillerDetailResponse(
            biller.Id,
            biller.Name,
            biller.Description,
            biller.LogoUrl,
            biller.BannerUrl,
            biller.SupportPhone,
            biller.SupportEmail,
            biller.CountryCode,
            biller.CategoryId,
            biller.CorrespondentPartnerId,
            biller.IsActive,
            serviceCount);
    }

    public async Task<CatalogBillerServiceResponse> GetBillerServicesAsync(Guid billerId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync(cancellationToken);
        var services = await _dbContext.CatalogBillerServices
            .AsNoTracking()
            .Where(service => service.BillerId == billerId && service.IsActive)
            .OrderBy(service => service.SortOrder)
            .ThenBy(service => service.Name)
            .Select(service => new CatalogBillerServiceItem(
                service.Id,
                service.ServiceCode,
                service.Name,
                service.Type,
                service.Currency,
                service.MinAmount,
                service.MaxAmount,
                service.SupportsPartialPayment,
                service.RequiresValidation,
                service.IsActive))
            .ToListAsync(cancellationToken);

        return new CatalogBillerServiceResponse(services);
    }

    public async Task<CatalogBillerServiceDetailResponse?> GetBillerServiceDetailAsync(
        Guid billerId,
        Guid serviceId,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync(cancellationToken);
        var service = await _dbContext.CatalogBillerServices
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.BillerId == billerId && item.Id == serviceId, cancellationToken);

        if (service == null)
        {
            return null;
        }

        var fields = DeserializeFields(service.FieldsJson);
        var validation = DeserializeValidation(service.ValidationJson);

        return new CatalogBillerServiceDetailResponse(
            service.Id,
            service.ServiceCode,
            service.Name,
            service.Type,
            service.Currency,
            service.MinAmount,
            service.MaxAmount,
            service.SupportsPartialPayment,
            service.RequiresValidation,
            fields,
            validation);
    }

    public async Task<CatalogServiceFieldValidationResult?> ValidateServiceFieldsAsync(
        Guid billerId,
        Guid serviceId,
        CatalogServiceFieldValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync(cancellationToken);
        var service = await _dbContext.CatalogBillerServices
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.BillerId == billerId && item.Id == serviceId, cancellationToken);

        if (service == null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var fields = DeserializeFields(service.FieldsJson);
        var missingRequired = fields
            .Where(field => field.Required)
            .Select(field => field.Key)
            .Where(key => string.IsNullOrWhiteSpace(key) || !request.FieldValues.ContainsKey(key))
            .ToList();

        if (missingRequired.Count > 0)
        {
            return new CatalogServiceFieldValidationResult(
                false,
                now,
                "MISSING_REQUIRED_FIELD",
                $"Missing required fields: {string.Join(", ", missingRequired)}",
                null,
                null);
        }

        return new CatalogServiceFieldValidationResult(
            true,
            now,
            null,
            null,
            null,
            null);
    }

    private static List<CatalogServiceField> DeserializeFields(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<CatalogServiceField>();
        }

        return JsonSerializer.Deserialize<List<CatalogServiceField>>(json) ?? new List<CatalogServiceField>();
    }

    private static CatalogServiceValidation? DeserializeValidation(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<CatalogServiceValidation>(json);
    }

    private static string? NormalizeCapabilityType(string? capabilityType)
    {
        return string.IsNullOrWhiteSpace(capabilityType)
            ? null
            : capabilityType.Trim().ToUpperInvariant();
    }

    private static string? NormalizeCountryCode(string? countryCode)
    {
        return string.IsNullOrWhiteSpace(countryCode)
            ? null
            : countryCode.Trim().ToUpperInvariant();
    }

    private Task EnsurePermissionAsync(CancellationToken cancellationToken)
    {
        return base.EnsurePermissionAsync("Catalog.Read", cancellationToken);
    }
}
