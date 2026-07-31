using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Aonik.Finance.Entities.Billing;
using Aonik.Finance.Entities.Ledger;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Observability;
using Aonik.SharedKernel.Abstractions.Ledgers;

namespace Aonik.Finance.Services.Ledger;

/// <summary>
/// Posts system-initiated, double-entry journal entries for payment and
/// billing lifecycle events. Writes <see cref="JournalEntry"/> rows directly to
/// the shared <see cref="FinanceDbContext"/> — deliberately bypassing
/// <c>ILedgerService</c> and its <c>Ledger.Write</c> permission check, because a
/// capture/mark-paid operator is not guaranteed to hold ledger-write rights and
/// the post is machine-driven, not user-authored. This mirrors the established
/// direct-write pattern in <see cref="Seeding.Phases.PartnerPrefundSeedHelper"/>.
///
/// Money moves through a two-step clearing model so the legs net cleanly even
/// though capture and settlement are independent events:
///   capture     →  Dr Cash (1000)              / Cr Payments Clearing (2100)
///   settlement  →  Dr Payments Clearing (2100)  / Cr Operating Revenue (4000)
/// Across the pair the clearing account returns to zero, leaving Dr Cash / Cr
/// Revenue — but either leg may stand alone, which is exactly what a clearing
/// (suspense) account exists to absorb.
/// </summary>
internal sealed class LedgerPostingService
{
    private const string CashAccountCode = "1000";
    private const string CashAccountName = "Cash";
    private const string ClearingAccountCode = "2100";
    private const string ClearingAccountName = "Payments Clearing";
    private const string RevenueAccountCode = "4000";
    private const string RevenueAccountName = "Operating Revenue";

    // Remittance accounts (Spec 036 §7.2). Lazily created like Payments Clearing so the
    // flow does not depend on a chart-of-accounts seed change. The first slice settles in
    // the origin (debit) currency — a "configured settlement currency" per Spec 036 §15 —
    // so every journal entry stays single-currency and Remittance Clearing nets to zero
    // across debit→settlement and debit→reversal. Destination-currency settlement and
    // fee-revenue recognition remain open accounting decisions for finance review.
    private const string RemittanceClearingAccountCode = "2150";
    private const string RemittanceClearingAccountName = "Remittance Clearing";
    private const string CustomerFundsAccountCode = "2200";
    private const string CustomerFundsAccountName = "Customer Funds Liability";
    private const string DueFromPartnerAccountCode = "1300";
    private const string DueFromPartnerAccountName = "Due From Partner";

    // Source identities double as idempotency keys: the filtered unique index on
    // (TenantId, SourceType, SourceId) guarantees one entry per event, so a
    // retried capture / mark-paid cannot double-post.
    private const string PaymentCaptureSourceType = "PaymentCapture";
    private const string InvoiceSettlementSourceType = "InvoiceSettlement";
    private const string RemittanceDebitSourceType = "RemittanceDebit";
    private const string RemittanceSettlementSourceType = "RemittanceSettlement";
    private const string RemittanceFailureReversalSourceType = "RemittanceFailureReversal";

    private readonly FinanceDbContext _db;
    private readonly ILogger<LedgerPostingService> _logger;
    private readonly IReadOnlyList<ISettlementRevenueResolver> _settlementResolvers;

    public LedgerPostingService(
        FinanceDbContext db,
        ILogger<LedgerPostingService>? logger = null,
        IEnumerable<ISettlementRevenueResolver>? settlementResolvers = null)
    {
        _db = db;
        // Spec 088 §9. Optional for the same reason as the logger: existing fixtures construct
        // this service with just a DbContext, and no resolvers means today's behaviour.
        _settlementResolvers = settlementResolvers?.ToList() ?? [];
        // Optional logger so existing test fixtures that construct this service
        // directly with just a DbContext keep compiling. Production DI always
        // resolves a real logger (NullLogger as fallback in tests).
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<LedgerPostingService>.Instance;
    }

