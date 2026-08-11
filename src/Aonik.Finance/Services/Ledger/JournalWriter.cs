using Aonik.Finance.Entities.Ledger;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ledgers;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.Ledger;

/// <summary>
/// Spec 088 §5 — the cross-module implementation of <see cref="IJournalWriter"/>.
///
/// Writes directly to <see cref="FinanceDbContext"/>, deliberately bypassing <c>ILedgerService</c>
/// and its <c>Ledger.Write</c> permission check, for the same reason
/// <see cref="LedgerPostingService"/> does: these posts are machine-driven consequences of an
/// event that already happened, not user-authored entries, and the operator who triggered the
/// event is not guaranteed to hold ledger-write rights.
/// </summary>
internal sealed class JournalWriter : IJournalWriter
{
    private readonly FinanceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;

    public JournalWriter(FinanceDbContext dbContext, ITenantProvider tenantProvider, IClock clock)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _clock = clock;
    }

    public async Task<JournalEntryRef> PostAsync(PostJournalCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        Validate(command);

        // The ledger must belong to THIS tenant. Without the check a caller could name any ledger
        // id it happened to hold and post across the tenant boundary.
        var ledger = await _dbContext.Ledgers
            .FirstOrDefaultAsync(l => l.Id == command.LedgerId && l.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException($"Ledger '{command.LedgerId}' was not found in this tenant.");

        // Idempotency: the filtered unique index on (TenantId, SourceType, SourceId) is the
        // authority, so a retried business event returns the original entry instead of posting a
        // second one. Checking first turns a constraint violation into a clean answer.
        var existing = await _dbContext.JournalEntries.AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.TenantId == tenantId
                     && e.SourceType == command.SourceType
                     && e.SourceId == command.SourceId,
                cancellationToken);

        if (existing is not null)
            return new JournalEntryRef(existing.Id, command.SourceType, command.SourceId, AlreadyExisted: true);

        var codes = command.Lines.Select(l => l.AccountCode).Distinct().ToList();

        // Codes are unique per LEDGER, not per tenant — resolving them anywhere else is exactly
        // the mistake the required LedgerId exists to prevent.
        var accounts = await _dbContext.LedgerAccounts.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.LedgerId == ledger.Id && codes.Contains(a.Code))
            .ToDictionaryAsync(a => a.Code, cancellationToken);

        var missing = codes.Where(c => !accounts.ContainsKey(c)).ToList();
        if (missing.Count > 0)
        {
            throw new NotFoundException(
                $"Account code(s) {string.Join(", ", missing)} do not exist in ledger '{ledger.Id}'.");
        }

        var entry = new JournalEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LedgerId = ledger.Id,
            Timestamp = command.TimestampUtc ?? _clock.UtcNow,
            SourceType = command.SourceType,
            SourceId = command.SourceId,
            Status = "Posted",
            Lines = command.Lines.Select(line => new JournalEntryLine
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                LedgerAccountId = accounts[line.AccountCode].Id,
                Direction = line.Direction,
                Amount = line.Amount,
                Currency = line.Currency,
                Narration = line.Narration,
                DimensionsJson = line.DimensionsJson ?? "{}"
            }).ToList()
        };

        _dbContext.JournalEntries.Add(entry);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new JournalEntryRef(entry.Id, command.SourceType, command.SourceId, AlreadyExisted: false);
    }

    private static void Validate(PostJournalCommand command)
    {
        if (command.Lines.Count == 0)
            throw new InvalidStateException("A journal entry must have at least one line.");

        if (string.IsNullOrWhiteSpace(command.SourceType))
            throw new InvalidStateException("SourceType is required — it is half of the idempotency key.");

        // "Manual" is excluded from the idempotency index (hand-authored entries all share
        // SourceId = Guid.Empty), so accepting it here would silently give up the guarantee this
        // contract advertises.
        if (string.Equals(command.SourceType, JournalDirections.ManualSourceType, StringComparison.Ordinal))
        {
            throw new InvalidStateException(
                "SourceType 'Manual' is reserved for hand-authored entries and is excluded from the "
                + "idempotency index. Use a source type that identifies the originating event.");
        }

        foreach (var line in command.Lines)
        {
            if (line.Direction is not (JournalDirections.Debit or JournalDirections.Credit))
                throw new InvalidStateException($"'{line.Direction}' is not a valid direction.");

            if (line.Amount <= 0)
            {
                // Direction carries the sign; a negative or zero amount is either a caller
                // reversing a leg the wrong way or a no-op line, and both are worth refusing.
                throw new InvalidStateException($"Line amounts must be positive; got {line.Amount}.");
            }
        }

        // A mixed-currency entry cannot balance in any meaningful sense — the totals would be
        // adding unlike units. Multi-currency posting needs FX legs and is a separate decision.
        var currencies = command.Lines.Select(l => l.Currency).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (currencies.Count > 1)
            throw new InvalidStateException($"An entry must be single-currency; got {string.Join(", ", currencies)}.");

        var debits = command.Lines.Where(l => l.Direction == JournalDirections.Debit).Sum(l => l.Amount);
        var credits = command.Lines.Where(l => l.Direction == JournalDirections.Credit).Sum(l => l.Amount);

        if (debits != credits)
            throw new InvalidStateException($"Entry does not balance: debits {debits} vs credits {credits}.");
    }
}
