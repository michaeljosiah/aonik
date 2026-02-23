using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Billing;

public class InvoiceLine : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid InvoiceId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal LineTotal { get; set; }
    public string MetadataJson { get; set; } = string.Empty;
}
