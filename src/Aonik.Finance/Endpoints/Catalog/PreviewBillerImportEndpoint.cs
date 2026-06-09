using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Contracts.Services.Catalog;

namespace Aonik.Finance.Endpoints.Catalog;

internal class PreviewBillerImportEndpoint : Endpoint<BillerImportPreviewRequest, BillerImportPreviewResponse>
{
    private readonly IBillerImportService _importService;

    public PreviewBillerImportEndpoint(IBillerImportService importService)
    {
        _importService = importService;
    }

    public override void Configure()
    {
        Post("/catalog/billers/import/preview");
        Policies("AdminUserWritePolicy");
        Summary(s =>
        {
            s.Summary = "Preview partner biller import";
            s.Description = "Reads the live biller catalogue from a configured partner connector and tags each biller New / Mapped / Changed against what is already imported. Persists nothing. Catalog.Write.";
            s.Response(200, "Catalogue preview");
            s.Response(401, "Not authenticated");
            s.Response(403, "Caller lacks Catalog.Write");
            s.Response(404, "Connector not found");
        });
        Options(x => x.WithTags("Product Catalog"));
    }

    public override async Task HandleAsync(BillerImportPreviewRequest req, CancellationToken ct)
    {
        var result = await _importService.PreviewAsync(req, ct);
        await Send.OkAsync(result, ct);
    }
}
