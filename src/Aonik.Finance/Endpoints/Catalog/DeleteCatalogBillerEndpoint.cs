using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Aonik.Finance.Contracts.Services.Catalog;

namespace Aonik.Finance.Endpoints.Catalog;

internal class DeleteCatalogBillerEndpoint : EndpointWithoutRequest
{
    private readonly ICatalogService _catalogService;

    public DeleteCatalogBillerEndpoint(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public override void Configure()
    {
        Delete("/catalog/billers/{billerId}");
        Policies("AdminUserWritePolicy");
        Summary(s =>
        {
            s.Summary = "Delete biller";
            s.Description = "Soft-deletes a tenant-scoped biller (and its services inherit the soft-delete state via the EF query filter).";
            s.Response(204, "Biller deleted");
            s.Response(401, "Not authenticated");
            s.Response(403, "Caller lacks Catalog.Write");
            s.Response(404, "Biller not found");
        });
        Options(x => x.WithTags("Product Catalog"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var billerId = Route<Guid>("billerId");
        await _catalogService.DeleteBillerAsync(billerId, ct);
        await Send.NoContentAsync(ct);
    }
}
