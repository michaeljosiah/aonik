namespace Aonik.Finance.Contracts.Models.Ledger;

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
    List<AddJournalEntryLineRequest> Lines,
    /// <summary>
    /// Originating business event that caused this entry to be posted
    /// (e.g. "PaymentCaptured", "InvoicePaid"). Null/blank means a manual
    /// entry with no upstream source. A non-manual source must be paired
    /// with a non-empty <see cref="SourceId"/> and is enforced to post at
    /// most once per (tenant, source type, source id) for idempotency.
    /// </summary>
    string? SourceType = null,
    /// <summary>
    /// Identifier of the originating business event (e.g. the payment or
    /// invoice id). Required when <see cref="SourceType"/> is non-manual.
    /// </summary>
    Guid? SourceId = null);

public record ListLedgerAccountsRequest(Guid? LedgerId);

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
    /// <summary>
    /// Running balance per currency, computed from posted JournalEntryLines.
    /// Sign follows accounting convention for the account type:
    /// Asset / Expense → debit positive; Liability / Equity / Income → credit positive.
    /// Empty when the account has no lines yet.
    /// </summary>
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