    /// <summary>
    /// Records the cash receipt for a captured payment:
    /// Dr Cash / Cr Payments Clearing for the captured amount. Idempotent per
    /// payment intent.
    /// </summary>
    public async Task PostPaymentCaptureAsync(PaymentIntent paymentIntent, CancellationToken cancellationToken = default)
    {
        var tenantId = paymentIntent.TenantId;
        var ledgerId = await GetTenantLedgerIdAsync(tenantId, cancellationToken);
        var cashAccountId = await ResolveRequiredAccountIdAsync(tenantId, CashAccountCode, CashAccountName, cancellationToken);
        var clearingAccountId = await ResolveOrCreateClearingAccountIdAsync(tenantId, ledgerId, cancellationToken);

        await PostBalancedEntryAsync(
            tenantId,
            ledgerId,
            PaymentCaptureSourceType,
            paymentIntent.Id,
            debitAccountId: cashAccountId,
            creditAccountId: clearingAccountId,
            amount: paymentIntent.Amount,
            currency: paymentIntent.Currency,
            narration: "Payment captured",
            orderId: paymentIntent.OrderId,
            cancellationToken);
    }

    /// <summary>
    /// Recognises revenue when an invoice is settled:
    /// Dr Payments Clearing / Cr Operating Revenue for the invoice total.
    /// Idempotent per invoice.
    /// </summary>
    public async Task PostInvoiceSettlementAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        var tenantId = invoice.TenantId;
        var ledgerId = await GetTenantLedgerIdAsync(tenantId, cancellationToken);
        var clearingAccountId = await ResolveOrCreateClearingAccountIdAsync(tenantId, ledgerId, cancellationToken);

        // Spec 088 §9 - route the revenue leg by the funding order's type, and split it per line.
        // Reads Invoice.OrderId, which Spec 088 §7 made non-null for order-backed invoices; before
        // that this could not have worked at all.
        var routed = await TryResolveRoutedCreditsAsync(invoice, cancellationToken);

        if (routed is not null)
        {
            await PostSplitSettlementAsync(tenantId, ledgerId, invoice, clearingAccountId, routed, cancellationToken);
            return;
        }

        // UNCHANGED fallback: no order, no order type, or no resolver claims it. Every product
        // that existed before this seam takes exactly this path and posts exactly what it did.
        var revenueAccountId = await ResolveRequiredAccountIdAsync(tenantId, RevenueAccountCode, RevenueAccountName, cancellationToken);

