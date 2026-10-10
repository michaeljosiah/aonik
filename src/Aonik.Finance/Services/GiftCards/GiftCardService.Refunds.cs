using Aonik.Finance.Entities.GiftCards;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Services.Payments;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.GiftCards;
using Aonik.SharedKernel.Abstractions.Ledgers;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.GiftCards;

internal sealed record GiftCardRefundInstruction(Guid GiftCardId, Guid OriginalOperationId, string Kind, decimal Amount)
{
    public const string RestoreTender = "RestoreTender";
    public const string ReclaimPurchase = "ReclaimPurchase";
}

internal sealed partial class GiftCardService
{
    public async Task<GiftCardRefundInstruction?> ReadRefundInstructionAsync(PaymentIntent intent,
        decimal restoreAmount, decimal reclaimAmount, CancellationToken ct = default, Guid? orderItemId = null)
    {
        if (intent.TenantId != TenantId || intent.Status != "Captured" || !Money(restoreAmount) || !Money(reclaimAmount)
            || restoreAmount > 0 && reclaimAmount > 0)
            throw new InvalidStateException("Gift-card refunds require their original captured funding.");
        if (restoreAmount == 0 && reclaimAmount == 0) return null;
        var restoring = restoreAmount > 0;
        Guid? cardId = null;
        if (!restoring && orderItemId is not null)
            cardId = await db.GiftCards.Where(x => x.TenantId == TenantId && !x.IsDeleted && x.PaymentIntentId == intent.Id && x.OrderItemId == orderItemId).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct)
                ?? throw new InvalidStateException("The selected order line has no original issued gift card.");
        if (!restoring && cardId is null && await db.GiftCardOperations.CountAsync(x => x.TenantId == TenantId
            && x.Kind == "Issue" && x.SourceId == intent.Id && !x.IsDeleted, ct) > 1)
            throw new InvalidStateException("A multi-card purchase refund must identify its original gift card; amount-only reclaim is unavailable.");
        var original = await OperationAsync(restoring ? "Redeem" : "Issue", intent.Id, ct, cardId)
            ?? throw new InvalidStateException("The original gift-card operation is unavailable.");
        var card = await db.GiftCards.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == TenantId
            && x.Id == original.GiftCardId && !x.IsDeleted, ct)
            ?? throw new InvalidStateException("The original gift card is unavailable.");
        await RequireOriginalRefundProofAsync(intent, card, original, ct);
        await GiftCardAccounting.BindingAsync(db, TenantId, ReadPolicy(card).Ledger!, ct);
        var amount = restoring ? restoreAmount : reclaimAmount;
        if (amount > original.Amount) throw new InvalidStateException("The refund exceeds the original gift-card value.");
        return new(card.Id, original.Id, restoring ? GiftCardRefundInstruction.RestoreTender : GiftCardRefundInstruction.ReclaimPurchase, amount);
    }

    // These hooks use the coordinator's Finance transaction and native payment/refund claim.
    // JournalWriter's internal Save remains enclosed by that transaction; no hook commits it.
    public async Task PrepareRefundTrackedAsync(Refund refund, GiftCardRefundInstruction? gift, CancellationToken ct = default)
    {
        ValidateRefundInstruction(refund, gift);
        if (gift is null) return;
        var (card, original, binding, balance) = await RefundSourceAsync(refund, gift, ct);
        if (await RefundOperationExistsAsync(refund, gift, original, binding, ct)) return;
        if (refund.EffectsAppliedAtUtc is not null || refund.GiftReclaimReleasedAtUtc is not null
            || refund.Status is not ("Requested" or "Unknown" or "Pending"))
            throw new InvalidStateException("This gift-card refund cannot be prepared again.");
        if (gift.Kind == GiftCardRefundInstruction.ReclaimPurchase && refund.GiftReclaimCardId is not null)
        {
            RequireReclaimHold(refund, gift);
            return; // The accepted hold survives expiry while a provider outcome is unknown.
        }
        if (UnavailableReason(card) is not null)
            throw new InvalidStateException("The original gift card is unavailable or expired; contact support to resolve this refund.");
        await RequireRefundLimitAsync(refund, gift, original, ct);
        if (gift.Kind == GiftCardRefundInstruction.ReclaimPurchase)
        {
            if (balance.Balance - balance.Reserved < gift.Amount)
                throw new InvalidStateException("The purchased gift value has been spent or reserved and cannot be refunded.");
            refund.GiftReclaimCardId = card.Id;
            refund.GiftReclaimAmount = gift.Amount;
        }
        Touch(card);
    }

    public async Task CompleteRefundTrackedAsync(Refund refund, GiftCardRefundInstruction? gift, CancellationToken ct = default)
    {
        ValidateRefundInstruction(refund, gift);
        if (gift is null) return;
        var (card, original, binding, balance) = await RefundSourceAsync(refund, gift, ct);
        if (await RefundOperationExistsAsync(refund, gift, original, binding, ct)) return;
        if (refund.EffectsAppliedAtUtc is not null || refund.GiftReclaimReleasedAtUtc is not null
            || refund.Status is not ("Requested" or "Unknown" or "Pending"))
            throw new InvalidStateException("The gift-card refund is not awaiting completion.");
        await RequireRefundLimitAsync(refund, gift, original, ct);
        var restoring = gift.Kind == GiftCardRefundInstruction.RestoreTender;
        if (!restoring)
        {
            RequireReclaimHold(refund, gift);
            if (balance.Balance < balance.Reserved)
                throw new InvalidStateException("The reserved gift refund value is unavailable.");
        }
        // Completion honors an accepted obligation even if the original validity has elapsed.
        var accounts = await GiftCardAccounting.BindingAsync(db, TenantId, binding, ct);
        var kind = restoring ? "Restore" : "Reclaim";
        var operation = new GiftCardOperation { TenantId = TenantId, GiftCardId = card.Id, Kind = kind,
            SourceId = refund.Id, OriginalOperationId = original.Id, OrderId = original.OrderId,
            Amount = gift.Amount, OccurredAtUtc = clock.UtcNow, PaymentId = restoring ? original.PaymentId : null };
        var debit = restoring ? binding.ClearingAccountId : binding.LiabilityAccountId;
        var credit = restoring ? binding.LiabilityAccountId : binding.ClearingAccountId;
        Touch(card);
        var dimensions = Serialize(new { giftCardId = card.Id, operationId = operation.Id, originalOperationId = original.Id, refundId = refund.Id });
        var posted = await journals.PostAsync(new(binding.LedgerId, "GiftCard" + kind, refund.Id,
        [
            new(accounts[debit].Code, JournalDirections.Debit, gift.Amount, "GBP", "Gift-card refund", dimensions),
            new(accounts[credit].Code, JournalDirections.Credit, gift.Amount, "GBP", "Gift-card refund", dimensions)
        ], clock.UtcNow), ct);
        operation.JournalEntryId = await GiftCardAccounting.RequirePairAsync(db, TenantId, binding.LedgerId,
            "GiftCard" + kind, refund.Id, debit, credit, gift.Amount, ct);
        if (operation.JournalEntryId != posted.JournalEntryId) throw new InvalidStateException("The gift-card refund journal changed.");
        operation.JournalEntryLineId = await db.JournalEntryLines.Where(x => x.TenantId == TenantId
            && x.JournalEntryId == posted.JournalEntryId && x.LedgerAccountId == binding.LiabilityAccountId
            && x.Direction == (restoring ? JournalDirections.Credit : JournalDirections.Debit))
            .Select(x => x.Id).SingleAsync(ct);
        db.GiftCardOperations.Add(operation);
    }

    public async Task ReleaseRefundTrackedAsync(Refund refund, GiftCardRefundInstruction? gift, CancellationToken ct = default)
    {
        ValidateRefundInstruction(refund, gift);
        if (gift is null) return;
        var kind = gift.Kind == GiftCardRefundInstruction.RestoreTender ? "Restore" : "Reclaim";
        if (refund.EffectsAppliedAtUtc is not null || await OperationAsync(kind, refund.Id, ct) is not null)
            throw new InvalidStateException("Applied gift-card refund value cannot be released.");
        if (gift.Kind != GiftCardRefundInstruction.ReclaimPurchase) return;
        RequireReclaimHold(refund, gift);
        if (refund.GiftReclaimReleasedAtUtc is not null) return;
        var card = await TrackedCardAsync(gift.GiftCardId, ct)
            ?? throw new InvalidStateException("The reserved gift card is unavailable.");
        Touch(card);
        refund.GiftReclaimReleasedAtUtc = clock.UtcNow;
    }

    private void ValidateRefundInstruction(Refund refund, GiftCardRefundInstruction? gift)
    {
        if (refund.TenantId != TenantId || refund.Id == Guid.Empty || refund.PaymentIntentId is null)
            throw new InvalidStateException("The refund belongs to another tenant or payment.");
        if (refund.PaymentIntentId == Guid.Empty || refund.Currency != "GBP" || RefundSnapshot.Read(refund).Gift != gift)
            throw new InvalidStateException("The frozen gift-card refund instruction changed.");
        if (gift is not null && (gift.GiftCardId == Guid.Empty || gift.OriginalOperationId == Guid.Empty
            || gift.Kind is not (GiftCardRefundInstruction.RestoreTender or GiftCardRefundInstruction.ReclaimPurchase)
            || !Money(gift.Amount) || gift.Amount <= 0 || gift.Amount > refund.Amount))
            throw new InvalidStateException("The gift-card refund is invalid.");
    }

    private async Task<(GiftCard Card, GiftCardOperation Original, GiftCardLedgerBinding Binding, GiftCardBalance Balance)> RefundSourceAsync(
        Refund refund, GiftCardRefundInstruction gift, CancellationToken ct)
    {
        // Capture the card's native version before reading mutable refund totals or holds.
        var card = await TrackedCardAsync(gift.GiftCardId, ct)
            ?? throw new InvalidStateException("The original gift card is unavailable.");
        var original = await db.GiftCardOperations.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == TenantId
            && x.Id == gift.OriginalOperationId && !x.IsDeleted, ct)
            ?? throw new InvalidStateException("The original gift-card operation is unavailable.");
        var expectedKind = gift.Kind == GiftCardRefundInstruction.RestoreTender ? "Redeem" : "Issue";
        if (original.Kind != expectedKind || original.GiftCardId != card.Id || original.SourceId != refund.PaymentIntentId)
            throw new InvalidStateException("The refund does not belong to the original gift funding.");
        var intent = await db.PaymentIntents.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == TenantId
            && x.Id == refund.PaymentIntentId && !x.IsDeleted, ct)
            ?? throw new InvalidStateException("The original payment is unavailable.");
        await RequireOriginalRefundProofAsync(intent, card, original, ct);
        var binding = ReadPolicy(card).Ledger!;
        await GiftCardAccounting.BindingAsync(db, TenantId, binding, ct);
        // Validate every prior operation against its actual journal before cumulative caps.
        return (card, original, binding, await BalanceAsync(card, ct));
    }

    private async Task RequireOriginalRefundProofAsync(PaymentIntent intent, GiftCard card, GiftCardOperation original, CancellationToken ct)
    {
        var attempt = await AttemptAsync(intent.Id, ct);
        if (intent.Status != "Captured" || intent.Currency != "GBP" || attempt?.Status != "Completed"
            || attempt.OrderId != intent.OrderId
            || original.OrderId != intent.OrderId || original.SourceId != intent.Id || original.GiftCardId != card.Id)
            throw new InvalidStateException("The original gift-card funding is not complete.");
        var instruction = Read(attempt);
        var funding = new GiftCardFunding(intent.Amount, instruction.Tender?.Amount ?? 0m,
            intent.Amount - (instruction.Tender?.Amount ?? 0m), intent.Currency, instruction.Ledger);
        if (original.Kind == "Redeem" && instruction.Tender is not null && attempt.GiftCardId == card.Id)
            await GiftCardAccounting.RequireRedeemedAsync(db, TenantId, intent, attempt, funding, ct);
        else if (original.Kind == "Issue" && instruction.PurchasedCards().Any(x => x.OrderItemId == card.OrderItemId && x.ItemIndex == card.ItemIndex && x.FaceValue == card.FaceValue))
        {
            if (card.PaymentIntentId != intent.Id)
                throw new InvalidStateException("The refund does not match the original issuance payment.");
            await GiftCardAccounting.RequireIssuedAsync(db, TenantId, card, ct);
            await GiftCardAccounting.RequireCashAsync(db, TenantId, intent, funding, ct);
        }
        else throw new InvalidStateException("The original gift-card instruction does not match its operation.");
    }

    private async Task RequireRefundLimitAsync(Refund refund, GiftCardRefundInstruction gift, GiftCardOperation original, CancellationToken ct)
    {
        var kind = gift.Kind == GiftCardRefundInstruction.RestoreTender ? "Restore" : "Reclaim";
        var applied = await db.GiftCardOperations.AsNoTracking().Where(x => x.TenantId == TenantId
            && x.Kind == kind && x.OriginalOperationId == original.Id && x.SourceId != refund.Id && !x.IsDeleted).SumAsync(x => x.Amount, ct);
        var pending = await db.Refunds.AsNoTracking().Where(x => x.TenantId == TenantId && x.PaymentIntentId == refund.PaymentIntentId
            && x.Id != refund.Id && x.EffectsAppliedAtUtc == null && x.GiftReclaimReleasedAtUtc == null
            && x.Status != "Failed" && !x.IsDeleted).ToListAsync(ct);
        var reserved = pending.Select(x => RefundSnapshot.Read(x).Gift)
            .Where(x => x is not null && x.OriginalOperationId == original.Id && x.Kind == gift.Kind).Sum(x => x!.Amount);
        if (applied + reserved + gift.Amount > original.Amount)
            throw new InvalidStateException("The refund exceeds the remaining original gift-card value.");
    }

    private async Task<bool> RefundOperationExistsAsync(Refund refund, GiftCardRefundInstruction gift,
        GiftCardOperation original, GiftCardLedgerBinding binding, CancellationToken ct)
    {
        var restoring = gift.Kind == GiftCardRefundInstruction.RestoreTender;
        var kind = restoring ? "Restore" : "Reclaim";
        var operation = await OperationAsync(kind, refund.Id, ct);
        if (operation is null) return false;
        if (operation.GiftCardId != gift.GiftCardId || operation.OriginalOperationId != original.Id
            || operation.OrderId != original.OrderId || operation.Amount != gift.Amount)
            throw new InvalidStateException("The refund was already recorded with different gift-card facts.");
        var entry = await GiftCardAccounting.RequirePairAsync(db, TenantId, binding.LedgerId, "GiftCard" + kind, refund.Id,
            restoring ? binding.ClearingAccountId : binding.LiabilityAccountId,
            restoring ? binding.LiabilityAccountId : binding.ClearingAccountId, gift.Amount, ct);
        if (entry != operation.JournalEntryId || !await db.JournalEntryLines.AnyAsync(x => x.TenantId == TenantId
            && x.Id == operation.JournalEntryLineId && x.JournalEntryId == entry && x.LedgerAccountId == binding.LiabilityAccountId
            && x.Direction == (restoring ? JournalDirections.Credit : JournalDirections.Debit)
            && x.Currency == "GBP" && x.Amount == gift.Amount && !x.IsDeleted, ct))
            throw new InvalidStateException("The gift-card refund journal provenance is unavailable.");
        return true;
    }

    private static void RequireReclaimHold(Refund refund, GiftCardRefundInstruction gift)
    {
        if (refund.GiftReclaimCardId != gift.GiftCardId || refund.GiftReclaimAmount != gift.Amount)
            throw new InvalidStateException("The original gift-value refund hold is unavailable.");
    }
}
