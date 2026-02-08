using Aonik.Api.Contracts.Ledger;
using Aonik.Application.Services.Ledger;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Ledger;

public class ListLedgerAccountsEndpoint : Endpoint<ListLedgerAccountsRequest, List<LedgerAccountResponse>>
{
    private readonly ILedgerService _ledgerService;

    public ListLedgerAccountsEndpoint(ILedgerService ledgerService)
    {
        _ledgerService = ledgerService;
    }

    public override void Configure()
    {
        Get("/ledger/accounts");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(ListLedgerAccountsRequest req, CancellationToken ct)
    {
        var appRequest = new Application.Models.Ledger.ListLedgerAccountsRequest(req.LedgerId);
        var result = await _ledgerService.ListAccountsAsync(appRequest, ct);
        var response = result.Select(account => new LedgerAccountResponse(
            account.Id,
            account.LedgerId,
            account.Name,
            account.Code,
            account.AccountType,
            account.Currency,
            account.CreatedUtc)).ToList();

        await Send.OkAsync(response, ct);
    }
}
