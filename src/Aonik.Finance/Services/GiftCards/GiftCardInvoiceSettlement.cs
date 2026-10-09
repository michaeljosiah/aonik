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
        var captured = await db.PaymentIntents.AsNoTracking().Where(x => x.TenantId == tenantId && x.OrderId == orderId
            && intentIds.Contains(x.Id) && x.Status == "Captured" && !x.IsDeleted).ToListAsync(cancellationToken);
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

        var purchase = snapshot.Purchase!;
        var card = await db.GiftCards.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.OrderId == orderId
            && x.PaymentIntentId == intent.Id && x.Id == attempt.GiftCardId && !x.IsDeleted, cancellationToken)
            ?? throw new InvalidStateException("Gift-value settlement must wait for funded issuance.");
        if (items.Count != 1 || purchase.OrderItemId != items[0].Id || card.OrderItemId != items[0].Id
            || purchase.ItemIndex != items[0].ItemIndex || purchase.FaceValue != card.FaceValue || items[0].AmountIn != card.FaceValue
            || invoiceTotal < card.FaceValue || GiftCardService.ReadPolicy(card).Ledger != snapshot.Ledger)
            throw new InvalidStateException("The invoice does not match funded gift issuance.");
        await GiftCardAccounting.RequireIssuedAsync(db, tenantId, card, cancellationToken);
        await GiftCardAccounting.RequireCashAsync(db, tenantId, intent,
            new(intent.Amount, 0, intent.Amount, currency, snapshot.Ledger), cancellationToken);
        return new(snapshot.Ledger, card.FaceValue);
    }
}

internal sealed record GiftCardSettlement(GiftCardLedgerBinding Ledger, decimal GiftValue);
