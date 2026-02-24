using FastEndpoints;
using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Contracts.Services.Catalog;

namespace Aonik.Finance.Endpoints.Admin.Catalog;

internal class GetAdminCatalogBillerServiceDetailEndpoint : EndpointWithoutRequest<CatalogBillerServiceDetailResponse>
{
    private readonly ICatalogService _catalogService;

    public GetAdminCatalogBillerServiceDetailEndpoint(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public override void Configure()
    {
        Get("/admin/catalog/billers/{billerId}/services/{serviceId}");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var billerId = Route<Guid>("billerId");
        var serviceId = Route<Guid>("serviceId");
        var result = await _catalogService.GetBillerServiceDetailAsync(billerId, serviceId, ct);
        if (result == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}
