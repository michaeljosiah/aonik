using System.Security.Cryptography;

using Aonik.Finance.Entities.GiftCards;
using Aonik.Finance.Entities.Payments;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.GiftCards;
using Aonik.SharedKernel.Abstractions.Ledgers;
using Aonik.SharedKernel.Abstractions.Pricing;
using Aonik.SharedKernel.Events.Integration;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.GiftCards;

internal sealed partial class GiftCardService
{
    // All writes use the payment caller's short transaction and native intent claim.
    public async Task<string?> PrepareTrackedAsync(PaymentIntent intent, GiftCardCheckout? instruction, CancellationToken cancellationToken = default)
    {
        if (instruction is null)
        {
            await ValidateReplayAsync(intent, null, cancellationToken);
            if (await db.OrderItems.AnyAsync(x => x.TenantId == TenantId && x.OrderId == intent.OrderId && x.ItemType == "GiftCardValue" && !x.IsDeleted, cancellationToken))
                throw new InvalidStateException("Gift-value purchases require an issuance instruction.");
            return null;
        }
        await ValidateInstructionAsync(intent, instruction, cancellationToken);
        if (await AttemptAsync(intent.Id, cancellationToken) is not null)
        {
            await ValidateReplayAsync(intent, instruction, cancellationToken);
            return intent.FailureReason;
        }
        var attempt = new GiftCardCheckoutAttempt
        {
            TenantId = TenantId, PaymentIntentId = intent.Id, OrderId = intent.OrderId, CartId = instruction.CartId,
            SnapshotJson = Serialize(instruction), Status = "Released"
        };
        db.GiftCardCheckoutAttempts.Add(attempt);
        // Preserve the exact no-start tombstone even if issuance was disabled or a code expired.
        if (intent.Status == "Cancelled") return null;
        if (instruction.Purchase is not null)
        {
            GiftCardPolicy policy;
            try { policy = await GetPolicyAsync(cancellationToken); }
            catch (InvalidStateException) { return "gift_card.unavailable"; }
            if (!policy.Enabled || !MatchesPolicy(instruction, policy)) return "gift_card.unavailable";
            if (await db.GiftCards.AnyAsync(x => x.TenantId == TenantId && x.CartId == instruction.CartId, cancellationToken))
                return "gift_card.unavailable";
        }
        else
        {
            var tender = instruction.Tender!;
            var id = ReadGrant(tender.PrivateCartGrant, instruction.CartId);
            if (id is null) return "gift_card.invalid";
            // Capture the original native version BEFORE reading balance/reservations.
            var card = await TrackedCardAsync(id.Value, cancellationToken);
            if (card is null) return "gift_card.invalid";
            var reason = UnavailableReason(card);
            if (reason is not null) return reason;
            if (!MatchesPolicy(instruction, ReadPolicy(card))) return "gift_card.tender_changed";
            try { await GiftCardAccounting.BindingAsync(db, TenantId, instruction.Ledger, cancellationToken); }
            catch (InvalidStateException) { return "gift_card.unavailable"; }
            var balance = await BalanceAsync(card, cancellationToken);
            if (tender.Amount > balance.Available) return "gift_card.insufficient_balance";
            attempt.GiftCardId = card.Id;
            attempt.ReservedAmount = tender.Amount;
            Touch(card);
        }
        attempt.Status = "Reserved";
        return null;
    }

    public async Task ValidateReplayAsync(PaymentIntent intent, GiftCardCheckout? instruction, CancellationToken cancellationToken = default)
    {
        if (intent.TenantId != TenantId) throw new InvalidStateException("The payment belongs to another tenant.");
        var attempt = await AttemptAsync(intent.Id, cancellationToken);
        if (attempt is null && instruction is null) return;
        if (attempt is null || instruction is null || attempt.OrderId != intent.OrderId || attempt.CartId != instruction.CartId
            || attempt.SnapshotJson != Serialize(instruction))
            throw new InvalidStateException("The payment attempt has a different frozen gift-card instruction.");
    }

