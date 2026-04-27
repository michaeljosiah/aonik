using Aonik.Finance.Contracts.Api.Ledger;
using Aonik.Finance.Contracts.Services.Ledger;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Ledger;

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
        Summary(s =>
        {
            s.Summary = "List ledger accounts";
            s.Description = "Returns all accounts for a given ledger, including account type and currency.";
            s.Response(200, "Ledger accounts retrieved successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Ledger"));
    }

    public override async Task HandleAsync(ListLedgerAccountsRequest req, CancellationToken ct)
    {
        var appRequest = new Contracts.Models.Ledger.ListLedgerAccountsRequest(req.LedgerId);
        var result = await _ledgerService.ListAccountsAsync(appRequest, ct);
        var response = result.Select(account => new LedgerAccountResponse(
            account.Id,
            account.LedgerId,
            account.Name,
            account.Code,
            account.AccountType,
            account.Currency,
            account.CreatedUtc,
            account.BalancesByCurrency
                .Select(b => new LedgerAccountBalance(b.Currency, b.Balance))
                .ToList())).ToList();

        await Send.OkAsync(response, ct);
    }
}
