using Aonik.Finance.Contracts.Api.Ledger;
using Aonik.Finance.Contracts.Services.Ledger;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.Ledger;

public class ListJournalEntriesEndpoint : Endpoint<ListJournalEntriesRequest, List<JournalEntryResponse>>
{
    private readonly ILedgerService _ledgerService;

    public ListJournalEntriesEndpoint(ILedgerService ledgerService)
    {
        _ledgerService = ledgerService;
    }

    public override void Configure()
    {
        Get("/ledger/journal-entries");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(ListJournalEntriesRequest req, CancellationToken ct)
    {
        var appRequest = new Contracts.Models.Ledger.ListJournalEntriesRequest(req.LedgerId);
        var result = await _ledgerService.ListJournalEntriesAsync(appRequest, ct);

        var response = result.Select(entry => new JournalEntryResponse(
            entry.Id,
            entry.LedgerId,
            entry.EntryUtc,
            entry.Status,
            entry.Reference,
            entry.Description,
            entry.Lines.Select(line => new JournalEntryLineResponse(
                line.Id,
                line.AccountId,
                line.Direction,
                line.Amount,
                line.Currency,
                line.Narration)).ToList())).ToList();

        await Send.OkAsync(response, ct);
    }
}
