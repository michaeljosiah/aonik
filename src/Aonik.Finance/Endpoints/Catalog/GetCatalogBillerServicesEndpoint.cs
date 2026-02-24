using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Contracts.Services.Catalog;

namespace Aonik.Finance.Endpoints.Catalog;

internal class GetCatalogBillerServicesEndpoint : EndpointWithoutRequest<CatalogBillerServiceResponse>
{
    private readonly ICatalogService _catalogService;

    public GetCatalogBillerServicesEndpoint(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public override void Configure()
    {
        Get("/catalog/billers/{billerId}/services");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var billerId = Route<Guid>("billerId");
        if (billerId == Guid.Empty)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "billerId must be a valid UUID." }, ct);
            return;
        }

        var result = await _catalogService.GetBillerServicesAsync(billerId, ct);
        await Send.OkAsync(result, ct);
    }
}
