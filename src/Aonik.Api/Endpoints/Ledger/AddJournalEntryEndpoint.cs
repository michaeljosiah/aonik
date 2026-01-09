using Aonik.Api.Contracts.Ledger;
using Aonik.Application.Services.Ledger;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Ledger;

public class AddJournalEntryEndpoint : Endpoint<AddJournalEntryRequest, JournalEntryResponse>
{
    private readonly ILedgerService _ledgerService;

    public AddJournalEntryEndpoint(ILedgerService ledgerService)
    {
        _ledgerService = ledgerService;
    }

    public override void Configure()
    {
        Post("/ledger/journal-entries");
        Policies("Ledger.Write");
    }

    public override async Task HandleAsync(AddJournalEntryRequest req, CancellationToken ct)
    {
        var appRequest = new Application.Models.Ledger.AddJournalEntryRequest(
            req.AccountId,
            req.Amount,
            req.Currency,
            req.Reference,
            req.Description);

        var result = await _ledgerService.AddJournalEntryAsync(appRequest, ct);

        var response = new JournalEntryResponse(
            result.Id,
            result.AccountId,
            result.Amount,
            result.Currency,
            result.EntryUtc,
            result.Reference,
            result.Description);

        await Send.CreatedAtAsync<AddJournalEntryEndpoint>(
            routeValues: new { id = response.Id },
            responseBody: response,
            cancellation: ct);
    }
}
