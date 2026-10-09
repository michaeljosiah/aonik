using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Promotions;

public class DiscountReservation : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid CartId { get; set; }
    public Guid DiscountId { get; set; }
    public Guid AttemptId { get; set; }
    public string Status { get; set; } = DiscountReservationStatuses.Reserved;
}

public static class DiscountReservationStatuses
{
    public const string Reserved = "Reserved";
    public const string Redeemed = "Redeemed";
    public const string Released = "Released";
}
