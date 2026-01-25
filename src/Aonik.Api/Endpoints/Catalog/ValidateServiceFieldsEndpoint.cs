using Aonik.Api.Contracts.Catalog;
using Aonik.Application.Services.Catalog;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Catalog;

public class ValidateServiceFieldsEndpoint : Endpoint<CatalogServiceFieldValidationRequest, CatalogServiceFieldValidationResponse>
{
    private readonly ICatalogService _catalogService;

    public ValidateServiceFieldsEndpoint(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public override void Configure()
    {
        Post("/catalog/billers/{billerId:guid}/services/{serviceId:guid}/validate");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(CatalogServiceFieldValidationRequest req, CancellationToken ct)
    {
        var billerId = Route<Guid>("billerId");
        var serviceId = Route<Guid>("serviceId");

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
