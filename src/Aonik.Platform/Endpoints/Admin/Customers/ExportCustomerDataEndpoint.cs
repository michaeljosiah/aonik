using System.Text.Json;
using Aonik.Platform.Contracts.Services.Customers;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Customers;

internal class ExportCustomerDataEndpoint : EndpointWithoutRequest
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly ICustomerDataService _customerDataService;

    public ExportCustomerDataEndpoint(ICustomerDataService customerDataService)
    {
        _customerDataService = customerDataService;
    }

    public override void Configure()
    {
        Get("/admin/customers/{partyId}/export");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Export customer data bundle";
            s.Description =
                "Exports the full data graph for a customer as a downloadable JSON bundle. " +
                "Includes party, profile, accounts, transactions, budgets, bills, and all " +
                "personal finance entities. Sensitive fields are redacted.";
            s.Response(200, "JSON file download");
            s.Response(401, "Not authenticated");
            s.Response(404, "Customer not found");
        });
        Options(x => x.WithTags("Customer Administration"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var partyId = Route<Guid>("partyId");

        var bundle = await _customerDataService.ExportAsync(partyId, ct);

        if (bundle == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var json = JsonSerializer.SerializeToUtf8Bytes(bundle, JsonOptions);
        var timestamp = bundle.ExportedAt.ToString("yyyyMMdd-HHmmss");
        var fileName = $"customer-export-{partyId:N}-{timestamp}.json";

        HttpContext.Response.ContentType = "application/json";
        HttpContext.Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{fileName}\"");
        HttpContext.Response.ContentLength = json.Length;

        await HttpContext.Response.Body.WriteAsync(json, ct);
    }
}
