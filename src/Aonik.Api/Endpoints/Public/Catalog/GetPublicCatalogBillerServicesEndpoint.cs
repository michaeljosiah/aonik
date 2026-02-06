using FastEndpoints;
using Microsoft.AspNetCore.Http;

using Aonik.Api.Contracts.Catalog;
using Aonik.Application.Services.Catalog;

namespace Aonik.Api.Endpoints.Public.Catalog;

public class GetPublicCatalogBillerServicesEndpoint : EndpointWithoutRequest<CatalogBillerServiceResponse>
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
        var response = new CatalogBillerServiceResponse(
            result.Services.Select(service => new CatalogBillerServiceItemResponse(
                service.ServiceId,
                service.ServiceCode,
                service.Name,
                service.Type,
                service.Currency,
                service.MinAmount,
                service.MaxAmount,
                service.SupportsPartialPayment,
                service.RequiresValidation,
                service.IsActive)).ToList());

        await Send.OkAsync(response, ct);
    }
}
