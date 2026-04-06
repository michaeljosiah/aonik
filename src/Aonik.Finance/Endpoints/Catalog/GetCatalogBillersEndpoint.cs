using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Contracts.Services.Catalog;

namespace Aonik.Finance.Endpoints.Catalog;

internal class GetCatalogBillersEndpoint : Endpoint<CatalogBillerListRequest, CatalogBillerResponse>
{
    private readonly ICatalogService _catalogService;

    public GetCatalogBillersEndpoint(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public override void Configure()
    {
        Get("/catalog/billers");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "List billers";
            s.Description = "Returns a paginated list of billers for the current tenant, with optional filtering by country, category, and search term.";
            s.Response(200, "Success");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Product Catalog"));
    }

    public override async Task HandleAsync(CatalogBillerListRequest req, CancellationToken ct)
    {
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

        var request = new CatalogBillerListRequest(countryCode, req.CategoryId, search, req.Page, req.PageSize);
        var result = await _catalogService.GetBillersAsync(request, ct);
        await Send.OkAsync(result, ct);
    }
}
