using FastEndpoints;
using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Contracts.Services.Catalog;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Host.Catalog;

internal class GetHostCatalogBillerServiceDetailEndpoint : EndpointWithoutRequest<CatalogBillerServiceDetailResponse>
{
    private readonly ICatalogService _catalogService;

    public GetHostCatalogBillerServiceDetailEndpoint(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public override void Configure()
    {
        Get("/host/catalog/billers/{billerId}/services/{serviceId}");
        Policies("PlatformAdmin");
        Summary(s =>
        {
            s.Summary = "Get biller service detail (host admin)";
            s.Description = "Returns full detail for a specific biller service in the platform host context.";
            s.Response(200, "Success");
            s.Response(401, "Not authenticated");
            s.Response(404, "Biller service not found");
        });
        Options(x => x.WithTags("Product Catalog"));
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
