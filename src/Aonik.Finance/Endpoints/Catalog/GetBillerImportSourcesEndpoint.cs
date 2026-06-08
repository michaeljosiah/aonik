using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Contracts.Services.Catalog;

namespace Aonik.Finance.Endpoints.Catalog;

internal class GetBillerImportSourcesEndpoint : EndpointWithoutRequest<BillerImportSourcesResponse>
{
    private readonly IBillerImportService _importService;

    public GetBillerImportSourcesEndpoint(IBillerImportService importService)
    {
        _importService = importService;
    }

    public override void Configure()
    {
        Get("/catalog/billers/import/sources");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "List import sources";
            s.Description = "Returns the current tenant's configured partner connectors that can supply a biller catalogue (the import wizard's Source step).";
            s.Response(200, "Available connectors");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Product Catalog"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _importService.GetSourcesAsync(ct);
        await Send.OkAsync(result, ct);
    }
}
