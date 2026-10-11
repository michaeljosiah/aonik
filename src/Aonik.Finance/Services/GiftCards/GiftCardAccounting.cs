using Aonik.Finance.Entities.GiftCards;
using Aonik.Finance.Entities.Ledger;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.GiftCards;
using Aonik.SharedKernel.Abstractions.Ledgers;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.GiftCards;

/// <summary>Checks canonical journal and actual receipt evidence, never a mutable stored balance.</summary>
internal static class GiftCardAccounting
{
    public static async Task<Dictionary<Guid, LedgerAccount>> BindingAsync(FinanceDbContext db, Guid tenantId,
        GiftCardLedgerBinding binding, CancellationToken ct)
    {
        var ids = new[] { binding.CashAccountId, binding.ClearingAccountId, binding.LiabilityAccountId };
        if (binding.LedgerId == Guid.Empty || ids.Contains(Guid.Empty) || ids.Distinct().Count() != 3
            || !await db.Ledgers.AnyAsync(x => x.TenantId == tenantId && !x.IsDeleted && x.Id == binding.LedgerId && x.BaseCurrency == "GBP", ct))
            throw new InvalidStateException("Gift cards require explicit existing GBP ledger accounts.");
        var accounts = await db.LedgerAccounts.AsNoTracking().Where(x => x.TenantId == tenantId && !x.IsDeleted
            && x.LedgerId == binding.LedgerId && ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        if (accounts.Count != 3 || accounts[binding.CashAccountId].AccountType != "Asset"
            || accounts[binding.ClearingAccountId].AccountType != "Liability" || accounts[binding.LiabilityAccountId].AccountType != "Liability")
            throw new InvalidStateException("Gift-card cash, clearing or liability accounts are unavailable.");
        // JournalWriter resolves these codes within the ledger. Prove that resolution
        // identifies only the frozen accounts before allowing an external payment.
        var codes = accounts.Values.Select(x => x.Code).ToArray();
        var resolved = await db.LedgerAccounts.AsNoTracking().Where(x => x.TenantId == tenantId && !x.IsDeleted
            && x.LedgerId == binding.LedgerId && codes.Contains(x.Code)).ToListAsync(ct);
        if (codes.Any(string.IsNullOrWhiteSpace) || codes.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 3
            || resolved.Count != 3 || resolved.Any(x => !accounts.ContainsKey(x.Id)))
            throw new InvalidStateException("Gift-card account codes must uniquely identify the configured accounts.");
        return accounts;
    }

    public static async Task RequireCashAsync(FinanceDbContext db, Guid tenantId, PaymentIntent intent,
        GiftCardFunding funding, CancellationToken ct)
    {
        if (funding.CardAmount == 0) return;
        var receipts = await db.Payments.AsNoTracking().Where(x => x.TenantId == tenantId && x.PaymentIntentId == intent.Id
            && x.Provider == "Stripe" && !x.IsDeleted).ToListAsync(ct);
        foreach (var receipt in db.Payments.Local.Where(x => x.TenantId == tenantId && x.PaymentIntentId == intent.Id && x.Provider == "Stripe"))
        {
            receipts.RemoveAll(x => x.Id == receipt.Id);
            receipts.Add(receipt);
        }
        if (receipts.Count != 1 || receipts[0].OutcomeStatus != "Captured" || receipts[0].Amount != funding.CardAmount
            || receipts[0].Currency != "GBP" || receipts[0].CapturedAt is null || intent.ConnectorId is null
            || receipts[0].ConnectorId != intent.ConnectorId || string.IsNullOrWhiteSpace(intent.ProviderPaymentIntentReference)
            || receipts[0].ProviderReference != intent.ProviderPaymentIntentReference || funding.Ledger is null)
            throw new InvalidStateException("Exact external gift-card funding has not been proved.");
        await RequirePairAsync(db, tenantId, funding.Ledger.LedgerId, "PaymentCapture", intent.Id,
            funding.Ledger.CashAccountId, funding.Ledger.ClearingAccountId, funding.CardAmount, ct);
    }

    public static async Task<Guid> RequirePairAsync(FinanceDbContext db, Guid tenantId, Guid ledgerId,
        string sourceType, Guid sourceId, Guid debitId, Guid creditId, decimal amount, CancellationToken ct)
    {
        var entry = await db.JournalEntries.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId
            && x.SourceType == sourceType && x.SourceId == sourceId && !x.IsDeleted, ct);
        if (entry is null || entry.Status != "Posted" || entry.LedgerId != ledgerId)
            throw new InvalidStateException("Gift-card funding journal is unavailable.");
        var lines = await db.JournalEntryLines.AsNoTracking().Where(x => x.TenantId == tenantId && x.JournalEntryId == entry.Id && !x.IsDeleted).ToListAsync(ct);
        if (lines.Count != 2 || lines.Count(x => x.LedgerAccountId == debitId && x.Direction == JournalDirections.Debit && x.Amount == amount && x.Currency == "GBP") != 1
            || lines.Count(x => x.LedgerAccountId == creditId && x.Direction == JournalDirections.Credit && x.Amount == amount && x.Currency == "GBP") != 1)
            throw new InvalidStateException("Gift-card funding journal does not match its source.");
        return entry.Id;
    }

