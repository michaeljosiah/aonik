using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Contracts.Services.Catalog;

namespace Aonik.Finance.Endpoints.Public.Catalog;

internal class GetPublicCatalogBillerServicesEndpoint : EndpointWithoutRequest<CatalogBillerServiceResponse>
{
    private readonly IPublicCatalogService _catalogService;

    public GetPublicCatalogBillerServicesEndpoint(IPublicCatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public override void Configure()
    {
        Get("/public/catalog/billers/{billerId}/services");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tenantHeader = HttpContext.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(tenantHeader) || !Guid.TryParse(tenantHeader, out _))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "X-Tenant-Id header is required." }, ct);
            return;
        }

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
