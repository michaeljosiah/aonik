using FastEndpoints;
using Microsoft.AspNetCore.Http;

using Aonik.Api.Contracts.Catalog;
using Aonik.Api.Endpoints.Catalog;
using Aonik.Application.Services.Catalog;

namespace Aonik.Api.Endpoints.Public.Catalog;

public class GetPublicCatalogBillersEndpoint : Endpoint<CatalogBillerListRequest, CatalogBillerResponse>
{
    private readonly IPublicCatalogService _catalogService;

    public GetPublicCatalogBillersEndpoint(IPublicCatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public override void Configure()
    {
        Get("/public/catalog/billers");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CatalogBillerListRequest req, CancellationToken ct)
    {
        var tenantHeader = HttpContext.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(tenantHeader) || !Guid.TryParse(tenantHeader, out _))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "X-Tenant-Id header is required." }, ct);
            return;
        }

        var countryCode = CatalogValidation.NormalizeCountryCode(req.CountryCode);
        if (req.CountryCode != null && countryCode == null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "countryCode must be ISO-3166-1 alpha-2." }, ct);
            return;
        }

        var search = CatalogValidation.NormalizeSearch(req.Search);
        if (req.Search != null && search == null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "search must be 100 characters or fewer." }, ct);
            return;
        }

        if (req.Page < 1 || req.PageSize < 1 || req.PageSize > 100)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "page must be >= 1 and pageSize must be between 1 and 100." }, ct);
            return;
        }

        if (req.CategoryId.HasValue && req.CategoryId.Value == Guid.Empty)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "categoryId must be a valid UUID." }, ct);
            return;
        }

        var appRequest = new Application.Models.Catalog.CatalogBillerListRequest(
            countryCode,
            req.CategoryId,
            search,
            req.Page,
            req.PageSize);

        var result = await _catalogService.GetBillersAsync(appRequest, ct);

        var response = new CatalogBillerResponse(
            result.Billers.Select(biller => new CatalogBillerSummaryItemResponse(
                biller.BillerId,
                biller.Name,
                biller.LogoUrl,
                biller.CountryCode,
                biller.CategoryId,
                biller.CorrespondentPartnerId,
                biller.IsActive,
                biller.IsFeatured)).ToList(),
            new CatalogPaginationMetadataResponse(
                result.Pagination.Page,
                result.Pagination.PageSize,
                result.Pagination.TotalCount,
                result.Pagination.TotalPages));

        await Send.OkAsync(response, ct);
    }
}
