using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Contracts.Services.Catalog;

namespace Aonik.Finance.Endpoints.Catalog;

internal class ImportBillersEndpoint : Endpoint<BillerImportRequest, BillerImportSummaryResponse>
{
    private readonly IBillerImportService _importService;

    public ImportBillersEndpoint(IBillerImportService importService)
    {
        _importService = importService;
    }

    public override void Configure()
    {
        Post("/catalog/billers/import");
        Policies("AdminUserWritePolicy");
        Summary(s =>
        {
            s.Summary = "Import billers from a partner";
            s.Description = "Idempotent upsert of the selected billers, services, and connector mappings. Re-running creates no duplicates, refreshes changed fields, and soft-deactivates dropped services. Catalog.Write.";
            s.Response(200, "Import summary (created / updated / deactivated counts)");
            s.Response(401, "Not authenticated");
            s.Response(403, "Caller lacks Catalog.Write");
            s.Response(404, "Connector not found");
        });
        Options(x => x.WithTags("Product Catalog"));
    }

    public override async Task HandleAsync(BillerImportRequest req, CancellationToken ct)
    {
        var result = await _importService.ImportAsync(req, ct);
        await Send.OkAsync(result, ct);
    }
}
