using FastEndpoints;
using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Contracts.Services.Catalog;

namespace Aonik.Finance.Endpoints.Admin.Catalog;

internal class GetAdminCatalogBillersEndpoint : EndpointWithoutRequest<CatalogBillerResponse>
{
    private readonly ICatalogService _catalogService;

    public GetAdminCatalogBillersEndpoint(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public override void Configure()
    {
        Get("/admin/catalog/billers");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var countryCode = Query<string?>("countryCode", isRequired: false);
        var categoryId = Query<Guid?>("categoryId", isRequired: false);
        var search = Query<string?>("search", isRequired: false);
        var page = Query<int?>("page", isRequired: false) ?? 1;
        var pageSize = Query<int?>("pageSize", isRequired: false) ?? 20;

        var request = new CatalogBillerListRequest(countryCode, categoryId, search, page, pageSize);
        var result = await _catalogService.GetBillersAsync(request, ct);
        await Send.OkAsync(result, ct);
    }
}
