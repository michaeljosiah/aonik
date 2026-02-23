using Aonik.Finance.Contracts.Models.Ledger;

namespace Aonik.Finance.Contracts.Services.Ledger;

public interface ILedgerService
{
    Task<LedgerResponse> CreateLedgerAsync(CreateLedgerRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LedgerResponse>> ListLedgersAsync(CancellationToken cancellationToken = default);
    Task<LedgerAccountResponse> CreateAccountAsync(CreateLedgerAccountRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LedgerAccountResponse>> ListAccountsAsync(ListLedgerAccountsRequest request, CancellationToken cancellationToken = default);
    Task<JournalEntryResponse> AddJournalEntryAsync(AddJournalEntryRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JournalEntryResponse>> ListJournalEntriesAsync(ListJournalEntriesRequest request, CancellationToken cancellationToken = default);
}
