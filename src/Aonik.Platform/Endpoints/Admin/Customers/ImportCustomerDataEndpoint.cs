using System.Text.Json;
using Aonik.Platform.Contracts.Services.Customers;
using Aonik.SharedKernel.Abstractions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Customers;

internal class ImportCustomerDataRequest
{
    /// <summary>
    /// The customer data bundle JSON content.
    /// </summary>
    public CustomerDataBundle Bundle { get; set; } = null!;

    /// <summary>
    /// How to handle conflicts: "fail" (default) aborts if customer exists,
    /// "skip" skips conflicting entities.
    /// </summary>
    public string ConflictMode { get; set; } = "fail";
}

internal class ImportCustomerDataResponse
{
    public Guid NewPartyId { get; set; }
    public Dictionary<string, int> EntityCounts { get; set; } = new();
    public int TotalEntities { get; set; }
    public List<string> Warnings { get; set; } = new();
}

internal class ImportCustomerDataEndpoint : Endpoint<ImportCustomerDataRequest, ImportCustomerDataResponse>
{
    private readonly ICustomerDataService _customerDataService;

    public ImportCustomerDataEndpoint(ICustomerDataService customerDataService)
    {
        _customerDataService = customerDataService;
    }

    public override void Configure()
    {
        Post("/admin/customers/import");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Import customer data bundle";
            s.Description =
                "Imports a previously exported customer data bundle into the current tenant. " +
                "All entity IDs are regenerated and FK references remapped. " +
                "Use conflictMode=skip to skip entities that conflict with existing data.";
            s.Response(200, "Import completed");
            s.Response(400, "Invalid bundle or conflict");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Customer Administration"));
    }

    public override async Task HandleAsync(ImportCustomerDataRequest req, CancellationToken ct)
    {
        if (req.Bundle == null)
        {
            ThrowError("Bundle is required.");
            return;
        }

        try
        {
            var result = await _customerDataService.ImportAsync(req.Bundle, req.ConflictMode, ct);

            await Send.OkAsync(new ImportCustomerDataResponse
            {
                NewPartyId = result.NewPartyId,
                EntityCounts = result.EntityCounts,
                TotalEntities = result.EntityCounts.Values.Sum(),
                Warnings = result.Warnings,
            }, ct);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message);
        }
    }
}
