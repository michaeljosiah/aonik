using Aonik.Api.Contracts.Ledger;
using Aonik.Application.Services.Ledger;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Ledger;

public class CreateLedgerAccountEndpoint : Endpoint<CreateLedgerAccountRequest, LedgerAccountResponse>
{
    private readonly ILedgerService _ledgerService;

    public CreateLedgerAccountEndpoint(ILedgerService ledgerService)
    {
        _ledgerService = ledgerService;
    }

    public override void Configure()
    {
        Post("/ledger/accounts");
        Policies("Ledger.Write");
    }

    public override async Task HandleAsync(CreateLedgerAccountRequest req, CancellationToken ct)
    {
        var appRequest = new Application.Models.Ledger.CreateLedgerAccountRequest(req.Name, req.Currency);
        var result = await _ledgerService.CreateAccountAsync(appRequest, ct);

        var response = new LedgerAccountResponse(result.Id, result.Name, result.Currency, result.CreatedUtc);

        await Send.CreatedAtAsync<CreateLedgerAccountEndpoint>(
            routeValues: new { id = response.Id },
            responseBody: response,
            cancellation: ct);
    }
}
