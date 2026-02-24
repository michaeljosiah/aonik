using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Contracts.Services.Catalog;
using Aonik.Finance.Persistence;

namespace Aonik.Finance.Services.Catalog;

internal class PublicCatalogService : IPublicCatalogService
{
    private readonly FinanceDbContext _dbContext;

    public PublicCatalogService(FinanceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CatalogCountryResponse> GetCountriesAsync(CatalogCountryListRequest request, CancellationToken cancellationToken = default)
    {
        var countries = await _dbContext.Countries
            .AsNoTracking()
            .Where(country => country.IsActive)
            .OrderBy(country => country.Name)
            .Select(country => new CatalogCountryItem(country.IsoAlpha2, country.Name))
            .ToListAsync(cancellationToken);

        var capabilityType = string.IsNullOrWhiteSpace(request.CapabilityType) ? null : request.CapabilityType.Trim().ToUpperInvariant();
        var shouldFilterByServices = request.OnlyServiceCountries || !string.IsNullOrWhiteSpace(capabilityType);

        if (shouldFilterByServices && countries.Count > 0)
        {
            var serviceCountriesQuery = _dbContext.CatalogBillers
                .AsNoTracking().Where(biller => biller.IsActive)
                .Join(_dbContext.CatalogBillerServices.AsNoTracking().Where(service => service.IsActive),
                    biller => biller.Id, service => service.BillerId,
                    (biller, service) => new { biller.CountryCode, service.ServiceCode });

            if (!string.IsNullOrWhiteSpace(capabilityType))
            {
                var prefix = capabilityType + "%";
                serviceCountriesQuery = serviceCountriesQuery
                    .Where(item => item.ServiceCode != "" && EF.Functions.Like(item.ServiceCode, prefix));
            }

            var activeCountries = await serviceCountriesQuery.Select(item => item.CountryCode).Distinct().ToListAsync(cancellationToken);
            var allowed = new HashSet<string>(activeCountries, StringComparer.OrdinalIgnoreCase);
            countries = countries.Where(item => allowed.Contains(item.CountryCode)).ToList();
        }

        return new CatalogCountryResponse(countries);
    }

    public async Task<CatalogBillerCategoryResponse> GetCategoriesAsync(CatalogCategoryListRequest request, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.CatalogBillerCategories.AsNoTracking().Where(category => category.IsActive);
        if (!string.IsNullOrWhiteSpace(request.CountryCode))
            query = query.Where(category => category.CountryCode == request.CountryCode.Trim().ToUpperInvariant());

        var categories = await query.OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new CatalogBillerCategoryItem(c.Id, c.Name, c.Description, c.IconUrl, c.CountryCode))
            .ToListAsync(cancellationToken);

        return new CatalogBillerCategoryResponse(categories);
    }

    public async Task<CatalogBillerResponse> GetBillersAsync(CatalogBillerListRequest request, CancellationToken cancellationToken = default)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var query = _dbContext.CatalogBillers.AsNoTracking().Where(b => b.IsActive);
        if (!string.IsNullOrWhiteSpace(request.CountryCode))
            query = query.Where(b => b.CountryCode == request.CountryCode.Trim().ToUpperInvariant());
        if (request.CategoryId.HasValue)
            query = query.Where(b => b.CategoryId == request.CategoryId.Value);
        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(b => b.Name.Contains(request.Search.Trim()));

        var totalCount = await query.CountAsync(cancellationToken);
        var billers = await query.OrderBy(b => b.SortOrder).ThenBy(b => b.Name)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(b => new CatalogBillerSummaryItem(b.Id, b.Name, b.LogoUrl, b.CountryCode, b.CategoryId, b.CorrespondentPartnerId, b.IsActive, b.IsFeatured))
            .ToListAsync(cancellationToken);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        return new CatalogBillerResponse(billers, new CatalogPaginationMetadata(page, pageSize, totalCount, totalPages));
    }

    public async Task<CatalogBillerServiceResponse> GetBillerServicesAsync(Guid billerId, CancellationToken cancellationToken = default)
    {
        var services = await _dbContext.CatalogBillerServices.AsNoTracking()
            .Where(s => s.BillerId == billerId && s.IsActive)
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Name)
            .Select(s => new CatalogBillerServiceItem(s.Id, s.ServiceCode, s.Name, s.Type, s.Currency, s.MinAmount, s.MaxAmount, s.SupportsPartialPayment, s.RequiresValidation, s.IsActive))
            .ToListAsync(cancellationToken);
        return new CatalogBillerServiceResponse(services);
    }

    public async Task<CatalogBillerServiceDetailResponse?> GetBillerServiceDetailAsync(Guid billerId, Guid serviceId, CancellationToken cancellationToken = default)
    {
        var service = await _dbContext.CatalogBillerServices.AsNoTracking()
            .FirstOrDefaultAsync(item => item.BillerId == billerId && item.Id == serviceId, cancellationToken);
        if (service == null) return null;

        var fields = string.IsNullOrWhiteSpace(service.FieldsJson) ? new List<CatalogServiceField>()
            : JsonSerializer.Deserialize<List<CatalogServiceField>>(service.FieldsJson) ?? new List<CatalogServiceField>();
        var validation = string.IsNullOrWhiteSpace(service.ValidationJson) ? null
            : JsonSerializer.Deserialize<CatalogServiceValidation>(service.ValidationJson);

        return new CatalogBillerServiceDetailResponse(service.Id, service.ServiceCode, service.Name, service.Type, service.Currency, service.MinAmount, service.MaxAmount, service.SupportsPartialPayment, service.RequiresValidation, fields, validation);
    }

    public async Task<CatalogServiceFieldValidationResult?> ValidateServiceFieldsAsync(Guid billerId, Guid serviceId, CatalogServiceFieldValidationRequest request, CancellationToken cancellationToken = default)
    {
        var service = await _dbContext.CatalogBillerServices.AsNoTracking()
            .FirstOrDefaultAsync(item => item.BillerId == billerId && item.Id == serviceId, cancellationToken);
        if (service == null) return null;

        var now = DateTimeOffset.UtcNow;
        var fields = string.IsNullOrWhiteSpace(service.FieldsJson) ? new List<CatalogServiceField>()
            : JsonSerializer.Deserialize<List<CatalogServiceField>>(service.FieldsJson) ?? new List<CatalogServiceField>();
        var missingRequired = fields.Where(f => f.Required).Select(f => f.Key)
            .Where(key => string.IsNullOrWhiteSpace(key) || !request.FieldValues.ContainsKey(key)).ToList();

        if (missingRequired.Count > 0)
            return new CatalogServiceFieldValidationResult(false, now, "MISSING_REQUIRED_FIELD", $"Missing required fields: {string.Join(", ", missingRequired)}", null, null);

        return new CatalogServiceFieldValidationResult(true, now, null, null, null, null);
    }
}
