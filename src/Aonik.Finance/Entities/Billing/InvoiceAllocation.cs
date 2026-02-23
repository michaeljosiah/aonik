using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Billing;

public class InvoiceAllocation : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid PaymentId { get; set; }
    public decimal Amount { get; set; }
    public DateTime AllocatedAt { get; set; }
}
