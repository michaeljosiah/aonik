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

public record ListJournalEntriesRequest(Guid? LedgerId);

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
    DateTime CreatedUtc);

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
