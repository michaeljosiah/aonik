using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Autonumbering.Entities;

public class AutonumberReservation : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid AutonumberProfileId { get; set; }
    public long SequenceValue { get; set; }
    public string Reference { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public AutonumberReservationStatus Status { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
}
