using FastEndpoints;
using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Contracts.Services.Catalog;

namespace Aonik.Finance.Endpoints.Host.Catalog;

internal class GetHostCatalogBillerDetailEndpoint : EndpointWithoutRequest<CatalogBillerDetailResponse>
{
    private readonly ICatalogService _catalogService;

    public GetHostCatalogBillerDetailEndpoint(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public override void Configure()
    {
        Get("/host/catalog/billers/{billerId}");
        Policies("PlatformAdmin");
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
