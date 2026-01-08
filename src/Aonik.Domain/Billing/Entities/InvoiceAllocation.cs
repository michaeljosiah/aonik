using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Billing.Entities;

public class InvoiceAllocation : AuditableEntity, ITenantScoped
{
    public Guid InvoiceAllocationId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid InvoiceId { get; private set; }
    public Guid PaymentId { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime AllocatedAt { get; private set; }

    private InvoiceAllocation() { }

    public InvoiceAllocation(Guid tenantId, Guid invoiceId, Guid paymentId, decimal amount)
    {
        InvoiceAllocationId = Id;
        TenantId = tenantId;
        InvoiceId = invoiceId;
        PaymentId = paymentId;
        Amount = amount;
        AllocatedAt = DateTime.UtcNow;
    }
}
