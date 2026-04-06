using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Contracts.Services.Catalog;

namespace Aonik.Finance.Endpoints.Catalog;

internal class GetCatalogBillerServiceDetailEndpoint : EndpointWithoutRequest<CatalogBillerServiceDetailResponse>
{
    private readonly ICatalogService _catalogService;

    public GetCatalogBillerServiceDetailEndpoint(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public override void Configure()
    {
        Get("/catalog/billers/{billerId}/services/{serviceId}");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "Get biller service detail";
            s.Description = "Returns full detail for a specific service offered by a biller, including field definitions.";
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
        if (billerId == Guid.Empty || serviceId == Guid.Empty)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "billerId and serviceId must be valid UUIDs." }, ct);
            return;
        }

        var result = await _catalogService.GetBillerServiceDetailAsync(billerId, serviceId, ct);
        if (result == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}
