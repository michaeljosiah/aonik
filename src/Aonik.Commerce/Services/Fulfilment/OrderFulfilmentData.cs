using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Contracts.Models.Fulfilment;
using Aonik.Commerce.Entities.Fulfilment;
using Aonik.SharedKernel.Abstractions.Ordering;

namespace Aonik.Commerce.Services.Fulfilment;

internal static class OrderFulfilmentData
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string? Status(string? recordedStatus, string paymentStatus, string orderStatus)
        => orderStatus is OrderStatusCodes.Cancelled or OrderStatusCodes.Failed or OrderStatusCodes.Expired ? "Cancelled"
            : paymentStatus == CheckoutPaymentStatuses.Captured ? recordedStatus ?? OrderFulfilmentStatuses.Confirmed : null;

    public static string HistoryGroup(string? recordedStatus, string paymentStatus, string orderStatus)
        => Status(recordedStatus, paymentStatus, orderStatus) is "Cancelled" or OrderFulfilmentStatuses.Delivered ? "Past"
            : paymentStatus == CheckoutPaymentStatuses.Captured ? "Upcoming" : "PendingPayment";

    public static OrderFulfilmentDto Map(OrderDeliveryDetails delivery) => new(
        delivery.FulfilmentStatus ?? OrderFulfilmentStatuses.Confirmed,
        Convert.ToBase64String(delivery.RowVersion), ReadHistory(delivery));

    public static List<OrderFulfilmentEvent> ReadHistory(OrderDeliveryDetails delivery)
        => delivery.FulfilmentHistoryJson is null ? []
            : JsonSerializer.Deserialize<List<OrderFulfilmentEvent>>(delivery.FulfilmentHistoryJson, Json)
                ?? throw new InvalidOperationException("The recorded fulfilment history is invalid.");

    public static string Serialize(IReadOnlyList<OrderFulfilmentEvent> history)
    {
        var json = JsonSerializer.Serialize(history, Json);
        if (history.Count > 3 || json.Length > 4000)
            throw new InvalidOperationException("The recorded fulfilment history is invalid.");
        return json;
    }
}
