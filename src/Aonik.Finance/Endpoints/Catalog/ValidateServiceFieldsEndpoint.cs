using FastEndpoints;
using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Contracts.Services.Catalog;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Catalog;

internal class ValidateServiceFieldsEndpoint : Endpoint<CatalogServiceFieldValidationRequest, CatalogServiceFieldValidationResult>
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
        Summary(s =>
        {
            s.Summary = "Validate service fields";
            s.Description = "Validates customer-supplied field values for a biller service before order submission.";
            s.Response(200, "Success");
            s.Response(400, "Validation failed");
            s.Response(401, "Not authenticated");
            s.Response(404, "Biller service not found");
        });
        Options(x => x.WithTags("Product Catalog"));
    }

    public override async Task HandleAsync(CatalogServiceFieldValidationRequest req, CancellationToken ct)
    {
        var billerId = Route<Guid>("billerId");
        var serviceId = Route<Guid>("serviceId");

        var result = await _catalogService.ValidateServiceFieldsAsync(billerId, serviceId, req, ct);
        if (result == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}
