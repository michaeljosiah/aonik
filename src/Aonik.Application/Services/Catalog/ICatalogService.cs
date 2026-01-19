using Aonik.Application.Models.Catalog;

namespace Aonik.Application.Services.Catalog;

public interface ICatalogService
{
    Task<CatalogCountryResponse> GetCountriesAsync(CatalogCountryListRequest request, CancellationToken cancellationToken = default);
    Task<CatalogBillerCategoryResponse> GetCategoriesAsync(CatalogCategoryListRequest request, CancellationToken cancellationToken = default);
    Task<CatalogBillerResponse> GetBillersAsync(CatalogBillerListRequest request, CancellationToken cancellationToken = default);
    Task<CatalogBillerDetailResponse?> GetBillerDetailAsync(Guid billerId, CancellationToken cancellationToken = default);
    Task<CatalogBillerServiceResponse> GetBillerServicesAsync(Guid billerId, CancellationToken cancellationToken = default);
    Task<CatalogBillerServiceDetailResponse?> GetBillerServiceDetailAsync(Guid billerId, Guid serviceId, CancellationToken cancellationToken = default);
}
