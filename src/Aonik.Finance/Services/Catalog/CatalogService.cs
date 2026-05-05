using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Contracts.Services.Catalog;
using Aonik.Finance.Entities.Catalog;
using Aonik.Finance.Entities.Partners;
using Aonik.Finance.Entities.ReferenceData;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Finance.Services.Catalog;

internal class CatalogService : ICatalogService
{
    private readonly FinanceDbContext _dbContext;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    public CatalogService(
        FinanceDbContext dbContext,
        IPermissionService permissionService,
        ICurrentUserProvider currentUserProvider,
        ITenantContext tenantContext,
        IClock clock)
    {
        _dbContext = dbContext;
        _permissionService = permissionService;
        _currentUserProvider = currentUserProvider;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    private async Task EnsurePermissionAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUserProvider.GetCurrentUserId();
        if (!userId.HasValue)
            throw new InvalidOperationException("Authenticated user is required.");
        await Task.CompletedTask;
    }

    /// <summary>
    /// Permission gate for catalog mutations. Reads stay open to any
    /// authenticated tenant user (matching the pre-existing read behaviour);
    /// writes require Catalog.Write, which the TenantAdmin role holds.
    /// </summary>
    private async Task EnsureWritePermissionAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUserProvider.GetCurrentUserId();
        if (!userId.HasValue)
            throw new PermissionDeniedException("Catalog.Write", "Authenticated user is required.");

