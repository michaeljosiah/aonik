using FastEndpoints;
using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Contracts.Services.Catalog;

namespace Aonik.Finance.Endpoints.Admin.Catalog;

internal class GetAdminCatalogBillerDetailEndpoint : EndpointWithoutRequest<CatalogBillerDetailResponse>
{
    private readonly ICatalogService _catalogService;

    public GetAdminCatalogBillerDetailEndpoint(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public override void Configure()
    {
        Get("/admin/catalog/billers/{billerId}");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var billerId = Route<Guid>("billerId");
        var result = await _catalogService.GetBillerDetailAsync(billerId, ct);
        if (result == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}