    public async Task<GiftCardFunding> ReadFundingTrackedAsync(PaymentIntent intent, CancellationToken cancellationToken = default)
    {
        if (intent.TenantId != TenantId) throw new InvalidStateException("The payment belongs to another tenant.");
        var attempt = await AttemptAsync(intent.Id, cancellationToken);
        if (attempt is null) return new(intent.Amount, 0, intent.Amount, intent.Currency);
        var instruction = Read(attempt);
        await ValidateInstructionAsync(intent, instruction, cancellationToken);
        var gift = instruction.Tender?.Amount ?? 0;
        return new(intent.Amount, gift, intent.Amount - gift, intent.Currency, instruction.Ledger);
    }

    public async Task CompleteTrackedAsync(PaymentIntent intent, CancellationToken cancellationToken = default)
    {
        var attempt = await AttemptAsync(intent.Id, cancellationToken);
        if (attempt is null)
        {
            if (await db.OrderItems.AnyAsync(x => x.TenantId == TenantId && x.OrderId == intent.OrderId && x.ItemType == "GiftCardValue" && !x.IsDeleted, cancellationToken))
                throw new InvalidStateException("Gift-value capture requires an issuance instruction.");
            return;
        }
        if (intent.TenantId != TenantId || intent.Status != "Captured" || attempt.Status == "Released")
            throw new InvalidStateException("Only a captured reserved gift-card attempt can be completed.");
        var instruction = Read(attempt);
        await ValidateInstructionAsync(intent, instruction, cancellationToken);
        var funding = await ReadFundingTrackedAsync(intent, cancellationToken);
        await GiftCardAccounting.BindingAsync(db, TenantId, instruction.Ledger, cancellationToken);
        await GiftCardAccounting.RequireCashAsync(db, TenantId, intent, funding, cancellationToken);
        if (instruction.Purchase is not null)
        {
          foreach (var purchase in instruction.PurchasedCards())
          {
            var card = db.GiftCards.Local.SingleOrDefault(x => x.TenantId == TenantId && x.CartId == instruction.CartId && x.OrderItemId == purchase.OrderItemId)
                ?? await db.GiftCards.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.CartId == instruction.CartId && x.OrderItemId == purchase.OrderItemId, cancellationToken);
            if (card is null)
            {
                var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
                card = new()
                {
                    TenantId = TenantId, CartId = instruction.CartId, OrderId = intent.OrderId, PaymentIntentId = intent.Id,
                    OrderItemId = purchase.OrderItemId, ItemIndex = purchase.ItemIndex, FaceValue = purchase.FaceValue, Currency = "GBP",
                    PolicySnapshotJson = Serialize(PolicyOf(instruction)), CodeHash = Hash(code), MaskedCode = "•••• " + code[^4..],
                    IssuedAtUtc = clock.UtcNow,
                    ExpiresAtUtc = instruction.Validity.NeverExpires ? null : clock.UtcNow.AddDays(instruction.Validity.ValidForDays!.Value)
                };
                card.ProtectedCode = CodeProtector(card.Id).Protect(code);
                db.GiftCards.Add(card);
            }
            else if (card.IsDeleted || card.PaymentIntentId != intent.Id || card.OrderId != intent.OrderId
                || card.OrderItemId != purchase.OrderItemId || card.ItemIndex != purchase.ItemIndex || card.FaceValue != purchase.FaceValue
                || !MatchesPolicy(instruction, ReadPolicy(card)))
                throw new InvalidStateException("This cart already issued a gift card from different funding.");
            var existing = await OperationAsync("Issue", intent.Id, cancellationToken, card.Id);
            await PostAsync(card, "Issue", intent, purchase.FaceValue, instruction.Ledger, cancellationToken, instruction.AdditionalPurchases?.Count > 0 ? card.Id : intent.Id);
            if (existing is null)
                db.EnqueueIntegrationEvent(new GiftCardIssuedEvent(TenantId, card.Id,
                    new(card.CartId, card.OrderId, card.PaymentIntentId, card.OrderItemId, card.ItemIndex)));
            attempt.GiftCardId ??= card.Id;
          }
        }
        else
        {
            var tender = instruction.Tender!;
            if (attempt.GiftCardId is not { } id || attempt.ReservedAmount != tender.Amount
                || ReadGrant(tender.PrivateCartGrant, instruction.CartId) != id)
                throw new InvalidStateException("The reserved gift-card tender changed.");
            var card = await TrackedCardAsync(id, cancellationToken) ?? throw new InvalidStateException("The reserved gift card is unavailable.");
            if (!MatchesPolicy(instruction, ReadPolicy(card))) throw new InvalidStateException("The reserved gift-card binding changed.");
            // A payment accepted while valid retains its reserved value if the instrument
            // expires during provider processing. Only proven cancellation releases it.
            await PostAsync(card, "Redeem", intent, tender.Amount, instruction.Ledger, cancellationToken);
        }
        attempt.Status = "Completed";
    }

    public async Task ReleaseTrackedAsync(PaymentIntent intent, CancellationToken cancellationToken = default)
    {
        if (intent.TenantId != TenantId) throw new InvalidStateException("The payment belongs to another tenant.");
        var attempt = await AttemptAsync(intent.Id, cancellationToken);
        if (attempt is null || attempt.Status != "Reserved") return;
        if (intent.Status != "Cancelled") throw new InvalidStateException("A payable attempt must retain its gift-card reservation.");
        if (attempt.GiftCardId is { } id)
        {
            var card = await TrackedCardAsync(id, cancellationToken) ?? throw new InvalidStateException("The reserved gift card is unavailable.");
            Touch(card);
        }
        attempt.Status = "Released";
    }

    private async Task PostAsync(GiftCard card, string kind, PaymentIntent intent, decimal amount,
        GiftCardLedgerBinding binding, CancellationToken ct, Guid? journalSource = null)
    {
        var source = journalSource ?? intent.Id;
        var existing = await OperationAsync(kind, intent.Id, ct, card.Id);
        if (existing is not null)
        {
            if (existing.GiftCardId != card.Id || existing.OrderId != intent.OrderId || existing.Amount != amount)
                throw new InvalidStateException("Gift-card operation source has different facts.");
            var existingEntry = await GiftCardAccounting.RequirePairAsync(db, TenantId, binding.LedgerId, "GiftCard" + kind, source,
                kind == "Issue" ? binding.ClearingAccountId : binding.LiabilityAccountId,
                kind == "Issue" ? binding.LiabilityAccountId : binding.ClearingAccountId, amount, ct);
            if (existing.JournalEntryId != existingEntry) throw new InvalidStateException("Gift-card operation journal changed.");
            if (kind == "Redeem") await RequireGiftReceiptAsync(existing, intent, ct);
            return;
        }
        var accounts = await GiftCardAccounting.BindingAsync(db, TenantId, binding, ct);
        var operation = new GiftCardOperation
        {
            TenantId = TenantId, GiftCardId = card.Id, Kind = kind, SourceId = intent.Id,
            OrderId = intent.OrderId, Amount = amount, OccurredAtUtc = clock.UtcNow
        };
        Touch(card);
        var debitId = kind == "Issue" ? binding.ClearingAccountId : binding.LiabilityAccountId;
        var creditId = kind == "Issue" ? binding.LiabilityAccountId : binding.ClearingAccountId;
        var dimensions = Serialize(new { giftCardId = card.Id, operationId = operation.Id, orderId = intent.OrderId });
        var journal = await journals.PostAsync(new(binding.LedgerId, "GiftCard" + kind, source,
        [
            new(accounts[debitId].Code, JournalDirections.Debit, amount, "GBP", "Gift card " + kind, dimensions),
            new(accounts[creditId].Code, JournalDirections.Credit, amount, "GBP", "Gift card " + kind, dimensions)
        ], clock.UtcNow), ct);
        operation.JournalEntryId = await GiftCardAccounting.RequirePairAsync(db, TenantId, binding.LedgerId, "GiftCard" + kind,
            source, debitId, creditId, amount, ct);
        if (operation.JournalEntryId != journal.JournalEntryId) throw new InvalidStateException("Gift-card journal source changed.");
        operation.JournalEntryLineId = await db.JournalEntryLines.Where(x => x.TenantId == TenantId
            && x.JournalEntryId == journal.JournalEntryId && x.LedgerAccountId == binding.LiabilityAccountId
            && x.Direction == (kind == "Issue" ? JournalDirections.Credit : JournalDirections.Debit)).Select(x => x.Id).SingleAsync(ct);
        if (kind == "Redeem")
        {
            operation.PaymentId = operation.Id;
            db.Payments.Add(new Payment
            {
                Id = operation.Id, TenantId = TenantId, PaymentIntentId = intent.Id, Provider = "GiftCard", ProviderReference = card.Id.ToString("N"),
                Amount = amount, Currency = "GBP", CapturedAt = clock.UtcNow, OutcomeStatus = "Captured",
                OutcomeJson = Serialize(new { operationId = operation.Id, journalEntryId = operation.JournalEntryId })
            });
        }
        db.GiftCardOperations.Add(operation);
    }

    private async Task RequireGiftReceiptAsync(GiftCardOperation operation, PaymentIntent intent, CancellationToken ct)
    {
        var receipt = db.Payments.Local.SingleOrDefault(x => x.TenantId == TenantId && x.Id == operation.PaymentId)
            ?? await db.Payments.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == operation.PaymentId, ct);
        if (receipt is null || receipt.IsDeleted || receipt.PaymentIntentId != intent.Id || receipt.Provider != "GiftCard"
            || receipt.ConnectorId is not null || receipt.Amount != operation.Amount || receipt.Currency != "GBP"
            || receipt.OutcomeStatus != "Captured" || receipt.ProviderReference != operation.GiftCardId.ToString("N") || receipt.CapturedAt is null)
            throw new InvalidStateException("Gift-card payment receipt is unavailable.");
    }

    private async Task<GiftCardCheckoutAttempt?> AttemptAsync(Guid paymentIntentId, CancellationToken ct) =>
        db.GiftCardCheckoutAttempts.Local.SingleOrDefault(x => x.TenantId == TenantId && x.PaymentIntentId == paymentIntentId)
        ?? await db.GiftCardCheckoutAttempts.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.PaymentIntentId == paymentIntentId && !x.IsDeleted, ct);
    private async Task<GiftCard?> TrackedCardAsync(Guid id, CancellationToken ct) =>
        db.GiftCards.Local.SingleOrDefault(x => x.TenantId == TenantId && x.Id == id && !x.IsDeleted)
        ?? await db.GiftCards.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id && !x.IsDeleted, ct);
    private async Task<GiftCardOperation?> OperationAsync(string kind, Guid sourceId, CancellationToken ct, Guid? cardId = null) =>
        db.GiftCardOperations.Local.SingleOrDefault(x => x.TenantId == TenantId && x.Kind == kind && x.SourceId == sourceId && (cardId == null || x.GiftCardId == cardId))
        ?? await db.GiftCardOperations.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Kind == kind && x.SourceId == sourceId && (cardId == null || x.GiftCardId == cardId) && !x.IsDeleted, ct);
    private void Touch(GiftCard card)
    {
        card.UpdatedAt = clock.UtcNow;
        if (db.Entry(card).State != EntityState.Added) db.Entry(card).Property(x => x.UpdatedAt).IsModified = true;
    }
    private static GiftCardPolicy PolicyOf(GiftCardCheckout value) => new(true, value.PolicyVersion, "GBP",
        value.Ledger, value.Validity, value.TermsVersion, value.FundingAllocation);
    private static bool MatchesPolicy(GiftCardCheckout instruction, GiftCardPolicy policy) => instruction.PolicyVersion == policy.Version
        && instruction.Ledger == policy.Ledger && instruction.Validity == policy.Validity
        && instruction.TermsVersion == policy.TermsVersion && instruction.FundingAllocation == policy.FundingAllocation;
    private static decimal[] Allocate(decimal amount, IReadOnlyList<GiftCardFundingLine> lines, decimal tax) =>
        ProportionalAllocation.Allocate(amount, lines.Select(x => x.OriginalCharged - x.CouponDiscount - x.PointsAppliedValue).Append(tax).ToArray());

    private static void ValidateQuote(GiftCardQuoteRequest request)
    {
        if (request.CartId == Guid.Empty || request.Currency != "GBP" || !Money(request.Total) || request.Total <= 0
            || !Money(request.RequestedAmount) || request.RequestedAmount <= 0
            || !Share(request.TaxTotal) || request.Lines is null || request.Lines.Count is < 1 or > 500
            || request.Lines.Any(x => x is null || x.ItemIndex < 0 || !Text(x.ItemType, 100) || x.ItemType == "GiftCardValue"
                || !Share(x.OriginalCharged) || !Share(x.CouponDiscount) || !Share(x.PointsAppliedValue) || x.GiftFundedValue != 0
                || x.CouponDiscount + x.PointsAppliedValue > x.OriginalCharged)
            || request.Lines.Select(x => x.ItemIndex).Distinct().Count() != request.Lines.Count
            || request.Lines.Sum(x => x.OriginalCharged - x.CouponDiscount - x.PointsAppliedValue) + request.TaxTotal != request.Total)
            throw new InvalidStateException("Gift-card funding requires exact payable lines, tax and selected tender.");
    }

    private async Task ValidateInstructionAsync(PaymentIntent intent, GiftCardCheckout instruction, CancellationToken ct)
    {
        if (intent.TenantId != TenantId || intent.OrderId == Guid.Empty || intent.Currency != "GBP" || !Money(intent.Amount) || intent.Amount <= 0
            || instruction.CartId == Guid.Empty || (instruction.Purchase is null) == (instruction.Tender is null))
            throw new InvalidStateException("The gift-card instruction does not match its payment.");
        ValidatePolicy(PolicyOf(instruction));
        var giftItems = await db.OrderItems.AsNoTracking().Where(x => x.TenantId == TenantId && x.OrderId == intent.OrderId
            && x.ItemType == "GiftCardValue" && !x.IsDeleted).ToListAsync(ct);
        if (instruction.Purchase is not null)
        {
            var purchases = instruction.PurchasedCards();
            if (purchases.Count is < 1 or > 10 || giftItems.Count != purchases.Count
                || purchases.Select(x => x.OrderItemId).Distinct().Count() != purchases.Count
                || purchases.Select(x => x.ItemIndex).Distinct().Count() != purchases.Count
                || purchases.Sum(x => x.FaceValue) > intent.Amount
                || purchases.Any(purchase => purchase.OrderItemId == Guid.Empty || purchase.ItemIndex < 0
                    || !Money(purchase.FaceValue) || purchase.FaceValue <= 0
                    || !giftItems.Any(item => item.Id == purchase.OrderItemId && item.ItemIndex == purchase.ItemIndex
                        && item.AmountIn == purchase.FaceValue && item.CurrencyIn == "GBP" && item.Quantity == 1 && item.UnitPrice == purchase.FaceValue)))
                throw new InvalidStateException("Gift-card issuance requires one fully funded order line per card, from one to ten cards.");
            return;
        }
        if (instruction.AdditionalPurchases?.Count > 0) throw new InvalidStateException("Gift-card tender cannot include purchases.");
        var tender = instruction.Tender!;
        if (giftItems.Count != 0 || tender.Lines is null || !Money(tender.Amount) || tender.Amount <= 0
            || !Money(tender.ExpectedCardAmount) || tender.Amount + tender.ExpectedCardAmount != intent.Amount
            || tender.ExpectedCardAmount is > 0 and < .30m || !Share(tender.GiftFundedTaxAmount)
            || string.IsNullOrWhiteSpace(tender.PrivateCartGrant) || tender.PrivateCartGrant.Length > 2048)
            throw new InvalidStateException("The frozen gift-card tender is invalid.");
        ValidateQuote(new(instruction.CartId, tender.PrivateCartGrant, intent.Currency, intent.Amount, tender.TaxTotal, tender.Amount,
            tender.Lines.Select(x => x with { GiftFundedValue = 0 }).ToArray()));
        var shares = Allocate(tender.Amount, tender.Lines, tender.TaxTotal);
        if (shares[^1] != tender.GiftFundedTaxAmount || tender.Lines.Where((line, index) => line.GiftFundedValue != shares[index]).Any()
            || tender.Lines.Any(x => x.OrderItemId == Guid.Empty) || tender.Lines.Select(x => x.OrderItemId).Distinct().Count() != tender.Lines.Count)
            throw new InvalidStateException("The frozen gift-card allocation is invalid.");
        var ids = tender.Lines.Select(x => x.OrderItemId).ToArray();
        var items = await db.OrderItems.AsNoTracking().Where(x => x.TenantId == TenantId && x.OrderId == intent.OrderId
            && ids.Contains(x.Id) && !x.IsDeleted).ToDictionaryAsync(x => x.Id, ct);
        if (items.Count != ids.Length || tender.Lines.Any(line => items[line.OrderItemId].ItemIndex != line.ItemIndex
            || items[line.OrderItemId].ItemType != line.ItemType || items[line.OrderItemId].AmountIn != line.OriginalCharged
            || items[line.OrderItemId].CurrencyIn != "GBP"))
            throw new InvalidStateException("A gift-card allocation references an unavailable order line.");
    }
}
