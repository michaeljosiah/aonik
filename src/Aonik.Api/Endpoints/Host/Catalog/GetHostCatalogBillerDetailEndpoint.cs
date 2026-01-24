using FastEndpoints;

using Aonik.Api.Contracts.Catalog;
using Aonik.Application.Services.Catalog;

namespace Aonik.Api.Endpoints.Host.Catalog;

public class GetHostCatalogBillerDetailEndpoint : EndpointWithoutRequest<CatalogBillerDetailResponse>
{
    private readonly ICatalogService _catalogService;

    public GetHostCatalogBillerDetailEndpoint(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public override void Configure()
    {
        Get("/host/catalog/billers/{billerId}");
        Policies("PlatformAdmin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var billerId = Route<Guid>("billerId");
        var result = await _catalogService.GetBillerDetailAsync(billerId, ct);
        if (result == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var response = new CatalogBillerDetailResponse(
            result.BillerId,
            result.Name,
            result.Description,
            result.LogoUrl,
            result.BannerUrl,
            result.SupportPhone,
            result.SupportEmail,
            result.CountryCode,
            result.CategoryId,
            result.CorrespondentPartnerId,
            result.IsActive,
            result.ServiceCount);

        await Send.OkAsync(response, ct);
    }
}
