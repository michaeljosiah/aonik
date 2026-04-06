using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Contracts.Services.Catalog;

namespace Aonik.Finance.Endpoints.Public.Catalog;

internal class ValidatePublicCatalogServiceFieldsEndpoint : Endpoint<CatalogServiceFieldValidationRequest, CatalogServiceFieldValidationResult>
{
    private readonly IPublicCatalogService _catalogService;

    public ValidatePublicCatalogServiceFieldsEndpoint(IPublicCatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public override void Configure()
    {
        Post("/public/catalog/billers/{billerId}/services/{serviceId}/validate");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Validate service fields (public)";
            s.Description = "Validates customer-supplied field values for a biller service before order submission. No authentication required.";
            s.Response(200, "Success");
            s.Response(400, "Validation failed");
            s.Response(404, "Biller service not found");
        });
        Options(x => x.WithTags("Product Catalog"));
    }

    public override async Task HandleAsync(CatalogServiceFieldValidationRequest req, CancellationToken ct)
    {
        var tenantHeader = HttpContext.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(tenantHeader) || !Guid.TryParse(tenantHeader, out _))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "X-Tenant-Id header is required." }, ct);
            return;
        }

        var billerId = Route<Guid>("billerId");
        var serviceId = Route<Guid>("serviceId");
        if (billerId == Guid.Empty || serviceId == Guid.Empty)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "billerId and serviceId must be valid UUIDs." }, ct);
            return;
        }

        var result = await _catalogService.ValidateServiceFieldsAsync(billerId, serviceId, req, ct);
        if (result == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}
