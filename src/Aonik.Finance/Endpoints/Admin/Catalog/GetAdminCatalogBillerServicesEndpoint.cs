using FastEndpoints;
using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Contracts.Services.Catalog;

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
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var billerId = Route<Guid>("billerId");
        var result = await _catalogService.GetBillerServicesAsync(billerId, ct);
        await Send.OkAsync(result, ct);
    }
}
