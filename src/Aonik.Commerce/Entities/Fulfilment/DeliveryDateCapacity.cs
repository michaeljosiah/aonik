using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Fulfilment;

public sealed class DeliveryDateCapacity : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public DateOnly DeliveryDate { get; set; }
    public string Unit { get; set; } = string.Empty;
    public int Capacity { get; set; }
}
