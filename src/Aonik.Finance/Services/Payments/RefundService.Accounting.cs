using System.Text.Json;

using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Services.GiftCards;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.GiftCards;
using Aonik.SharedKernel.Abstractions.Ledgers;
using Aonik.SharedKernel.Abstractions.Loyalty;
using Aonik.SharedKernel.Abstractions.Payments;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.Payments;

internal sealed partial class RefundService
{
    private async Task<RefundCashLedger?> RequireFundingAsync(PaymentIntent intent, CheckoutRefundSource source, CancellationToken ct)
    {
        var funding = await gifts.ReadFundingTrackedAsync(intent, ct);
        if (funding.GiftAmount != source.GiftAmount || funding.CardAmount != source.CardAmount)
            throw new InvalidStateException("The refund breakdown does not match its original funding.");
        var giftAttempt = await db.GiftCardCheckoutAttempts.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == TenantId
            && x.PaymentIntentId == intent.Id && !x.IsDeleted, ct);
        if (giftAttempt is not null)
        {
            if (giftAttempt.Status != "Completed") throw new InvalidStateException("Gift funding has not completed.");
            if (funding.GiftAmount > 0)
                await GiftCardAccounting.RequireRedeemedAsync(db, TenantId, intent, giftAttempt, funding, ct);
            var instruction = JsonSerializer.Deserialize<GiftCardCheckout>(giftAttempt.SnapshotJson, RefundSnapshot.Json)
                ?? throw new InvalidStateException("Original gift funding facts are unavailable.");
            foreach (var component in source.Components)
            {
                var original = component.OrderItemId is { } itemId
                    ? instruction.Tender?.Lines.SingleOrDefault(x => x.OrderItemId == itemId)?.GiftFundedValue ?? 0m
                    : instruction.Tender?.GiftFundedTaxAmount ?? 0m;
                if (component.GiftFundedValue != original) throw new InvalidStateException("The original gift allocation changed.");
            }
        }
        var loyaltyAttempt = await db.LoyaltyCheckoutAttempts.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == TenantId
            && x.PaymentIntentId == intent.Id && !x.IsDeleted, ct);
        var points = loyaltyAttempt is null ? null : JsonSerializer.Deserialize<LoyaltyCheckout>(loyaltyAttempt.SnapshotJson, RefundSnapshot.Json);
        if (loyaltyAttempt is not null && (loyaltyAttempt.Status != "Completed" || points is null))
            throw new InvalidStateException("Original loyalty accounting has not completed.");
        foreach (var component in source.Components)
        {
            var original = points?.Lines.SingleOrDefault(x => x.OrderItemId == component.OrderItemId);
            if (component.EarnedPoints != (original?.EarnedPoints ?? 0) || component.RedeemedPoints != (original?.RedeemedPoints ?? 0)
                || (original is not null && component.Amount != original.OriginalCharged - original.CouponDiscount - original.PointsAppliedValue))
                throw new InvalidStateException("The original points allocation changed.");
        }
        var receipts = await db.Payments.AsNoTracking().Where(x => x.TenantId == TenantId && x.PaymentIntentId == intent.Id && !x.IsDeleted).ToListAsync(ct);
        if (receipts.Count != (source.CardAmount > 0 ? 1 : 0) + (source.GiftAmount > 0 ? 1 : 0)
            || receipts.Any(x => x.Currency != "GBP" || x.OutcomeStatus != "Captured" || x.CapturedAt is null)
            || receipts.Sum(x => x.Amount) != source.Total)
            throw new InvalidStateException("The original payment receipts do not reconcile.");
        if (source.CardAmount == 0) return null;
        var receipt = receipts.SingleOrDefault(x => x.Provider == "Stripe");
        if (receipt is null || receipt.Amount != source.CardAmount || receipt.ConnectorId != intent.ConnectorId
            || receipt.ProviderReference != intent.ProviderPaymentIntentReference || intent.ConnectorId is null
            || intent.ProviderCode != "Stripe" || intent.ProviderLiveMode is null || string.IsNullOrEmpty(intent.ProviderAccountId)
            || string.IsNullOrEmpty(intent.ProviderPaymentIntentReference))
            throw new InvalidStateException("The original Stripe cash receipt is unavailable.");
        var journal = await db.JournalEntries.AsNoTracking().Include(x => x.Lines).SingleOrDefaultAsync(x => x.TenantId == TenantId
            && x.SourceType == "PaymentCapture" && x.SourceId == intent.Id && !x.IsDeleted, ct);
        if (journal is null || journal.Status != "Posted" || journal.Lines.Count != 2
            || journal.Lines.Any(x => x.TenantId != TenantId || x.IsDeleted || x.Amount != source.CardAmount || x.Currency != "GBP"))
            throw new InvalidStateException("The original cash journal is unavailable.");
        var debit = journal.Lines.SingleOrDefault(x => x.Direction == JournalDirections.Debit);
        var credit = journal.Lines.SingleOrDefault(x => x.Direction == JournalDirections.Credit);
        if (debit is null || credit is null || debit.LedgerAccountId == credit.LedgerAccountId)
            throw new InvalidStateException("The original cash journal is invalid.");
        var binding = new RefundCashLedger(receipt.Id, journal.Id, journal.LedgerId, debit.LedgerAccountId, credit.LedgerAccountId, source.CardAmount);
        await CashAccountsAsync(binding, ct);
        return binding;
    }

    private async Task<(string Cash, string Clearing)> CashAccountsAsync(RefundCashLedger binding, CancellationToken ct)
    {
        var ids = new[] { binding.CashAccountId, binding.ClearingAccountId };
        var accounts = await db.LedgerAccounts.AsNoTracking().Where(x => x.TenantId == TenantId && !x.IsDeleted
            && x.LedgerId == binding.LedgerId && ids.Contains(x.Id)).ToListAsync(ct);
        if (accounts.Count != 2 || accounts.Single(x => x.Id == binding.CashAccountId).AccountType != "Asset"
            || accounts.Single(x => x.Id == binding.ClearingAccountId).AccountType != "Liability"
            || !await db.Ledgers.AnyAsync(x => x.TenantId == TenantId && x.Id == binding.LedgerId && !x.IsDeleted && x.BaseCurrency == "GBP", ct))
            throw new InvalidStateException("The original cash accounts are unavailable.");
        var codes = accounts.Select(x => x.Code).ToArray();
        if (codes.Any(string.IsNullOrWhiteSpace) || codes.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 2
            || await db.LedgerAccounts.CountAsync(x => x.TenantId == TenantId && !x.IsDeleted
                && x.LedgerId == binding.LedgerId && codes.Contains(x.Code), ct) != 2)
            throw new InvalidStateException("Original account codes do not resolve uniquely.");
        return (accounts.Single(x => x.Id == binding.CashAccountId).Code, accounts.Single(x => x.Id == binding.ClearingAccountId).Code);
    }

    private async Task PostCashAsync(Refund refund, RefundSnapshot snapshot, bool returned, CancellationToken ct)
    {
        if (snapshot.CashAmount == 0) return;
        var binding = snapshot.CashLedger ?? throw new InvalidStateException("Original cash accounting is unavailable.");
        var accounts = await CashAccountsAsync(binding, ct);
        var sourceType = returned ? "RefundCashReturned" : "RefundCash";
        await journals.PostAsync(new(binding.LedgerId, sourceType, refund.Id,
        [
            new(returned ? accounts.Cash : accounts.Clearing, JournalDirections.Debit, snapshot.CashAmount, refund.Currency, "Order refund"),
            new(returned ? accounts.Clearing : accounts.Cash, JournalDirections.Credit, snapshot.CashAmount, refund.Currency, "Order refund")
        ], clock.UtcNow), ct);
        await GiftCardAccounting.RequirePairAsync(db, TenantId, binding.LedgerId, sourceType, refund.Id,
            returned ? binding.CashAccountId : binding.ClearingAccountId,
            returned ? binding.ClearingAccountId : binding.CashAccountId, snapshot.CashAmount, ct);
    }
}