    public static async Task RequireIssuedAsync(FinanceDbContext db, Guid tenantId, GiftCard card, CancellationToken ct)
    {
        var intent = await db.PaymentIntents.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId
            && x.Id == card.PaymentIntentId && x.OrderId == card.OrderId && !x.IsDeleted, ct);
        var attempt = await db.GiftCardCheckoutAttempts.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId
            && x.PaymentIntentId == card.PaymentIntentId && x.CartId == card.CartId && x.OrderId == card.OrderId && !x.IsDeleted, ct);
        var operation = await db.GiftCardOperations.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId
            && x.GiftCardId == card.Id && x.Kind == "Issue" && x.SourceId == card.PaymentIntentId && !x.IsDeleted, ct);
        if (intent?.Status != "Captured" || attempt?.Status != "Completed" || operation is null
            || operation.Amount != card.FaceValue || operation.OrderId != card.OrderId)
            throw new InvalidStateException("Gift-card issuance funding is not complete.");
        var policy = GiftCardService.ReadPolicy(card);
        var instruction = System.Text.Json.JsonSerializer.Deserialize<GiftCardCheckout>(attempt!.SnapshotJson, GiftCardService.Json)
            ?? throw new InvalidStateException("Gift-card instruction is missing.");
        if (!instruction.PurchasedCards().Any(x => x.OrderItemId == card.OrderItemId && x.ItemIndex == card.ItemIndex && x.FaceValue == card.FaceValue))
            throw new InvalidStateException("Gift-card instrument is not part of its funded instruction.");
        var issueSource = instruction.AdditionalPurchases?.Count > 0 ? card.Id : card.PaymentIntentId;
        var entryId = await RequirePairAsync(db, tenantId, policy.Ledger!.LedgerId, "GiftCardIssue", issueSource,
            policy.Ledger.ClearingAccountId, policy.Ledger.LiabilityAccountId, card.FaceValue, ct);
        if (operation.JournalEntryId != entryId || !await db.JournalEntryLines.AnyAsync(x => x.TenantId == tenantId
            && x.Id == operation.JournalEntryLineId && x.JournalEntryId == entryId && x.Direction == JournalDirections.Credit
            && x.LedgerAccountId == policy.Ledger.LiabilityAccountId && x.Amount == card.FaceValue, ct))
            throw new InvalidStateException("Gift-card issuance provenance is unavailable.");
    }

    public static async Task RequireRedeemedAsync(FinanceDbContext db, Guid tenantId, PaymentIntent intent,
        GiftCardCheckoutAttempt attempt, GiftCardFunding funding, CancellationToken ct)
    {
        var card = await db.GiftCards.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId
            && x.Id == attempt.GiftCardId && !x.IsDeleted, ct);
        var operation = await db.GiftCardOperations.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId
            && x.Kind == "Redeem" && x.SourceId == intent.Id && !x.IsDeleted, ct);
        if (card is null || GiftCardService.ReadPolicy(card).Ledger != funding.Ledger || operation is null
            || operation.GiftCardId != card.Id || operation.OrderId != intent.OrderId || operation.Amount != funding.GiftAmount
            || attempt.ReservedAmount != funding.GiftAmount || funding.Ledger is null)
            throw new InvalidStateException("Gift-card redemption funding is not complete.");

        var entryId = await RequirePairAsync(db, tenantId, funding.Ledger.LedgerId, "GiftCardRedeem", intent.Id,
            funding.Ledger.LiabilityAccountId, funding.Ledger.ClearingAccountId, funding.GiftAmount, ct);
        if (operation.JournalEntryId != entryId || !await db.JournalEntryLines.AnyAsync(x => x.TenantId == tenantId
            && !x.IsDeleted && x.Id == operation.JournalEntryLineId && x.JournalEntryId == entryId
            && x.Direction == JournalDirections.Debit && x.LedgerAccountId == funding.Ledger.LiabilityAccountId
            && x.Amount == funding.GiftAmount && x.Currency == "GBP", ct))
            throw new InvalidStateException("Gift-card redemption provenance is unavailable.");

        var receipts = await db.Payments.AsNoTracking().Where(x => x.TenantId == tenantId
            && x.PaymentIntentId == intent.Id && !x.IsDeleted).ToListAsync(ct);
        var gift = receipts.SingleOrDefault(x => x.Id == operation.PaymentId);
        if (receipts.Count != (funding.CardAmount > 0 ? 2 : 1) || receipts.Sum(x => x.Amount) != intent.Amount
            || gift is null || gift.Provider != "GiftCard" || gift.ConnectorId is not null
            || gift.Amount != funding.GiftAmount || gift.Currency != "GBP" || gift.OutcomeStatus != "Captured"
            || gift.CapturedAt is null || gift.ProviderReference != card.Id.ToString("N"))
            throw new InvalidStateException("Exact gift-card payment receipts have not been proved.");
        await RequireCashAsync(db, tenantId, intent, funding, ct);
    }
}
