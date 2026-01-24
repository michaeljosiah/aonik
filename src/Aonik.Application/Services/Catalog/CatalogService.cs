using System.Text.Json;
using Microsoft.EntityFrameworkCore;

using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Abstractions.ReferenceData;
using Aonik.Application.Models.Catalog;
using Aonik.Application.Services.Identity;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Application.Services.Catalog;

public class CatalogService : ICatalogService
{
    private const string CountryReferenceType = "Country";
    private readonly IAonikDbContext _dbContext;
    private readonly IReferenceDataService _referenceDataService;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserProvider _currentUserProvider;

    public CatalogService(
        IAonikDbContext dbContext,
        IReferenceDataService referenceDataService,
        IPermissionService permissionService,
        ICurrentUserProvider currentUserProvider)
    {
        _dbContext = dbContext;
        _referenceDataService = referenceDataService;
        _permissionService = permissionService;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<CatalogCountryResponse> GetCountriesAsync(
        CatalogCountryListRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync(cancellationToken);
        var items = await _referenceDataService.GetAsync(CountryReferenceType, cancellationToken: cancellationToken);
        var countries = items
            .Where(item => item.IsActive)
            .Select(item => new CatalogCountryItem(item.Code, item.DisplayName))
            .OrderBy(item => item.Name)
            .ToList();

        if (request.OnlyServiceCountries && countries.Count > 0)
        {
            var activeCountries = await _dbContext.CatalogBillers
                .AsNoTracking()
                .Where(biller => biller.IsActive)
                .Select(biller => biller.CountryCode)
                .Distinct()
                .ToListAsync(cancellationToken);

            var allowed = new HashSet<string>(activeCountries, StringComparer.OrdinalIgnoreCase);
            countries = countries.Where(item => allowed.Contains(item.CountryCode)).ToList();
        }

        return new CatalogCountryResponse(countries);
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

    private async Task EnsurePermissionAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUserProvider.GetCurrentUserId();
        if (!userId.HasValue)
        {
            throw new InvalidOperationException("Authenticated user is required.");
        }

        var hasPermission = await _permissionService.HasPermissionAsync(userId.Value, "Catalog.Read", cancellationToken);
        if (!hasPermission)
        {
            throw new InvalidOperationException("Permission Catalog.Read is required.");
        }
    }
}
