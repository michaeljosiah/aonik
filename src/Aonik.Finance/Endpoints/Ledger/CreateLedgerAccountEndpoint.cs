using Aonik.Finance.Contracts.Api.Ledger;
using Aonik.Finance.Contracts.Services.Ledger;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.Ledger;

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
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(CreateLedgerAccountRequest req, CancellationToken ct)
    {
        var appRequest = new Contracts.Models.Ledger.CreateLedgerAccountRequest(
            req.LedgerId,
            req.Name,
            req.Code,
            req.AccountType);
        var result = await _ledgerService.CreateAccountAsync(appRequest, ct);

        var response = new LedgerAccountResponse(
            result.Id,
            result.LedgerId,
            result.Name,
            result.Code,
            result.AccountType,
            result.Currency,
            result.CreatedUtc);

        await Send.CreatedAtAsync<CreateLedgerAccountEndpoint>(
            routeValues: new { id = response.Id },
            responseBody: response,
            cancellation: ct);
    }
}
