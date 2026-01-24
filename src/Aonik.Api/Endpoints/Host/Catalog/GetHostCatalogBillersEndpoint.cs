using FastEndpoints;

using Aonik.Api.Contracts.Catalog;
using Aonik.Application.Services.Catalog;

namespace Aonik.Api.Endpoints.Host.Catalog;

public class GetHostCatalogBillersEndpoint : EndpointWithoutRequest<CatalogBillerResponse>
{
    private readonly ICatalogService _catalogService;

    public GetHostCatalogBillersEndpoint(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public override void Configure()
    {
        Get("/host/catalog/billers");
        Policies("PlatformAdmin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var countryCode = Query<string?>("countryCode", isRequired: false);
        var categoryId = Query<Guid?>("categoryId", isRequired: false);
        var search = Query<string?>("search", isRequired: false);
        var page = Query<int?>("page", isRequired: false) ?? 1;
        var pageSize = Query<int?>("pageSize", isRequired: false) ?? 20;
        
        var request = new Application.Models.Catalog.CatalogBillerListRequest(
            countryCode,
            categoryId,
            search,
            page,
            pageSize);

        var result = await _catalogService.GetBillersAsync(request, ct);

        var response = new CatalogBillerResponse(
            result.Billers.Select(biller => new CatalogBillerSummaryItemResponse(
                biller.BillerId,
                biller.Name,
                biller.LogoUrl,
                biller.CountryCode,
                biller.CategoryId,
                biller.CorrespondentPartnerId,
                biller.IsActive,
                biller.IsFeatured)).ToList(),
            new CatalogPaginationMetadataResponse(
                result.Pagination.Page,
                result.Pagination.PageSize,
                result.Pagination.TotalCount,
                result.Pagination.TotalPages));

        await Send.OkAsync(response, ct);
    }
}
