using FastEndpoints;

using Aonik.Api.Contracts.Catalog;
using Aonik.Application.Services.Catalog;

namespace Aonik.Api.Endpoints.Admin.Catalog;

public class GetAdminCatalogBillerServicesEndpoint : EndpointWithoutRequest<CatalogBillerServiceResponse>
{
    private readonly ICatalogService _catalogService;

    public GetAdminCatalogBillerServicesEndpoint(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public override void Configure()
    {
        Get("/admin/catalog/billers/{billerId}/services");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var billerId = Route<Guid>("billerId");
        var result = await _catalogService.GetBillerServicesAsync(billerId, ct);

        var response = new CatalogBillerServiceResponse(
            result.Services.Select(service => new CatalogBillerServiceItemResponse(
                service.ServiceId,
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
