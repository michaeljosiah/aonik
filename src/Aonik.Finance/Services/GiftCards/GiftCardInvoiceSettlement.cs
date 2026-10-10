using System.Text.Json;

using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.GiftCards;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.GiftCards;

/// <summary>Gift issuance and redemption settle through their proved original funding accounts.</summary>
internal static class GiftCardInvoiceSettlement
{
    public static async Task<GiftCardSettlement?> ReadAsync(FinanceDbContext db, Guid tenantId, Guid? orderId,
        decimal invoiceTotal, string currency, CancellationToken cancellationToken = default)
    {
        if (orderId is null) return null;
        var items = await db.OrderItems.AsNoTracking().Where(x => x.TenantId == tenantId && x.OrderId == orderId
            && x.ItemType == "GiftCardValue" && !x.IsDeleted).ToListAsync(cancellationToken);
        var attempts = await db.GiftCardCheckoutAttempts.AsNoTracking().Where(x => x.TenantId == tenantId
            && x.OrderId == orderId && !x.IsDeleted).ToListAsync(cancellationToken);
        if (items.Count == 0 && attempts.Count == 0) return null;
        if (items.Count > 1 || currency != "GBP" || invoiceTotal <= 0)
            throw new InvalidStateException("Gift-card settlement requires its exact funded invoice.");

        var intentIds = attempts.Select(x => x.PaymentIntentId).ToArray();
        var intents = await db.PaymentIntents.AsNoTracking().Where(x => x.TenantId == tenantId && x.OrderId == orderId
            && intentIds.Contains(x.Id) && !x.IsDeleted).ToListAsync(cancellationToken);
        if (items.Count == 0)
        {
            // A closed tender from an earlier try must not redirect a later cash-only sale.
            // Neither missing payment state nor a one-sided release proves unpaid closure.
            attempts.RemoveAll(attempt => attempt.Status == "Released"
                && intents.Any(intent => intent.Id == attempt.PaymentIntentId && intent.Status == "Cancelled"));
            if (attempts.Count == 0) return null;
            if (attempts.Count != 1)
                throw new InvalidStateException("Gift-card settlement must wait for completed funding.");
        }
        var captured = intents.Where(intent => intent.Status == "Captured"
            && attempts.Any(attempt => attempt.PaymentIntentId == intent.Id)).ToList();
        if (captured.Count != 1)
            throw new InvalidStateException("Gift-card settlement must wait for completed funding.");
        var intent = captured[0];
        var attempt = attempts.Single(x => x.PaymentIntentId == intent.Id);
        var snapshot = JsonSerializer.Deserialize<GiftCardCheckout>(attempt.SnapshotJson, GiftCardService.Json);
        if (attempt.Status != "Completed" || snapshot is null || snapshot.CartId != attempt.CartId
            || (snapshot.Purchase is null) == (snapshot.Tender is null) || intent.Currency != currency || intent.Amount != invoiceTotal)
            throw new InvalidStateException("The invoice does not match completed gift-card funding.");
        await GiftCardAccounting.BindingAsync(db, tenantId, snapshot.Ledger, cancellationToken);

        if (snapshot.Tender is { } tender)
        {
            if (items.Count != 0 || tender.Amount <= 0 || tender.ExpectedCardAmount < 0
                || tender.Amount + tender.ExpectedCardAmount != invoiceTotal)
                throw new InvalidStateException("The invoice does not match redeemed gift funding.");
            await GiftCardAccounting.RequireRedeemedAsync(db, tenantId, intent, attempt,
                new(invoiceTotal, tender.Amount, tender.ExpectedCardAmount, currency, snapshot.Ledger), cancellationToken);
            return new(snapshot.Ledger, 0);
        }

        var purchases = snapshot.PurchasedCards();
        var cards = await db.GiftCards.AsNoTracking().Where(x => x.TenantId == tenantId && x.OrderId == orderId
            && x.PaymentIntentId == intent.Id && !x.IsDeleted).ToListAsync(cancellationToken);
        if (items.Count != purchases.Count || cards.Count != purchases.Count || invoiceTotal < purchases.Sum(x => x.FaceValue))
            throw new InvalidStateException("The invoice does not match funded gift issuance.");
        foreach (var purchase in purchases)
        {
            var card = cards.SingleOrDefault(x => x.OrderItemId == purchase.OrderItemId);
            var item = items.SingleOrDefault(x => x.Id == purchase.OrderItemId);
            if (card is null || item is null || card.ItemIndex != purchase.ItemIndex || item.ItemIndex != purchase.ItemIndex
                || purchase.FaceValue != card.FaceValue || item.AmountIn != card.FaceValue || GiftCardService.ReadPolicy(card).Ledger != snapshot.Ledger)
                throw new InvalidStateException("The invoice does not match funded gift issuance.");
            await GiftCardAccounting.RequireIssuedAsync(db, tenantId, card, cancellationToken);
        }
        await GiftCardAccounting.RequireCashAsync(db, tenantId, intent,
            new(intent.Amount, 0, intent.Amount, currency, snapshot.Ledger), cancellationToken);
        return new(snapshot.Ledger, purchases.Sum(x => x.FaceValue));

    }
}

internal sealed record GiftCardSettlement(GiftCardLedgerBinding Ledger, decimal GiftValue);
