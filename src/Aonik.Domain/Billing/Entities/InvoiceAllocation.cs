using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Billing.Entities;

public class InvoiceAllocation : AuditableEntity, ITenantScoped
{
    public Guid InvoiceAllocationId { get; set; }
    public Guid TenantId { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid PaymentId { get; set; }
    public decimal Amount { get; set; }
    public DateTime AllocatedAt { get; set; }
}
