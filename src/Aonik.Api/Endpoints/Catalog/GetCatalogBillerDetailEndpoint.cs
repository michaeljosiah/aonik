using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Aonik.Api.Contracts.Catalog;
using Aonik.Application.Services.Catalog;

namespace Aonik.Api.Endpoints.Catalog;

public class GetCatalogBillerDetailEndpoint : EndpointWithoutRequest<CatalogBillerDetailResponse>
{
    private readonly ICatalogService _catalogService;

    public GetCatalogBillerDetailEndpoint(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public override void Configure()
    {
        Get("/catalog/billers/{billerId}");
        Policies("Catalog.Read");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var billerId = Route<Guid>("billerId");
        if (billerId == Guid.Empty)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "billerId must be a valid UUID." }, ct);
            return;
        }

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
