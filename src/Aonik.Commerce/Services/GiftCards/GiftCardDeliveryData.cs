using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Aonik.Commerce.Contracts.Models.GiftCards;
using Aonik.Commerce.Entities.Fulfilment;
using Aonik.Commerce.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.GiftCards;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.GiftCards;

internal static class GiftCardDeliveryData
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task StageTrackedAsync(CommerceDbContext db, Guid tenantId, Guid cartId,
        Guid orderId, Guid paymentIntentId, GiftCardPurchaseSnapshot snapshot, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || cartId == Guid.Empty || orderId == Guid.Empty || paymentIntentId == Guid.Empty
            || snapshot.OrderItemId == Guid.Empty || snapshot.ItemIndex < 0 || snapshot.FaceValue <= 0m
            || snapshot.Currency != "GBP" || string.IsNullOrWhiteSpace(snapshot.RecipientName)
            || snapshot.DeliveryMethod is not (GiftCardDeliveryMethods.Email or GiftCardDeliveryMethods.Post or GiftCardDeliveryMethods.InFoodBox)
            || (snapshot.DeliveryMethod == GiftCardDeliveryMethods.Email && (string.IsNullOrWhiteSpace(snapshot.RecipientEmail)
                || snapshot.SendAtUtc is null || snapshot.SendAtUtc.Value.Kind != DateTimeKind.Utc))
            || (snapshot.DeliveryMethod == GiftCardDeliveryMethods.Post && (snapshot.PostalAddress is null || snapshot.PostingDate is null)))
            throw new InvalidStateException("The gift card purchase snapshot is incomplete.");
        var json = JsonSerializer.Serialize(snapshot, Json);
        if (json.Length > 10000) throw new InvalidStateException("The gift card purchase snapshot is too large.");
        var id = StableId("delivery", tenantId, paymentIntentId, snapshot.ItemIndex);
        var existing = db.OrderGiftCardDeliveries.Local.SingleOrDefault(row => row.Id == id)
            ?? await db.OrderGiftCardDeliveries.AsNoTracking().SingleOrDefaultAsync(row => row.Id == id && row.TenantId == tenantId, ct);
        if (existing is not null)
        {
            if (existing.CartId != cartId || existing.OrderId != orderId || existing.PaymentIntentId != paymentIntentId
                || existing.PurchaseSnapshotJson != json)
                throw new InvalidStateException("The gift card delivery is already bound to another purchase snapshot.");
            return;
        }
        db.OrderGiftCardDeliveries.Add(new OrderGiftCardDelivery
        {
            Id = id, TenantId = tenantId, CartId = cartId, OrderId = orderId, PaymentIntentId = paymentIntentId,
            OrderItemId = snapshot.OrderItemId, ItemIndex = snapshot.ItemIndex, PurchaseSnapshotJson = json,
            DeliveryMethod = snapshot.DeliveryMethod, SendAtUtc = snapshot.SendAtUtc, PostingDate = snapshot.PostingDate
        });
    }

    public static GiftCardPurchaseSnapshot Read(OrderGiftCardDelivery row)
        => JsonSerializer.Deserialize<GiftCardPurchaseSnapshot>(row.PurchaseSnapshotJson, Json)
            ?? throw new InvalidStateException("The gift card purchase snapshot is unavailable.");

    public static GiftCardPurchaseSource Source(OrderGiftCardDelivery row)
        => new(row.CartId, row.OrderId, row.PaymentIntentId, row.OrderItemId, row.ItemIndex);

    public static Guid TaskId(Guid tenantId, Guid deliveryId, int sequence)
        => StableId("email", tenantId, deliveryId, sequence);

    private static Guid StableId(string purpose, Guid tenantId, Guid sourceId, int sequence)
        => new(SHA256.HashData(Encoding.UTF8.GetBytes($"aonik:gift-card:{purpose}:{tenantId:D}:{sourceId:D}:{sequence}"))[..16]);
}
