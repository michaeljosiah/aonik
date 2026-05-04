using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Contracts.Services.Catalog;

namespace Aonik.Finance.Endpoints.Catalog;

internal class CreateCatalogBillerEndpoint : Endpoint<CreateCatalogBillerRequest, CatalogBillerDetailResponse>
{
    private readonly ICatalogService _catalogService;

    public CreateCatalogBillerEndpoint(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public override void Configure()
    {
        Post("/catalog/billers");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "Create biller";
            s.Description = "Creates a tenant-scoped biller. If CorrespondentPartnerId is omitted, the tenant's first existing partner is used (or a default 'Self' partner is lazily provisioned).";
            s.Response(201, "Biller created");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
            s.Response(403, "Caller lacks Catalog.Write");
            s.Response(404, "Category or partner not found");
        });
        Options(x => x.WithTags("Product Catalog"));
    }

    public override async Task HandleAsync(CreateCatalogBillerRequest req, CancellationToken ct)
    {
        var result = await _catalogService.CreateBillerAsync(req, ct);
        await Send.CreatedAtAsync<GetCatalogBillerDetailEndpoint>(
            routeValues: new { billerId = result.BillerId },
            responseBody: result,
            generateAbsoluteUrl: false,
            cancellation: ct);
    }
}
