using FastEndpoints;
using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Contracts.Services.Catalog;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "List billers (admin)";
            s.Description = "Returns a paginated list of billers for the current tenant in the tenant admin context.";
            s.Response(200, "Success");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Product Catalog"));
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
