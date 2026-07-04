namespace Aonik.Finance.Contracts.Api.Ledger;

public record CreateLedgerRequest(string BaseCurrency);

public record CreateLedgerAccountRequest(
    Guid LedgerId,
    string Name,
    string Code,
    string AccountType);

public record AddJournalEntryLineRequest(
    Guid AccountId,
    string Direction,
    decimal Amount,
    string Currency,
    string? Narration);

public record AddJournalEntryRequest(
    Guid LedgerId,
    string? Reference,
    string? Description,
    List<AddJournalEntryLineRequest> Lines);

public record ListLedgerAccountsRequest(Guid? LedgerId);

// Note: FastEndpoints binds this positional record via its constructor, passing
// default(int) (0) for any paging query param the client omits — the `= 1/= 200`
// initializers are not applied at the binding boundary. That is safe because the
// service runs every value through FinancePaging.Normalize, which maps 0 -> page 1 /
// default page size. The initializers are kept only so the OpenAPI schema advertises
// the effective defaults.
public record ListJournalEntriesRequest(Guid? LedgerId, int PageNumber = 1, int PageSize = 200);

public record LedgerResponse(
    Guid Id,
    string BaseCurrency,
    DateTime CreatedUtc);

public record LedgerAccountResponse(
    Guid Id,
    Guid LedgerId,
    string Name,
    string Code,
    string AccountType,
    string Currency,
    DateTime CreatedUtc,
    IReadOnlyList<LedgerAccountBalance> BalancesByCurrency);

public record LedgerAccountBalance(
    string Currency,
    decimal Balance);

public record JournalEntryLineResponse(
    Guid Id,
    Guid AccountId,
    string Direction,
    decimal Amount,
    string Currency,
    string? Narration);

public record JournalEntryResponse(
    Guid Id,
    Guid LedgerId,
    DateTime EntryUtc,
    string Status,
    string? Reference,
    string? Description,
    IReadOnlyList<JournalEntryLineResponse> Lines);
