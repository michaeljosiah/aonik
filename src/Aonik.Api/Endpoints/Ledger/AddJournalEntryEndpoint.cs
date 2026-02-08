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
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(AddJournalEntryRequest req, CancellationToken ct)
    {
        var appRequest = new Application.Models.Ledger.AddJournalEntryRequest(
            req.LedgerId,
            req.Reference,
            req.Description,
            req.Lines.Select(line => new Application.Models.Ledger.AddJournalEntryLineRequest(
                line.AccountId,
                line.Direction,
                line.Amount,
                line.Currency,
                line.Narration)).ToList());

        var result = await _ledgerService.AddJournalEntryAsync(appRequest, ct);

        var response = new JournalEntryResponse(
            result.Id,
            result.LedgerId,
            result.EntryUtc,
            result.Status,
            result.Reference,
            result.Description,
            result.Lines.Select(line => new JournalEntryLineResponse(
                line.Id,
                line.AccountId,
                line.Direction,
                line.Amount,
                line.Currency,
                line.Narration)).ToList());

        await Send.CreatedAtAsync<AddJournalEntryEndpoint>(
            routeValues: new { id = response.Id },
            responseBody: response,
            cancellation: ct);
    }
}
