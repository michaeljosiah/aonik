using FastEndpoints;
using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Contracts.Services.Catalog;

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
