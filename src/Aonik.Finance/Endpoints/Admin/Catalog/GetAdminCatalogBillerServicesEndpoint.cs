using FastEndpoints;
using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Contracts.Services.Catalog;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Admin.Catalog;

internal class GetAdminCatalogBillerServicesEndpoint : EndpointWithoutRequest<CatalogBillerServiceResponse>
{
    private readonly ICatalogService _catalogService;

    public GetAdminCatalogBillerServicesEndpoint(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public override void Configure()
    {
        Get("/admin/catalog/billers/{billerId}/services");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "List biller services (admin)";
            s.Description = "Returns all services offered by a specific biller in the tenant admin context.";
            s.Response(200, "Success");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Product Catalog"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var billerId = Route<Guid>("billerId");
        var result = await _catalogService.GetBillerServicesAsync(billerId, ct);
        await Send.OkAsync(result, ct);
    }
}
