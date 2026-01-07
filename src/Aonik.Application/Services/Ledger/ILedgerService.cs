using Aonik.Application.Models.Ledger;
using Aonik.Domain.Ledger.Entities;

namespace Aonik.Application.Services.Ledger;

public interface ILedgerService
{
    Task<LedgerAccountResponse> CreateAccountAsync(CreateLedgerAccountRequest request, CancellationToken cancellationToken = default);
    Task<JournalEntryResponse> AddJournalEntryAsync(AddJournalEntryRequest request, CancellationToken cancellationToken = default);
}