        var hasPermission = await _permissionService.HasPermissionAsync(userId.Value, "Catalog.Write", cancellationToken);
        if (!hasPermission)
            throw new PermissionDeniedException("Catalog.Write");
    }

    private Guid GetCurrentTenantIdOrThrow()
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId is null || tenantId.Value == Guid.Empty)
            throw new InvalidOperationException("A tenant context is required for catalog mutations.");
        return tenantId.Value;
    }

    private static string NormaliseCountryCode(string countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
            throw new ArgumentException("CountryCode is required.", nameof(countryCode));
        var trimmed = countryCode.Trim().ToUpperInvariant();
        if (trimmed.Length != 2)
            throw new ArgumentException("CountryCode must be ISO-3166-1 alpha-2 (e.g. 'US').", nameof(countryCode));
        return trimmed;
    }

    public async Task<CatalogCountryResponse> GetCountriesAsync(CatalogCountryListRequest request, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync(cancellationToken);
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

    public async Task<CatalogCurrencyResponse> GetCurrenciesAsync(CatalogCurrencyListRequest request, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync(cancellationToken);

        var normalizedCountryCode = string.IsNullOrWhiteSpace(request.CountryCode) ? null : request.CountryCode.Trim().ToUpperInvariant();
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
            query = query.Where(currency => currency.IsActive);

        var currencies = await query
            .OrderBy(currency => currency.SortOrder)
            .ThenBy(currency => currency.Name)
            .Select(currency => new CatalogCurrencyItem(currency.Code, currency.Name))
            .ToListAsync(cancellationToken);

        if (countryCurrencyCodes is { Count: > 0 })
        {
            var availableCodes = new HashSet<string>(currencies.Select(c => c.Code), StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(defaultCurrencyCode) && !availableCodes.Contains(defaultCurrencyCode))
                defaultCurrencyCode = null;

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

    public async Task<CatalogBillerCategoryResponse> GetCategoriesAsync(CatalogCategoryListRequest request, CancellationToken cancellationToken = default)
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
            .Select(category => new CatalogBillerCategoryItem(category.Id, category.Name, category.Description, category.IconUrl, category.CountryCode))
            .ToListAsync(cancellationToken);

        return new CatalogBillerCategoryResponse(categories);
    }

    public async Task<CatalogBillerResponse> GetBillersAsync(CatalogBillerListRequest request, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync(cancellationToken);
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var query = _dbContext.CatalogBillers.AsNoTracking().Where(biller => biller.IsActive);

        if (!string.IsNullOrWhiteSpace(request.CountryCode))
            query = query.Where(biller => biller.CountryCode == request.CountryCode.Trim().ToUpperInvariant());
        if (request.CategoryId.HasValue)
            query = query.Where(biller => biller.CategoryId == request.CategoryId.Value);
        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(biller => biller.Name.Contains(request.Search.Trim()));

        var totalCount = await query.CountAsync(cancellationToken);
        var billers = await query
            .OrderBy(biller => biller.SortOrder).ThenBy(biller => biller.Name)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(biller => new CatalogBillerSummaryItem(biller.Id, biller.Name, biller.LogoUrl, biller.CountryCode, biller.CategoryId, biller.CorrespondentPartnerId, biller.IsActive, biller.IsFeatured))
            .ToListAsync(cancellationToken);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        return new CatalogBillerResponse(billers, new CatalogPaginationMetadata(page, pageSize, totalCount, totalPages));
    }

    public async Task<CatalogBillerDetailResponse?> GetBillerDetailAsync(Guid billerId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync(cancellationToken);
        var biller = await _dbContext.CatalogBillers.AsNoTracking().FirstOrDefaultAsync(item => item.Id == billerId, cancellationToken);
        if (biller == null) return null;

        var serviceCount = await _dbContext.CatalogBillerServices.AsNoTracking()
            .CountAsync(service => service.BillerId == billerId && service.IsActive, cancellationToken);

        return new CatalogBillerDetailResponse(biller.Id, biller.Name, biller.Description, biller.LogoUrl, biller.BannerUrl, biller.SupportPhone, biller.SupportEmail, biller.CountryCode, biller.CategoryId, biller.CorrespondentPartnerId, biller.IsActive, serviceCount);
    }

    public async Task<CatalogBillerServiceResponse> GetBillerServicesAsync(Guid billerId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync(cancellationToken);
        var services = await _dbContext.CatalogBillerServices.AsNoTracking()
            .Where(service => service.BillerId == billerId && service.IsActive)
            .OrderBy(service => service.SortOrder).ThenBy(service => service.Name)
            .Select(service => new CatalogBillerServiceItem(service.Id, service.ServiceCode, service.Name, service.Type, service.Currency, service.MinAmount, service.MaxAmount, service.SupportsPartialPayment, service.RequiresValidation, service.IsActive))
            .ToListAsync(cancellationToken);

        return new CatalogBillerServiceResponse(services);
    }

    public async Task<CatalogBillerServiceDetailResponse?> GetBillerServiceDetailAsync(Guid billerId, Guid serviceId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync(cancellationToken);
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

    // ── Mutation surface ──────────────────────────────────────────────────
    //
    // All mutations are tenant-scoped via ITenantContext. The EF query
    // filter on ITenantScoped already restricts read-by-id to the current
    // tenant, so a write attempt against an id that belongs to a different
    // tenant manifests as a NotFound, not a 403. CatalogBiller has a
    // mandatory CorrespondentPartnerId FK to Partner, so we lazy-create a
    // single "Self" partner per tenant when a caller does not supply one.

    public async Task<CatalogBillerCategoryItem> CreateCategoryAsync(
        CreateCatalogBillerCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureWritePermissionAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Name is required.", nameof(request));

        var tenantId = GetCurrentTenantIdOrThrow();
        var countryCode = NormaliseCountryCode(request.CountryCode);
        var name = request.Name.Trim();

        // Block exact-duplicate names within the same tenant + country (case-insensitive).
        var duplicateExists = await _dbContext.CatalogBillerCategories
            .AnyAsync(c => c.TenantId == tenantId
                && c.CountryCode == countryCode
                && c.Name == name, cancellationToken);
        if (duplicateExists)
            throw new InvalidOperationException($"A category named '{name}' already exists for {countryCode}.");

        var now = _clock.UtcNow;
        var userId = _currentUserProvider.GetCurrentUserId();
        var entity = new CatalogBillerCategory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CountryCode = countryCode,
            Name = name,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IconUrl = string.IsNullOrWhiteSpace(request.IconUrl) ? null : request.IconUrl.Trim(),
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            CreatedAt = now,
            CreatedBy = userId
        };

        _dbContext.CatalogBillerCategories.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CatalogBillerCategoryItem(entity.Id, entity.Name, entity.Description, entity.IconUrl, entity.CountryCode);
    }

    public async Task<CatalogBillerCategoryItem> UpdateCategoryAsync(
        Guid categoryId,
        UpdateCatalogBillerCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureWritePermissionAsync(cancellationToken);

        var tenantId = GetCurrentTenantIdOrThrow();
        var entity = await _dbContext.CatalogBillerCategories
            .FirstOrDefaultAsync(c => c.Id == categoryId, cancellationToken);

        if (entity is null || entity.TenantId != tenantId)
            throw new InvalidOperationException($"Category {categoryId} not found.");

        var now = _clock.UtcNow;
        var userId = _currentUserProvider.GetCurrentUserId();

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            var newName = request.Name.Trim();
            if (!string.Equals(newName, entity.Name, StringComparison.Ordinal))
            {
                var duplicateExists = await _dbContext.CatalogBillerCategories
                    .AnyAsync(c => c.TenantId == tenantId
                        && c.CountryCode == entity.CountryCode
                        && c.Name == newName
                        && c.Id != categoryId, cancellationToken);
                if (duplicateExists)
                    throw new InvalidOperationException($"A category named '{newName}' already exists for {entity.CountryCode}.");
                entity.Name = newName;
            }
        }
        if (request.Description != null)
            entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        if (request.IconUrl != null)
            entity.IconUrl = string.IsNullOrWhiteSpace(request.IconUrl) ? null : request.IconUrl.Trim();
        if (request.SortOrder.HasValue)
            entity.SortOrder = request.SortOrder.Value;
        if (request.IsActive.HasValue)
            entity.IsActive = request.IsActive.Value;

        entity.UpdatedAt = now;
        entity.UpdatedBy = userId;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CatalogBillerCategoryItem(entity.Id, entity.Name, entity.Description, entity.IconUrl, entity.CountryCode);
    }

    public async Task DeleteCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        await EnsureWritePermissionAsync(cancellationToken);

        var tenantId = GetCurrentTenantIdOrThrow();
        var entity = await _dbContext.CatalogBillerCategories
            .FirstOrDefaultAsync(c => c.Id == categoryId, cancellationToken);

        if (entity is null || entity.TenantId != tenantId)
            throw new InvalidOperationException($"Category {categoryId} not found.");

        // Block delete when a biller still references this category — otherwise
        // the FK from CatalogBillers would dangle, and our soft-delete
        // interceptor would mask the cascade by leaving stale references behind.
        var hasBillers = await _dbContext.CatalogBillers
            .AnyAsync(b => b.TenantId == tenantId && b.CategoryId == categoryId, cancellationToken);
        if (hasBillers)
            throw new InvalidOperationException("Cannot delete a category that still has billers. Reassign or delete the billers first.");

        // Soft-delete via the SaveChanges interceptor (Remove → IsDeleted=true).
        _dbContext.CatalogBillerCategories.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<CatalogBillerDetailResponse> CreateBillerAsync(
        CreateCatalogBillerRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureWritePermissionAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Name is required.", nameof(request));
        if (request.CategoryId == Guid.Empty)
            throw new ArgumentException("CategoryId is required.", nameof(request));

        var tenantId = GetCurrentTenantIdOrThrow();
        var countryCode = NormaliseCountryCode(request.CountryCode);
        var name = request.Name.Trim();

        // Validate the category belongs to this tenant.
        var category = await _dbContext.CatalogBillerCategories
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken);
        if (category is null || category.TenantId != tenantId)
            throw new InvalidOperationException($"Category {request.CategoryId} not found.");

        // Resolve / lazy-create the correspondent partner.
        var partnerId = await ResolveOrCreateCorrespondentPartnerAsync(
            tenantId, request.CorrespondentPartnerId, cancellationToken);

        // Block duplicate biller names within the tenant + country.
        var duplicateExists = await _dbContext.CatalogBillers
            .AnyAsync(b => b.TenantId == tenantId
                && b.CountryCode == countryCode
                && b.Name == name, cancellationToken);
        if (duplicateExists)
            throw new InvalidOperationException($"A biller named '{name}' already exists for {countryCode}.");

        var now = _clock.UtcNow;
        var userId = _currentUserProvider.GetCurrentUserId();
        var biller = new CatalogBiller
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CategoryId = request.CategoryId,
            CorrespondentPartnerId = partnerId,
            CountryCode = countryCode,
            Name = name,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            LogoUrl = string.IsNullOrWhiteSpace(request.LogoUrl) ? null : request.LogoUrl.Trim(),
            BannerUrl = string.IsNullOrWhiteSpace(request.BannerUrl) ? null : request.BannerUrl.Trim(),
            SupportPhone = string.IsNullOrWhiteSpace(request.SupportPhone) ? null : request.SupportPhone.Trim(),
            SupportEmail = string.IsNullOrWhiteSpace(request.SupportEmail) ? null : request.SupportEmail.Trim(),
            IsActive = request.IsActive,
            IsFeatured = request.IsFeatured,
            SortOrder = request.SortOrder,
            CreatedAt = now,
            CreatedBy = userId
        };

        _dbContext.CatalogBillers.Add(biller);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CatalogBillerDetailResponse(
            biller.Id, biller.Name, biller.Description, biller.LogoUrl, biller.BannerUrl,
            biller.SupportPhone, biller.SupportEmail, biller.CountryCode, biller.CategoryId,
            biller.CorrespondentPartnerId, biller.IsActive, ServiceCount: 0);
    }

    public async Task<CatalogBillerDetailResponse> UpdateBillerAsync(
        Guid billerId,
        UpdateCatalogBillerRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureWritePermissionAsync(cancellationToken);

        var tenantId = GetCurrentTenantIdOrThrow();
        var biller = await _dbContext.CatalogBillers
            .FirstOrDefaultAsync(b => b.Id == billerId, cancellationToken);
        if (biller is null || biller.TenantId != tenantId)
            throw new InvalidOperationException($"Biller {billerId} not found.");

        var now = _clock.UtcNow;
        var userId = _currentUserProvider.GetCurrentUserId();

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            var newName = request.Name.Trim();
            if (!string.Equals(newName, biller.Name, StringComparison.Ordinal))
            {
                var duplicateExists = await _dbContext.CatalogBillers
                    .AnyAsync(b => b.TenantId == tenantId
                        && b.CountryCode == biller.CountryCode
                        && b.Name == newName
                        && b.Id != billerId, cancellationToken);
                if (duplicateExists)
                    throw new InvalidOperationException($"A biller named '{newName}' already exists for {biller.CountryCode}.");
                biller.Name = newName;
            }
        }
        if (request.CategoryId.HasValue && request.CategoryId.Value != Guid.Empty)
        {
            var category = await _dbContext.CatalogBillerCategories
                .FirstOrDefaultAsync(c => c.Id == request.CategoryId.Value, cancellationToken);
            if (category is null || category.TenantId != tenantId)
                throw new InvalidOperationException($"Category {request.CategoryId.Value} not found.");
            biller.CategoryId = request.CategoryId.Value;
        }
        if (request.CorrespondentPartnerId.HasValue && request.CorrespondentPartnerId.Value != Guid.Empty)
        {
            var partner = await _dbContext.Partners
                .FirstOrDefaultAsync(p => p.Id == request.CorrespondentPartnerId.Value, cancellationToken);
            if (partner is null || partner.TenantId != tenantId)
                throw new InvalidOperationException($"Partner {request.CorrespondentPartnerId.Value} not found.");
            biller.CorrespondentPartnerId = request.CorrespondentPartnerId.Value;
        }
        if (request.Description != null)
            biller.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        if (request.LogoUrl != null)
            biller.LogoUrl = string.IsNullOrWhiteSpace(request.LogoUrl) ? null : request.LogoUrl.Trim();
        if (request.BannerUrl != null)
            biller.BannerUrl = string.IsNullOrWhiteSpace(request.BannerUrl) ? null : request.BannerUrl.Trim();
        if (request.SupportPhone != null)
            biller.SupportPhone = string.IsNullOrWhiteSpace(request.SupportPhone) ? null : request.SupportPhone.Trim();
        if (request.SupportEmail != null)
            biller.SupportEmail = string.IsNullOrWhiteSpace(request.SupportEmail) ? null : request.SupportEmail.Trim();
        if (request.IsActive.HasValue)
            biller.IsActive = request.IsActive.Value;
        if (request.IsFeatured.HasValue)
            biller.IsFeatured = request.IsFeatured.Value;
        if (request.SortOrder.HasValue)
            biller.SortOrder = request.SortOrder.Value;

        biller.UpdatedAt = now;
        biller.UpdatedBy = userId;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var serviceCount = await _dbContext.CatalogBillerServices
            .CountAsync(s => s.BillerId == billerId && s.IsActive, cancellationToken);

        return new CatalogBillerDetailResponse(
            biller.Id, biller.Name, biller.Description, biller.LogoUrl, biller.BannerUrl,
            biller.SupportPhone, biller.SupportEmail, biller.CountryCode, biller.CategoryId,
            biller.CorrespondentPartnerId, biller.IsActive, serviceCount);
    }

    public async Task DeleteBillerAsync(Guid billerId, CancellationToken cancellationToken = default)
    {
        await EnsureWritePermissionAsync(cancellationToken);

        var tenantId = GetCurrentTenantIdOrThrow();
        var biller = await _dbContext.CatalogBillers
            .FirstOrDefaultAsync(b => b.Id == billerId, cancellationToken);
        if (biller is null || biller.TenantId != tenantId)
            throw new InvalidOperationException($"Biller {billerId} not found.");

        // Soft-delete via the interceptor; child services stay tied to the
        // soft-deleted biller and inherit invisibility through the EF query
        // filter on AuditableEntity.IsDeleted.
        _dbContext.CatalogBillers.Remove(biller);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Returns the partner id to attach to a new biller.
    ///   - If the caller specified one, validate it belongs to the tenant.
    ///   - Otherwise reuse the tenant's first existing partner, or
    ///     lazy-create a "{TenantName}" Partner row marked as the
    ///     self-correspondent. CatalogBiller.CorrespondentPartnerId is
    ///     non-nullable with an FK to Partner, so we cannot leave it empty.
    /// </summary>
    private async Task<Guid> ResolveOrCreateCorrespondentPartnerAsync(
        Guid tenantId,
        Guid? requestedPartnerId,
        CancellationToken cancellationToken)
    {
        if (requestedPartnerId.HasValue && requestedPartnerId.Value != Guid.Empty)
        {
            var partner = await _dbContext.Partners
                .FirstOrDefaultAsync(p => p.Id == requestedPartnerId.Value, cancellationToken);
            if (partner is null || partner.TenantId != tenantId)
                throw new InvalidOperationException($"Partner {requestedPartnerId.Value} not found.");
            return partner.Id;
        }

        var existing = await _dbContext.Partners
            .OrderBy(p => p.Name)
            .FirstOrDefaultAsync(p => p.TenantId == tenantId, cancellationToken);
        if (existing is not null)
            return existing.Id;

        var now = _clock.UtcNow;
        var userId = _currentUserProvider.GetCurrentUserId();
        var newPartner = new Partner
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Self",
            Status = "Active",
            CapabilitiesJson = "[]",
            OperatingHoursJson = "{}",
            CreatedAt = now,
            CreatedBy = userId
        };
        _dbContext.Partners.Add(newPartner);
        // Saved by the caller's SaveChangesAsync.
        return newPartner.Id;
    }
}
