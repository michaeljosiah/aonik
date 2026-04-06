using Aonik.Finance.Contracts.Api.Ledger;
using Aonik.Finance.Contracts.Services.Ledger;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Ledger;

public class CreateLedgerEndpoint : Endpoint<CreateLedgerRequest, LedgerResponse>
{
    private readonly ILedgerService _ledgerService;

    public CreateLedgerEndpoint(ILedgerService ledgerService)
    {
        _ledgerService = ledgerService;
    }

    public override void Configure()
    {
        Post("/ledger");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "Create a new ledger";
            s.Description = "Creates a new ledger with the specified base currency.";
            s.Response(201, "Ledger created successfully");
            s.Response(400, "Invalid request data");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Ledger"));
    }

    public override async Task HandleAsync(CreateLedgerRequest req, CancellationToken ct)
    {
        var appRequest = new Contracts.Models.Ledger.CreateLedgerRequest(req.BaseCurrency);
        var result = await _ledgerService.CreateLedgerAsync(appRequest, ct);

        var response = new LedgerResponse(result.Id, result.BaseCurrency, result.CreatedUtc);

        await Send.CreatedAtAsync<CreateLedgerEndpoint>(
            routeValues: new { id = response.Id },
            responseBody: response,
            cancellation: ct);
    }
}
