using System.Text.Json;

using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.GiftCards;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.GiftCards;

/// <summary>Gift-value invoices can settle only after funded issuance, never through generic revenue fallback.</summary>
internal static class GiftCardInvoiceSettlement
{
    public static async Task<GiftCardSettlement?> ReadAsync(FinanceDbContext db, Guid tenantId, Guid? orderId,
        decimal invoiceTotal, string currency, CancellationToken cancellationToken = default)
    {
        if (orderId is null) return null;
        var items = await db.OrderItems.AsNoTracking().Where(x => x.TenantId == tenantId && x.OrderId == orderId
            && x.ItemType == "GiftCardValue" && !x.IsDeleted).ToListAsync(cancellationToken);
        if (items.Count == 0) return null;
        if (items.Count != 1 || currency != "GBP") throw new InvalidStateException("Gift-value settlement requires its exact issued funding.");
        var item = items[0];
        var card = await db.GiftCards.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.OrderId == orderId
            && x.OrderItemId == item.Id && !x.IsDeleted, cancellationToken)
            ?? throw new InvalidStateException("Gift-value settlement must wait for funded issuance.");
        await GiftCardAccounting.RequireIssuedAsync(db, tenantId, card, cancellationToken);
        var intent = await db.PaymentIntents.AsNoTracking().SingleAsync(x => x.TenantId == tenantId && x.Id == card.PaymentIntentId, cancellationToken);
        var attempt = await db.GiftCardCheckoutAttempts.AsNoTracking().SingleAsync(x => x.TenantId == tenantId && x.PaymentIntentId == intent.Id, cancellationToken);
        var snapshot = JsonSerializer.Deserialize<GiftCardCheckout>(attempt.SnapshotJson, GiftCardService.Json);
        if (snapshot?.Purchase is not { } purchase || snapshot.Tender is not null || purchase.OrderItemId != item.Id
            || purchase.ItemIndex != item.ItemIndex || purchase.FaceValue != card.FaceValue || item.AmountIn != card.FaceValue
            || intent.Currency != currency || intent.Amount != invoiceTotal || invoiceTotal < card.FaceValue)
            throw new InvalidStateException("The invoice does not match funded gift issuance.");
        await GiftCardAccounting.BindingAsync(db, tenantId, snapshot.Ledger, cancellationToken);
        await GiftCardAccounting.RequireCashAsync(db, tenantId, intent,
            new(intent.Amount, 0, intent.Amount, currency, snapshot.Ledger), cancellationToken);
        return new(snapshot.Ledger, card.FaceValue);
    }
}

internal sealed record GiftCardSettlement(GiftCardLedgerBinding Ledger, decimal GiftValue);
