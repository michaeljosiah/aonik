
using Aonik.Application.Models.Catalog;

namespace Aonik.Application.Services.Catalog;

public interface IPublicCatalogService
{
    Task<CatalogCountryResponse> GetCountriesAsync(
        CatalogCountryListRequest request,
        CancellationToken cancellationToken = default);
}