        await PostBalancedEntryAsync(
            tenantId,
            ledgerId,
            InvoiceSettlementSourceType,
            invoice.Id,
            debitAccountId: clearingAccountId,
            creditAccountId: revenueAccountId,
            amount: invoice.Total,
            currency: invoice.Currency,
            narration: "Invoice settled",
            orderId: invoice.OrderId,
            cancellationToken);
    }

    /// <summary>
    /// Posts the customer debit for a confirmed remittance — BEFORE any connector call:
    /// Dr Customer Funds Liability / Cr Remittance Clearing for the quote total in the
    /// origin currency. Idempotent per order (SourceId = orderId). Spec 036 §7.
    /// </summary>
    public async Task PostRemittanceDebitAsync(
        Guid tenantId,
        Guid orderId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken = default)
    {
        var ledgerId = await GetTenantLedgerIdAsync(tenantId, cancellationToken);
        var customerFundsAccountId = await ResolveOrCreateAccountIdAsync(
            tenantId, ledgerId, CustomerFundsAccountCode, CustomerFundsAccountName, "Liability", cancellationToken);
        var clearingAccountId = await ResolveOrCreateAccountIdAsync(
            tenantId, ledgerId, RemittanceClearingAccountCode, RemittanceClearingAccountName, "Liability", cancellationToken);

        await PostBalancedEntryAsync(
            tenantId,
            ledgerId,
            RemittanceDebitSourceType,
            orderId,
            debitAccountId: customerFundsAccountId,
            creditAccountId: clearingAccountId,
            amount: amount,
            currency: currency,
            narration: "Remittance debit",
            orderId: orderId,
            cancellationToken);
    }

    /// <summary>
    /// Posts settlement for a succeeded remittance payout:
    /// Dr Remittance Clearing / Cr Due From Partner for the settled amount. Idempotent per
    /// payout (SourceId = payoutId). Spec 036 §7.
    /// </summary>
    public async Task PostRemittanceSettlementAsync(
        Guid tenantId,
        Guid payoutId,
        Guid? orderId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken = default)
    {
        var ledgerId = await GetTenantLedgerIdAsync(tenantId, cancellationToken);
        var clearingAccountId = await ResolveOrCreateAccountIdAsync(
            tenantId, ledgerId, RemittanceClearingAccountCode, RemittanceClearingAccountName, "Liability", cancellationToken);
        var dueFromPartnerAccountId = await ResolveOrCreateAccountIdAsync(
            tenantId, ledgerId, DueFromPartnerAccountCode, DueFromPartnerAccountName, "Asset", cancellationToken);

        await PostBalancedEntryAsync(
            tenantId,
            ledgerId,
            RemittanceSettlementSourceType,
            payoutId,
            debitAccountId: clearingAccountId,
            creditAccountId: dueFromPartnerAccountId,
            amount: amount,
            currency: currency,
            narration: "Remittance settled",
            orderId: orderId,
            cancellationToken);
    }

    /// <summary>
    /// Reverses the customer debit when a remittance payout fails or is reversed after the
    /// debit posted: Dr Remittance Clearing / Cr Customer Funds Liability for the original
    /// debited amount. Idempotent per payout (SourceId = payoutId). Spec 036 §7.
    /// </summary>
    public async Task PostRemittanceFailureReversalAsync(
        Guid tenantId,
        Guid payoutId,
        Guid? orderId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken = default)
    {
        var ledgerId = await GetTenantLedgerIdAsync(tenantId, cancellationToken);
        var clearingAccountId = await ResolveOrCreateAccountIdAsync(
            tenantId, ledgerId, RemittanceClearingAccountCode, RemittanceClearingAccountName, "Liability", cancellationToken);
        var customerFundsAccountId = await ResolveOrCreateAccountIdAsync(
            tenantId, ledgerId, CustomerFundsAccountCode, CustomerFundsAccountName, "Liability", cancellationToken);

        await PostBalancedEntryAsync(
            tenantId,
            ledgerId,
            RemittanceFailureReversalSourceType,
            payoutId,
            debitAccountId: clearingAccountId,
            creditAccountId: customerFundsAccountId,
            amount: amount,
            currency: currency,
            narration: "Remittance reversed",
            orderId: orderId,
            cancellationToken);
    }

    /// <summary>
    /// Spec 088 §9.1 — the credit account for each of an invoice's lines, or null when nothing
    /// claims this invoice and the caller should take the unchanged single-line path.
    /// </summary>
    private async Task<List<(InvoiceLine Line, SettlementCredit Credit)>?> TryResolveRoutedCreditsAsync(
        Invoice invoice,
        CancellationToken cancellationToken)
    {
        if (_settlementResolvers.Count == 0 || invoice.OrderId is not { } orderId)
            return null;

        var orderType = await _db.Orders.AsNoTracking()
            .Where(o => o.Id == orderId && o.TenantId == invoice.TenantId)
            .Select(o => o.OrderType)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(orderType))
            return null;

        var claiming = _settlementResolvers
            .Where(r => r.OrderTypes.Any(t => string.Equals(t, orderType, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (claiming.Count == 0)
            return null;

        if (claiming.Count > 1)
        {
            // Ambiguous routing of real revenue is not something to resolve by picking first.
            throw new InvalidOperationException(
                $"{claiming.Count} settlement resolvers claim order type '{orderType}'. Exactly one must.");
        }

        var resolver = claiming[0];

        // Lines are not loaded by the settlement caller, so fetch them here rather than relying on
        // whatever the caller happened to include.
        var lines = await _db.InvoiceLines.AsNoTracking()
            .Where(l => l.InvoiceId == invoice.Id && l.TenantId == invoice.TenantId)
            .OrderBy(l => l.Id)
            .ToListAsync(cancellationToken);

        if (lines.Count == 0)
            return null;

        return lines
            .Select(line => (
                Line: line,
                Credit: resolver.Resolve(orderType, new SettlementLineContext(
                    invoice.Id,
                    invoice.OrderId,
                    line.Id,
                    line.Description,
                    line.LineTotal,
                    invoice.Currency))))
            .ToList();
    }

    /// <summary>
    /// Spec 088 §9.2 — one debit for the invoice total, one credit per line, each dimensioned.
    ///
    /// A single credit for the total cannot carry a truthful per-line tag when an order mixes
    /// kinds — two prepaid meters in one checkout, or goods alongside a service — which would
    /// leave the balance unreconcilable by whatever dimension the consumer cares about.
    /// </summary>
    private async Task PostSplitSettlementAsync(
        Guid tenantId,
        Guid ledgerId,
        Invoice invoice,
        Guid clearingAccountId,
        List<(InvoiceLine Line, SettlementCredit Credit)> routed,
        CancellationToken cancellationToken)
    {
        using var activity = FinanceActivitySource.Source.StartActivity("ledger.post");
        activity?.SetTag(FinanceActivitySource.StageTag, MoneyActionStages.Settle);
        activity?.SetTag(FinanceActivitySource.TenantIdTag, tenantId);
        activity?.SetTag("LedgerSourceType", InvoiceSettlementSourceType);
        activity?.SetTag("LedgerSourceId", invoice.Id);
        if (invoice.OrderId is { } tagOrderId && tagOrderId != Guid.Empty)
            activity?.SetTag(FinanceActivitySource.OrderIdTag, tagOrderId);

        var total = routed.Sum(r => r.Line.LineTotal);

        if (total <= 0)
        {
            throw new InvalidOperationException(
                $"Cannot post a non-positive amount ({total}) for {InvoiceSettlementSourceType} {invoice.Id}.");
        }

        // Same idempotency key as the single-line path, so an invoice cannot be settled twice by
        // taking a different branch on a retry.
        var alreadyPosted = await _db.JournalEntries.AsNoTracking()
            .AnyAsync(e => e.TenantId == tenantId
                           && e.SourceType == InvoiceSettlementSourceType
                           && e.SourceId == invoice.Id,
                cancellationToken);

        if (alreadyPosted)
        {
            activity?.SetTag(FinanceActivitySource.OutcomeTag, MoneyActionOutcomes.SkippedIdempotent);
            _logger.LedgerPostSkippedIdempotent(invoice.OrderId, tenantId);
            return;
        }

        var now = DateTime.UtcNow;
        var entryId = Guid.NewGuid();

        var entryLines = new List<JournalEntryLine>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                JournalEntryId = entryId,
                LedgerAccountId = clearingAccountId,
                Direction = "Debit",
                Amount = total,
                Currency = invoice.Currency,
                Narration = "Invoice settled",
                DimensionsJson = "{}",
                CreatedAt = now
            }
        };

        foreach (var (line, credit) in routed)
        {
            var creditAccountId = await ResolveOrCreateAccountIdAsync(
                tenantId, ledgerId, credit.AccountCode, credit.AccountName, credit.AccountType, cancellationToken);

            entryLines.Add(new JournalEntryLine
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                JournalEntryId = entryId,
                LedgerAccountId = creditAccountId,
                Direction = "Credit",
                Amount = line.LineTotal,
                Currency = invoice.Currency,
                Narration = line.Description,
                DimensionsJson = credit.DimensionsJson ?? "{}",
                CreatedAt = now
            });
        }

        _db.JournalEntries.Add(new JournalEntry
        {
            Id = entryId,
            TenantId = tenantId,
            LedgerId = ledgerId,
            Timestamp = now,
            SourceType = InvoiceSettlementSourceType,
            SourceId = invoice.Id,
            Status = "Posted",
            CreatedAt = now,
            Lines = entryLines
        });

        await _db.SaveChangesAsync(cancellationToken);

        activity?.SetTag(FinanceActivitySource.OutcomeTag, MoneyActionOutcomes.Success);
    }

    private async Task PostBalancedEntryAsync(
        Guid tenantId,
        Guid ledgerId,
        string sourceType,
        Guid sourceId,
        Guid debitAccountId,
        Guid creditAccountId,
        decimal amount,
        string currency,
        string narration,
        Guid? orderId,
        CancellationToken cancellationToken)
    {
        // Observability span for Issue #142. Stage="settle"; tag OrderId when
        // present so the saved KQL query can pivot. The two outcome paths we
        // care about (idempotent skip vs successful post) and the lost-race
        // exception path each emit their own structured log below.
        using var activity = FinanceActivitySource.Source.StartActivity("ledger.post");
        activity?.SetTag(FinanceActivitySource.StageTag, MoneyActionStages.Settle);
        activity?.SetTag(FinanceActivitySource.TenantIdTag, tenantId);
        // SourceType / SourceId are internal idempotency keys; not part of
        // the OrderId-pivoted query surface but useful when correlating a
        // duplicate-post complaint back to the underlying capture/invoice.
        activity?.SetTag("LedgerSourceType", sourceType);
        activity?.SetTag("LedgerSourceId", sourceId);
        if (orderId.HasValue && orderId.Value != Guid.Empty)
        {
            activity?.SetTag(FinanceActivitySource.OrderIdTag, orderId.Value);
        }

        if (amount <= 0)
        {
            throw new InvalidOperationException(
                $"Cannot post a non-positive amount ({amount}) for {sourceType} {sourceId}.");
        }

        // Fast-path idempotency. The filtered unique index is the real authority
        // (and the only guard under concurrency); this pre-check just avoids the
        // insert churn on the common already-posted / retried-call path.
        var alreadyPosted = await _db.JournalEntries
            .AsNoTracking()
            .AnyAsync(entry =>
                entry.TenantId == tenantId &&
                entry.SourceType == sourceType &&
                entry.SourceId == sourceId,
                cancellationToken);

        if (alreadyPosted)
        {
            // Previously silent — operators triaging a duplicate-post complaint
            // had no signal that the idempotency guard had short-circuited.
            activity?.SetTag(FinanceActivitySource.OutcomeTag, MoneyActionOutcomes.SkippedIdempotent);
            _logger.LedgerPostSkippedIdempotent(orderId, tenantId);
            return;
        }

        var now = DateTime.UtcNow;
        var entryId = Guid.NewGuid();
        var entry = new JournalEntry
        {
            Id = entryId,
            TenantId = tenantId,
            LedgerId = ledgerId,
            Timestamp = now,
            SourceType = sourceType,
            SourceId = sourceId,
            Status = "Posted",
            CreatedAt = now,
            Lines = new List<JournalEntryLine>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    JournalEntryId = entryId,
                    LedgerAccountId = debitAccountId,
                    Direction = "Debit",
                    Amount = amount,
                    Currency = currency,
                    Narration = narration,
                    DimensionsJson = "{}",
                    CreatedAt = now
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    JournalEntryId = entryId,
                    LedgerAccountId = creditAccountId,
                    Direction = "Credit",
                    Amount = amount,
                    Currency = currency,
                    Narration = narration,
                    DimensionsJson = "{}",
                    CreatedAt = now
                }
            }
        };

        _db.JournalEntries.Add(entry);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);

            activity?.SetTag(FinanceActivitySource.OutcomeTag, MoneyActionOutcomes.Success);
            activity?.SetTag(FinanceActivitySource.JournalEntryIdTag, entryId);
            _logger.LedgerPosted(orderId, tenantId, entryId, amount, currency);
        }
        catch (DbUpdateException ex)
        {
            // Lost the idempotency race: a concurrent capture / mark-paid committed
            // the same (TenantId, SourceType, SourceId) first and tripped the
            // filtered unique index. Detach our rejected graph and treat the
            // existing post as the winner — the effect is recorded exactly once.
            _db.Entry(entry).State = EntityState.Detached;
            foreach (var line in entry.Lines)
            {
                _db.Entry(line).State = EntityState.Detached;
            }

            var winningEntryExists = await _db.JournalEntries
                .AsNoTracking()
                .AnyAsync(item =>
                    item.TenantId == tenantId &&
                    item.SourceType == sourceType &&
                    item.SourceId == sourceId,
                    cancellationToken);

            if (!winningEntryExists)
            {
                activity?.SetTag(FinanceActivitySource.OutcomeTag, MoneyActionOutcomes.Failed);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                _logger.LedgerPostFailed(orderId, tenantId, ex.Message, ex);
                throw;
            }

            // Lost-race-but-winner-exists: same outcome as the up-front
            // idempotency hit — the effect is recorded exactly once.
            activity?.SetTag(FinanceActivitySource.OutcomeTag, MoneyActionOutcomes.SkippedIdempotent);
            _logger.LedgerPostSkippedIdempotent(orderId, tenantId);
        }
    }

    private async Task<Guid> GetTenantLedgerIdAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var ledgerId = await _db.Ledgers
            .AsNoTracking()
            .Where(ledger => ledger.TenantId == tenantId)
            .Select(ledger => (Guid?)ledger.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (!ledgerId.HasValue)
        {
            throw new InvalidOperationException($"Tenant {tenantId} does not have a ledger to post against.");
        }

        return ledgerId.Value;
    }

    private async Task<Guid> ResolveRequiredAccountIdAsync(
        Guid tenantId,
        string code,
        string nameFallback,
        CancellationToken cancellationToken)
    {
        var accountId = await _db.LedgerAccounts
            .AsNoTracking()
            .Where(account => account.TenantId == tenantId && account.Code == code)
            .Select(account => (Guid?)account.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (accountId.HasValue)
        {
            return accountId.Value;
        }

        // Code is the canonical key; fall back to the seeded display name in case
        // a tenant's chart was authored with a different numbering scheme.
        accountId = await _db.LedgerAccounts
            .AsNoTracking()
            .Where(account => account.TenantId == tenantId && account.Name == nameFallback)
            .Select(account => (Guid?)account.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (!accountId.HasValue)
        {
            throw new InvalidOperationException(
                $"Tenant {tenantId} is missing the '{nameFallback}' ledger account (code {code}) required to post the entry.");
        }

        return accountId.Value;
    }

    private async Task<Guid> ResolveOrCreateClearingAccountIdAsync(
        Guid tenantId,
        Guid ledgerId,
        CancellationToken cancellationToken)
    {
        var clearingAccountId = await _db.LedgerAccounts
            .Where(account => account.TenantId == tenantId && account.Code == ClearingAccountCode)
            .Select(account => (Guid?)account.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (clearingAccountId.HasValue)
        {
            return clearingAccountId.Value;
        }

        // Self-healing path for ledgers provisioned before Payments Clearing
        // joined the default chart of accounts. New tenants get it at provisioning
        // time; older ledgers materialise it lazily here on first capture. The
        // LedgerAccount.Code index is non-unique, so two concurrent first-captures
        // could each create a clearing row — harmless, as both are valid liability
        // accounts that net identically.
        var now = DateTime.UtcNow;
        var clearingAccount = new LedgerAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LedgerId = ledgerId,
            AccountType = "Liability",
            Name = ClearingAccountName,
            Code = ClearingAccountCode,
            DimensionsJson = "{}",
            CreatedAt = now
        };

        _db.LedgerAccounts.Add(clearingAccount);
        await _db.SaveChangesAsync(cancellationToken);

        return clearingAccount.Id;
    }

    /// <summary>
    /// Resolves a ledger account by code, lazily creating it with the given name/type if absent.
    /// Mirrors the self-healing clearing-account path: the <c>LedgerAccount.Code</c> index is
    /// non-unique, so two concurrent first-uses could each create a row — harmless, as both are
    /// valid accounts of the same type that net identically.
    /// </summary>
    private async Task<Guid> ResolveOrCreateAccountIdAsync(
        Guid tenantId,
        Guid ledgerId,
        string code,
        string name,
        string accountType,
        CancellationToken cancellationToken)
    {
        var accountId = await _db.LedgerAccounts
            .Where(account => account.TenantId == tenantId && account.Code == code)
            .Select(account => (Guid?)account.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (accountId.HasValue)
        {
            return accountId.Value;
        }

        var now = DateTime.UtcNow;
        var account = new LedgerAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LedgerId = ledgerId,
            AccountType = accountType,
            Name = name,
            Code = code,
            DimensionsJson = "{}",
            CreatedAt = now
        };

        _db.LedgerAccounts.Add(account);
        await _db.SaveChangesAsync(cancellationToken);

        return account.Id;
    }
}
