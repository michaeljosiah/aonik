using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Orders;

public class OrderNote : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public string Note { get; set; } = string.Empty;
    public Guid? CreatedByUserId { get; set; }
}
