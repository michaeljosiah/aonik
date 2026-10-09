using System.Text.Json.Serialization;

namespace Aonik.Commerce.Contracts.Models.Fulfilment;

public static class DeliveryAvailabilityStatuses
{
    public const string Available = "available";
    public const string FullyBooked = "fully_booked";
    public const string NoDelivery = "no_delivery";
    public const string Unknown = "unknown";
}

public record DeliveryDateAvailabilityDto(DateOnly DeliveryDate, string Status);
public record DeliveryDateCapacityDto(DateOnly DeliveryDate, string Unit, int Capacity, int Occupied, string Version);
public record UpdateDeliveryDateCapacityRequest(
    [property: JsonRequired] string Unit,
    [property: JsonRequired] int Capacity,
    string? ExpectedVersion = null);
public record ReserveDeliveryDateRequest(DateOnly DeliveryDate);
public record DeliveryReservationDto(Guid Id, DateOnly DeliveryDate, string Status,
    DateTime SelectedAtUtc, DateTime ExpiresAtUtc, Guid? PaymentAttemptId,
    DateTime? PaymentStartedAtUtc, DateTime? PaymentDeadlineUtc, Guid? OrderId);
public record CartDeliveryReservationDto(Guid CartId, string CartVersion, DateTime ServerNowUtc,
    DeliveryReservationDto? Reservation);
