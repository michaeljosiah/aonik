using FastEndpoints;
using Microsoft.AspNetCore.Http;

using Aonik.Api.Contracts.Catalog;
using Aonik.Application.Services.Catalog;

namespace Aonik.Api.Endpoints.Public.Catalog;

public class ValidatePublicCatalogServiceFieldsEndpoint : Endpoint<CatalogServiceFieldValidationRequest, CatalogServiceFieldValidationResponse>
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

        var appRequest = new Application.Models.Catalog.CatalogServiceFieldValidationRequest(req.FieldValues);
        var result = await _catalogService.ValidateServiceFieldsAsync(billerId, serviceId, appRequest, ct);
        if (result == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var response = new CatalogServiceFieldValidationResponse(
            result.IsValid,
            result.ValidatedAt,
            result.ErrorCode,
            result.ErrorMessage,
            result.AccountHolderName,
            result.AdditionalInfo);

        await Send.OkAsync(response, ct);
    }
}
