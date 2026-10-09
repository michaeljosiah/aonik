namespace Aonik.Commerce.Contracts.Models.Fulfilment;

public record UpdateOrderFulfilmentCommand(string Status, string ExpectedVersion);

/// <summary>Staff-only audit facts, committed atomically with the delivery row.</summary>
public record OrderFulfilmentEvent(string FromStatus, string ToStatus, Guid ActorId, DateTime OccurredAtUtc);

public record OrderFulfilmentDto(string Status, string Version, IReadOnlyList<OrderFulfilmentEvent> History);

public static class OrderFulfilmentStatuses
{
    public const string Confirmed = "Confirmed";
    public const string Cooking = "Cooking";
    public const string OutForDelivery = "OutForDelivery";
    public const string Delivered = "Delivered";
}
