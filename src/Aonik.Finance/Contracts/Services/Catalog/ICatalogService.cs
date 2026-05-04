using Aonik.Finance.Contracts.Models.Catalog;

namespace Aonik.Finance.Contracts.Services.Catalog;

public interface ICatalogService
{
    Task<CatalogCountryResponse> GetCountriesAsync(CatalogCountryListRequest request, CancellationToken cancellationToken = default);
    Task<CatalogCurrencyResponse> GetCurrenciesAsync(CatalogCurrencyListRequest request, CancellationToken cancellationToken = default);
    Task<CatalogBillerCategoryResponse> GetCategoriesAsync(CatalogCategoryListRequest request, CancellationToken cancellationToken = default);
    Task<CatalogBillerResponse> GetBillersAsync(CatalogBillerListRequest request, CancellationToken cancellationToken = default);
    Task<CatalogBillerDetailResponse?> GetBillerDetailAsync(Guid billerId, CancellationToken cancellationToken = default);
    Task<CatalogBillerServiceResponse> GetBillerServicesAsync(Guid billerId, CancellationToken cancellationToken = default);
    Task<CatalogBillerServiceDetailResponse?> GetBillerServiceDetailAsync(Guid billerId, Guid serviceId, CancellationToken cancellationToken = default);
    Task<CatalogServiceFieldValidationResult?> ValidateServiceFieldsAsync(Guid billerId, Guid serviceId, CatalogServiceFieldValidationRequest request, CancellationToken cancellationToken = default);

    // ── Mutation surface ──────────────────────────────────────────────────
    // All mutations are scoped to the current tenant. The TenantId is
    // resolved from ITenantContext, not from the request. Permission gate:
    // Catalog.Write (granted to TenantAdmin by default).

    Task<CatalogBillerCategoryItem> CreateCategoryAsync(CreateCatalogBillerCategoryRequest request, CancellationToken cancellationToken = default);
    Task<CatalogBillerCategoryItem> UpdateCategoryAsync(Guid categoryId, UpdateCatalogBillerCategoryRequest request, CancellationToken cancellationToken = default);
    Task DeleteCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default);

    Task<CatalogBillerDetailResponse> CreateBillerAsync(CreateCatalogBillerRequest request, CancellationToken cancellationToken = default);
    Task<CatalogBillerDetailResponse> UpdateBillerAsync(Guid billerId, UpdateCatalogBillerRequest request, CancellationToken cancellationToken = default);
    Task DeleteBillerAsync(Guid billerId, CancellationToken cancellationToken = default);
}
