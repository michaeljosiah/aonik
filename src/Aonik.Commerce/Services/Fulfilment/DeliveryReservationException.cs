namespace Aonik.Commerce.Services.Fulfilment;

public sealed class DeliveryReservationException(string code, string message) : Exception(message)
{
    public const string Unavailable = "commerce.delivery_availability_unknown";
    public const string Full = "commerce.delivery_date_full";
    public const string NoDelivery = "commerce.no_delivery";
    public const string Expired = "commerce.delivery_reservation_expired";
    public const string Conflict = "commerce.delivery_reservation_conflict";
    public string Code { get; } = code;
}
