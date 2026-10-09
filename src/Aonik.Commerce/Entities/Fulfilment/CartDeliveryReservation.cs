using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Fulfilment;

public sealed class CartDeliveryReservation : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid CartId { get; set; }
    public Guid CapacityId { get; set; }
    public DateOnly DeliveryDate { get; set; }
    public DateTime SelectedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string Status { get; set; } = DeliveryReservationStatuses.Held;
    public Guid? PaymentAttemptId { get; set; }
    public DateTime? PaymentStartedAtUtc { get; set; }
    public DateTime? PaymentDeadlineUtc { get; set; }
    public Guid? OrderId { get; set; }
}

public static class DeliveryReservationStatuses
{
    public const string Held = "Held";
    public const string PaymentPending = "PaymentPending";
    public const string Committed = "Committed";
    public const string Released = "Released";
}
