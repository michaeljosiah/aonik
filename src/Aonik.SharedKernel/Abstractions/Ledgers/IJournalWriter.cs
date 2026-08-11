namespace Aonik.SharedKernel.Abstractions.Ledgers;

/// <summary>
/// Write-side contract for posting a double-entry journal from outside <c>Aonik.Finance</c>
/// (Spec 088 §5) — the write mirror of the ADR-006 read contracts, alongside
/// <c>IInvoiceWriter</c> and <c>IPaymentInitiator</c>. Implemented by Finance and consumed by any
/// module that must record financial truth without referencing Finance.
///
/// Until this existed, <c>ILedgerService</c> lived in <c>Aonik.Finance.Contracts</c> and no other
/// module could post at all.
/// </summary>
public interface IJournalWriter
{
    /// <summary>
    /// Post one balanced entry. Finance validates that debits equal credits, that every account
    /// code resolves <b>within the named ledger</b>, and that the ledger belongs to the current
    /// tenant.
    ///
    /// <b>Idempotent</b> on <c>(SourceType, SourceId)</c>: re-posting the same business event
    /// returns the existing entry rather than double-posting, enforced by a filtered unique index
    /// that already exists. The one exception is the reserved <c>"Manual"</c> source type, which
    /// the index deliberately excludes because hand-authored entries all share
    /// <c>SourceId = Guid.Empty</c> — so a caller must not use it.
    /// </summary>
    /// <exception cref="NotFoundException">The ledger, or an account code within it, does not resolve.</exception>
    /// <exception cref="InvalidStateException">The entry does not balance, mixes currencies, has no lines, or uses the reserved "Manual" source type.</exception>
    Task<JournalEntryRef> PostAsync(PostJournalCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// One leg of an entry. <paramref name="AccountCode"/> is resolved within the command's ledger —
/// codes are unique per ledger, not per tenant.
/// </summary>
/// <param name="Direction">One of <see cref="JournalDirections"/>.</param>
/// <param name="DimensionsJson">
/// Opaque analytic tags, e.g. <c>{"meterCode":"animated-videos"}</c>. This is what lets a consumer
/// post many lines into one account and still separate them afterwards — without it, subtracting
/// two account balances yields a tenant-wide aggregate rather than a per-dimension figure. Finance
/// stores it and never interprets it.
/// </param>
public sealed record JournalLineSpec(
    string AccountCode,
    string Direction,
    decimal Amount,
    string Currency,
    string? Narration = null,
    string? DimensionsJson = null);

/// <summary>
/// Post an entry to <paramref name="LedgerId"/>.
/// </summary>
/// <param name="LedgerId">
/// <b>Required.</b> A tenant may hold several ledgers and account codes are unique only within
/// one, so there is no safe default — a code-only command would force a <c>FirstOrDefault</c> that
/// can post a material entry into the wrong ledger, in the wrong base currency. Callers that do
/// not track ledgers resolve one through <see cref="ILedgerResolver"/>.
/// </param>
/// <param name="SourceType">The originating business event type. The idempotency key, with <paramref name="SourceId"/>.</param>
public sealed record PostJournalCommand(
    Guid LedgerId,
    string SourceType,
    Guid SourceId,
    IReadOnlyList<JournalLineSpec> Lines,
    DateTime? TimestampUtc = null);

/// <summary>A reference to the posted entry.</summary>
/// <param name="AlreadyExisted">
/// True when idempotency returned an entry posted earlier rather than creating one. Callers that
/// must not repeat a side effect alongside the post can branch on this.
/// </param>
public sealed record JournalEntryRef(Guid JournalEntryId, string SourceType, Guid SourceId, bool AlreadyExisted);

/// <summary>The two legs of a double entry.</summary>
public static class JournalDirections
{
    public const string Debit = "Debit";
    public const string Credit = "Credit";

    /// <summary>
    /// Reserved for hand-authored entries, which are excluded from the idempotency index because
    /// they all share <c>SourceId = Guid.Empty</c>. <see cref="IJournalWriter"/> rejects it.
    /// </summary>
    public const string ManualSourceType = "Manual";
}
