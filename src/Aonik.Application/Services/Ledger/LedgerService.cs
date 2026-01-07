using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.Ledger;
using Aonik.Domain.Ledger.Entities;

namespace Aonik.Application.Services.Ledger;

public class LedgerService : ILedgerService
{
    private readonly IAonikDbContext _dbContext;

    public LedgerService(IAonikDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<LedgerAccountResponse> CreateAccountAsync(CreateLedgerAccountRequest request, CancellationToken cancellationToken = default)
    {
        var account = new LedgerAccount(request.Name, request.Currency);

        _dbContext.LedgerAccounts.Add(account);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new LedgerAccountResponse(
            account.Id,
            account.Name,
            account.Currency,
            account.CreatedUtc);
    }

    public async Task<JournalEntryResponse> AddJournalEntryAsync(AddJournalEntryRequest request, CancellationToken cancellationToken = default)
    {
        var entry = new JournalEntry(
            request.AccountId,
            request.Amount,
            request.Currency,
            request.Reference,
            request.Description);

        _dbContext.JournalEntries.Add(entry);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new JournalEntryResponse(
            entry.Id,
            entry.AccountId,
            entry.Amount,
            entry.Currency,
            entry.EntryUtc,
            entry.Reference,
            entry.Description);
    }
}
