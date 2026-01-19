using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Aonik.Api.Contracts.Catalog;
using Aonik.Application.Services.Catalog;

namespace Aonik.Api.Endpoints.Catalog;

public class GetCatalogBillerServiceDetailEndpoint : EndpointWithoutRequest<CatalogBillerServiceDetailResponse>
{
    private readonly ICatalogService _catalogService;

    public GetCatalogBillerServiceDetailEndpoint(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public override void Configure()
    {
        Get("/catalog/billers/{billerId}/services/{serviceId}");
        Policies("Catalog.Read");
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

        var response = new CatalogBillerServiceDetailResponse(
            result.ServiceId,
            result.Name,
            result.Type,
            result.Currency,
            result.MinAmount,
            result.MaxAmount,
            result.SupportsPartialPayment,
            result.RequiresValidation,
            result.Fields.Select(field => new CatalogServiceFieldResponse(
                field.Key,
                field.Label,
                field.FieldType,
                field.Required,
                field.MinLength,
                field.MaxLength,
                field.Mask,
                field.Placeholder,
                field.Options?.Select(option => new CatalogServiceFieldOptionResponse(
                    option.Value,
                    option.Label)).ToList())).ToList(),
            result.Validation == null
                ? null
                : new CatalogServiceValidationResponse(
                    result.Validation.ValidationEndpoint,
                    result.Validation.ValidationMode));

        await Send.OkAsync(response, ct);
    }
}
