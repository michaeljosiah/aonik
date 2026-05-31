using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Contracts.Services.Catalog;

namespace Aonik.Finance.Endpoints.Catalog;

internal class UpdateCatalogBillerEndpoint : Endpoint<UpdateCatalogBillerRequest, CatalogBillerDetailResponse>
{
    private readonly ICatalogService _catalogService;

    public UpdateCatalogBillerEndpoint(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public override void Configure()
    {
        Put("/catalog/billers/{billerId}");
        Policies("AdminUserWritePolicy");
        Summary(s =>
        {
            s.Summary = "Update biller";
            s.Description = "Updates a tenant-scoped biller. All body fields are optional.";
            s.Response(200, "Biller updated");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
            s.Response(403, "Caller lacks Catalog.Write");
            s.Response(404, "Biller, category or partner not found");
        });
        Options(x => x.WithTags("Product Catalog"));
    }

    public override async Task HandleAsync(UpdateCatalogBillerRequest req, CancellationToken ct)
    {
        var billerId = Route<Guid>("billerId");
        var result = await _catalogService.UpdateBillerAsync(billerId, req, ct);
        await Send.OkAsync(result, ct);
    }
}
