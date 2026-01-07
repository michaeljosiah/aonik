namespace Aonik.Application.Models.Ledger;

public record CreateLedgerAccountRequest(string Name, string Currency);

public record AddJournalEntryRequest(
    Guid AccountId,
    decimal Amount,
    string Currency,
    string? Reference,
    string? Description);

public record LedgerAccountResponse(
    Guid Id,
    string Name,
    string Currency,
    DateTime CreatedUtc);

public record JournalEntryResponse(
    Guid Id,
    Guid AccountId,
    decimal Amount,
    string Currency,
    DateTime EntryUtc,
    string? Reference,
    string? Description);
