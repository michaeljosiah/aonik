using FastEndpoints;

using Aonik.Api.Contracts.Catalog;
using Aonik.Application.Services.Catalog;

namespace Aonik.Api.Endpoints.Admin.Catalog;

public class GetAdminCatalogBillerDetailEndpoint : EndpointWithoutRequest<CatalogBillerDetailResponse>
{
    private readonly ICatalogService _catalogService;

    public GetAdminCatalogBillerDetailEndpoint(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public override void Configure()
    {
        Get("/admin/catalog/billers/{billerId}");
        Policies("AdminPolicy");
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
